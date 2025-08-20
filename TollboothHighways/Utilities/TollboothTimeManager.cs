using System;
using Unity.Mathematics;
using Game.Common;
using Game.Simulation;
using Unity.Entities;
using Game.Prefabs;

namespace Utilities
{
    /// <summary>
    /// Manages time-based pricing for tollbooth entities in Cities Skylines II.
    /// Handles peak and non-peak traffic hours for different days of the week.
    /// </summary>
    public static class TollboothTimeManager
    {
        #region Public Enums

        /// <summary>
        /// Represents the day of the week
        /// </summary>
        public enum DayOfWeek
        {
            Monday = 0,
            Tuesday = 1,
            Wednesday = 2,
            Thursday = 3,
            Friday = 4,
            Saturday = 5,
            Sunday = 6
        }

        /// <summary>
        /// Represents traffic density periods
        /// </summary>
        public enum TrafficPeriod
        {
            Peak,
            NonPeak
        }

        #endregion

        #region Time Window Structures

        /// <summary>
        /// Represents a time window within a day
        /// </summary>
        public struct TimeWindow
        {
            public int StartHour;
            public int EndHour;

            public TimeWindow(int startHour, int endHour)
            {
                StartHour = startHour;
                EndHour = endHour;
            }

            /// <summary>
            /// Checks if the given hour falls within this time window
            /// </summary>
            public bool ContainsHour(int hour)
            {
                // Handle cases where the window crosses midnight
                if (StartHour > EndHour)
                {
                    return hour >= StartHour || hour < EndHour;
                }
                return hour >= StartHour && hour < EndHour;
            }
        }

        /// <summary>
        /// Represents all time windows for a specific day type
        /// </summary>
        public struct DayTimeWindows
        {
            public TimeWindow[] PeakWindows;
            public TimeWindow[] NonPeakWindows;

            public DayTimeWindows(TimeWindow[] peakWindows, TimeWindow[] nonPeakWindows)
            {
                PeakWindows = peakWindows;
                NonPeakWindows = nonPeakWindows;
            }
        }

        #endregion

        #region Static Time Configuration

        // Weekday (Monday-Friday) time windows
        private static readonly DayTimeWindows WeekdayWindows = new DayTimeWindows(
            peakWindows: new TimeWindow[]
            {
                new TimeWindow(7, 11),   // Morning peak: 7:00-11:00
                new TimeWindow(16, 20)   // Evening peak: 16:00-20:00
            },
            nonPeakWindows: new TimeWindow[]
            {
                new TimeWindow(0, 7),    // Night/Early morning: 0:00-7:00
                new TimeWindow(11, 16),  // Midday: 11:00-16:00
                new TimeWindow(20, 24)   // Evening/Night: 20:00-24:00 (handled as 20:00-0:00)
            }
        );

        // Weekend (Saturday-Sunday) time windows
        private static readonly DayTimeWindows WeekendWindows = new DayTimeWindows(
            peakWindows: new TimeWindow[]
            {
                new TimeWindow(11, 15),  // Midday peak: 11:00-15:00
                new TimeWindow(17, 21)   // Evening peak: 17:00-21:00
            },
            nonPeakWindows: new TimeWindow[]
            {
                new TimeWindow(0, 11),   // Night/Morning: 0:00-11:00
                new TimeWindow(15, 17),  // Afternoon: 15:00-17:00
                new TimeWindow(21, 24)   // Night: 21:00-24:00 (handled as 21:00-0:00)
            }
        );

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the current day of the week based on game time
        /// </summary>
        /// <param name="timeSystem">The game's time system</param>
        /// <param name="timeData">Current time data</param>
        /// <returns>Current day of the week</returns>
        public static DayOfWeek GetCurrentDayOfWeek(TimeSystem timeSystem, TimeData timeData)
        {
            // Get the current day number since the game started
            int currentDay = TimeSystem.GetDay(timeSystem.World.GetExistingSystemManaged<SimulationSystem>().frameIndex, timeData);
            
            // Assume the game starts on a Monday (day 0), so we can calculate day of week
            // You can adjust the offset if the game starts on a different day
            int dayOfWeek = currentDay % 7;
            return (DayOfWeek)dayOfWeek;
        }

        /// <summary>
        /// Gets the current hour of the day from game time
        /// </summary>
        /// <param name="timeSystem">The game's time system</param>
        /// <param name="timeSettingsData">Time settings data</param>
        /// <param name="timeData">Current time data</param>
        /// <returns>Current hour (0-23)</returns>
        public static int GetCurrentHour(TimeSystem timeSystem, TimeSettingsData timeSettingsData, TimeData timeData)
        {
            float timeOfDay = timeSystem.GetTimeOfDay(timeSettingsData, timeData, 
                timeSystem.World.GetExistingSystemManaged<SimulationSystem>().frameIndex);
            
            // Convert normalized time (0.0-1.0) to hour (0-23)
            return math.clamp((int)(timeOfDay * 24f), 0, 23);
        }

        /// <summary>
        /// Gets the current minute of the hour from game time
        /// </summary>
        /// <param name="timeSystem">The game's time system</param>
        /// <param name="timeSettingsData">Time settings data</param>
        /// <param name="timeData">Current time data</param>
        /// <returns>Current minute (0-59)</returns>
        public static int GetCurrentMinute(TimeSystem timeSystem, TimeSettingsData timeSettingsData, TimeData timeData)
        {
            float timeOfDay = timeSystem.GetTimeOfDay(timeSettingsData, timeData,
                timeSystem.World.GetExistingSystemManaged<SimulationSystem>().frameIndex);
            
            // Get the fractional part of the hour and convert to minutes
            float hourFraction = timeOfDay * 24f % 1f;
            return math.clamp((int)(hourFraction * 60f), 0, 59);
        }

        /// <summary>
        /// Determines the current traffic period (Peak or NonPeak)
        /// </summary>
        /// <param name="timeSystem">The game's time system</param>
        /// <param name="timeSettingsData">Time settings data</param>
        /// <param name="timeData">Current time data</param>
        /// <returns>Current traffic period</returns>
        public static TrafficPeriod GetCurrentTrafficPeriod(TimeSystem timeSystem, TimeSettingsData timeSettingsData, TimeData timeData)
        {
            DayOfWeek dayOfWeek = GetCurrentDayOfWeek(timeSystem, timeData);
            int currentHour = GetCurrentHour(timeSystem, timeSettingsData, timeData);

            return GetTrafficPeriodForTime(dayOfWeek, currentHour);
        }

        /// <summary>
        /// Determines the traffic period for a specific day and hour
        /// </summary>
        /// <param name="dayOfWeek">Day of the week</param>
        /// <param name="hour">Hour of the day (0-23)</param>
        /// <returns>Traffic period for the specified time</returns>
        public static TrafficPeriod GetTrafficPeriodForTime(DayOfWeek dayOfWeek, int hour)
        {
            DayTimeWindows windows = IsWeekend(dayOfWeek) ? WeekendWindows : WeekdayWindows;

            // Check if hour falls within any peak window
            foreach (var peakWindow in windows.PeakWindows)
            {
                if (peakWindow.ContainsHour(hour))
                {
                    return TrafficPeriod.Peak;
                }
            }

            // If not in peak, it must be non-peak
            return TrafficPeriod.NonPeak;
        }

        /// <summary>
        /// Checks if the given day is a weekend
        /// </summary>
        /// <param name="dayOfWeek">Day of the week to check</param>
        /// <returns>True if Saturday or Sunday, false otherwise</returns>
        public static bool IsWeekend(DayOfWeek dayOfWeek)
        {
            return dayOfWeek == DayOfWeek.Saturday || dayOfWeek == DayOfWeek.Sunday;
        }

        /// <summary>
        /// Checks if the given day is a weekday
        /// </summary>
        /// <param name="dayOfWeek">Day of the week to check</param>
        /// <returns>True if Monday through Friday, false otherwise</returns>
        public static bool IsWeekday(DayOfWeek dayOfWeek)
        {
            return !IsWeekend(dayOfWeek);
        }

        /// <summary>
        /// Gets all peak time windows for a specific day of the week
        /// </summary>
        /// <param name="dayOfWeek">Day of the week</param>
        /// <returns>Array of peak time windows</returns>
        public static TimeWindow[] GetPeakWindows(DayOfWeek dayOfWeek)
        {
            return IsWeekend(dayOfWeek) ? WeekendWindows.PeakWindows : WeekdayWindows.PeakWindows;
        }

        /// <summary>
        /// Gets all non-peak time windows for a specific day of the week
        /// </summary>
        /// <param name="dayOfWeek">Day of the week</param>
        /// <returns>Array of non-peak time windows</returns>
        public static TimeWindow[] GetNonPeakWindows(DayOfWeek dayOfWeek)
        {
            return IsWeekend(dayOfWeek) ? WeekendWindows.NonPeakWindows : WeekdayWindows.NonPeakWindows;
        }

        /// <summary>
        /// Gets a formatted string representation of the current time
        /// </summary>
        /// <param name="timeSystem">The game's time system</param>
        /// <param name="timeSettingsData">Time settings data</param>
        /// <param name="timeData">Current time data</param>
        /// <returns>Formatted time string (HH:MM)</returns>
        public static string GetFormattedCurrentTime(TimeSystem timeSystem, TimeSettingsData timeSettingsData, TimeData timeData)
        {
            int hour = GetCurrentHour(timeSystem, timeSettingsData, timeData);
            int minute = GetCurrentMinute(timeSystem, timeSettingsData, timeData);
            
            return $"{hour:D2}:{minute:D2}";
        }

        /// <summary>
        /// Gets a formatted string representation of the current day and time
        /// </summary>
        /// <param name="timeSystem">The game's time system</param>
        /// <param name="timeSettingsData">Time settings data</param>
        /// <param name="timeData">Current time data</param>
        /// <returns>Formatted day and time string</returns>
        public static string GetFormattedCurrentDateTime(TimeSystem timeSystem, TimeSettingsData timeSettingsData, TimeData timeData)
        {
            DayOfWeek dayOfWeek = GetCurrentDayOfWeek(timeSystem, timeData);
            string timeString = GetFormattedCurrentTime(timeSystem, timeSettingsData, timeData);
            
            return $"{dayOfWeek} {timeString}";
        }

        /// <summary>
        /// Calculates how many minutes until the next traffic period change
        /// </summary>
        /// <param name="timeSystem">The game's time system</param>
        /// <param name="timeSettingsData">Time settings data</param>
        /// <param name="timeData">Current time data</param>
        /// <returns>Minutes until next period change</returns>
        public static int GetMinutesUntilNextPeriodChange(TimeSystem timeSystem, TimeSettingsData timeSettingsData, TimeData timeData)
        {
            DayOfWeek dayOfWeek = GetCurrentDayOfWeek(timeSystem, timeData);
            int currentHour = GetCurrentHour(timeSystem, timeSettingsData, timeData);
            int currentMinute = GetCurrentMinute(timeSystem, timeSettingsData, timeData);
            
            DayTimeWindows windows = IsWeekend(dayOfWeek) ? WeekendWindows : WeekdayWindows;
            TrafficPeriod currentPeriod = GetTrafficPeriodForTime(dayOfWeek, currentHour);
            
            // Find the next time window boundary
            int nextBoundaryHour = FindNextTimeBoundary(currentHour, windows);
            
            if (nextBoundaryHour == currentHour)
            {
                return 0; // We're at a boundary
            }
            
            // Calculate minutes difference
            int totalCurrentMinutes = currentHour * 60 + currentMinute;
            int totalNextMinutes = nextBoundaryHour * 60;
            
            // Handle day boundary crossing
            if (nextBoundaryHour < currentHour)
            {
                totalNextMinutes += 24 * 60; // Add a full day
            }
            
            return totalNextMinutes - totalCurrentMinutes;
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Finds the next time boundary (start or end of any time window)
        /// </summary>
        /// <param name="currentHour">Current hour</param>
        /// <param name="windows">Day time windows</param>
        /// <returns>Hour of the next boundary</returns>
        private static int FindNextTimeBoundary(int currentHour, DayTimeWindows windows)
        {
            int nextBoundary = 24; // Default to end of day
            
            // Check all peak window boundaries
            foreach (var window in windows.PeakWindows)
            {
                if (window.StartHour > currentHour && window.StartHour < nextBoundary)
                {
                    nextBoundary = window.StartHour;
                }
                if (window.EndHour > currentHour && window.EndHour < nextBoundary)
                {
                    nextBoundary = window.EndHour;
                }
            }
            
            // Check all non-peak window boundaries
            foreach (var window in windows.NonPeakWindows)
            {
                if (window.StartHour > currentHour && window.StartHour < nextBoundary)
                {
                    nextBoundary = window.StartHour;
                }
                if (window.EndHour > currentHour && window.EndHour < nextBoundary)
                {
                    nextBoundary = window.EndHour;
                }
            }
            
            // If no boundary found today, return the first boundary of tomorrow
            if (nextBoundary == 24)
            {
                nextBoundary = 0; // Midnight - start of the next day
                // Find the earliest boundary in the day
                foreach (var window in windows.PeakWindows)
                {
                    if (window.StartHour < nextBoundary || nextBoundary == 0)
                        nextBoundary = window.StartHour;
                }
                foreach (var window in windows.NonPeakWindows)
                {
                    if (window.StartHour < nextBoundary || nextBoundary == 0)
                        nextBoundary = window.StartHour;
                }
            }
            
            return nextBoundary;
        }

        #endregion
    }
}