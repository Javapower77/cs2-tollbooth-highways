using Unity.Entities;
using Colossal.Serialization.Entities;

namespace TollboothHighways.Domain.Components
{
    /// <summary>
    /// Component to track toll income statistics for the entire city
    /// </summary>
    public struct TollIncomeStatistics : IComponentData, ISerializable
    {
        public float DailyIncome;
        public float WeeklyIncome;
        public float MonthlyIncome;
        public float TotalIncome;
        public int DailyVehicleCount;
        public int WeeklyVehicleCount;
        public int MonthlyVehicleCount;
        public int TotalVehicleCount;
        public uint LastDayUpdate;
        public uint LastWeekUpdate;
        public uint LastMonthUpdate;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(DailyIncome);
            writer.Write(WeeklyIncome);
            writer.Write(MonthlyIncome);
            writer.Write(TotalIncome);
            writer.Write(DailyVehicleCount);
            writer.Write(WeeklyVehicleCount);
            writer.Write(MonthlyVehicleCount);
            writer.Write(TotalVehicleCount);
            writer.Write(LastDayUpdate);
            writer.Write(LastWeekUpdate);
            writer.Write(LastMonthUpdate);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out DailyIncome);
            reader.Read(out WeeklyIncome);
            reader.Read(out MonthlyIncome);
            reader.Read(out TotalIncome);
            reader.Read(out DailyVehicleCount);
            reader.Read(out WeeklyVehicleCount);
            reader.Read(out MonthlyVehicleCount);
            reader.Read(out TotalVehicleCount);
            reader.Read(out LastDayUpdate);
            reader.Read(out LastWeekUpdate);
            reader.Read(out LastMonthUpdate);
        }
    }
}