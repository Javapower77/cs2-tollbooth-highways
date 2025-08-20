import React, { useState, useEffect } from 'react';
import { bindValue, useValue, trigger } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import mod from "../../../mod.json";
import { 
  TrafficVolumeChart, 
  TrafficFlowChart,
  TrafficChartTheme 
} from "../UI/game-ui-modules/TrafficChartComponents";
import { 
  InfoSection, 
  InfoRow 
} from "../UI/game-ui-modules/InfoSectionComponents";
import styles from "./TollboothTrafficAnalysis.module.scss";

// Bind traffic data from C# backend
const m_TrafficVolumeData$ = bindValue<number[]>(mod.id, "m_TrafficVolumeData");
const m_TrafficFlowData$ = bindValue<number[]>(mod.id, "m_TrafficFlowData");
const m_PeakHourVolume$ = bindValue<number>(mod.id, "m_PeakHourVolume");
const m_AverageFlowEfficiency$ = bindValue<number>(mod.id, "m_AverageFlowEfficiency");
const m_TotalVehiclesProcessed$ = bindValue<number>(mod.id, "m_TotalVehiclesProcessed");

export const TollboothTrafficAnalysis: React.FC = () => {
  const { translate } = useLocalization();
  
  // Get data from C# backend
  const trafficVolumeData = useValue(m_TrafficVolumeData$) || [0, 0, 0, 0, 0];
  const trafficFlowData = useValue(m_TrafficFlowData$) || [0, 0, 0, 0, 0];
  const peakHourVolume = useValue(m_PeakHourVolume$) || 0;
  const averageFlowEfficiency = useValue(m_AverageFlowEfficiency$) || 0;
  const totalVehiclesProcessed = useValue(m_TotalVehiclesProcessed$) || 0;

  // Local state for chart customization
  const [selectedTimeRange, setSelectedTimeRange] = useState('24h');
  const [showDetailedView, setShowDetailedView] = useState(false);

  const handleTimeRangeChange = (range: string) => {
    setSelectedTimeRange(range);
    // Trigger C# backend to update data for new time range
    trigger(mod.id, "setTrafficAnalysisTimeRange", range);
  };

  const handleToggleDetailedView = () => {
    setShowDetailedView(!showDetailedView);
    trigger(mod.id, "setDetailedTrafficView", !showDetailedView);
  };

  return (
    <div className={styles.trafficAnalysisContainer}>
      {/* Traffic Volume Section */}
      <InfoSection 
        focusKey="traffic-volume-section"
        tooltip="Traffic volume analysis showing vehicle count per hour"
        className={styles.trafficVolumeSection}
      >
        <InfoRow
          left="Traffic Volume (24h)"
          right={
            <div className={styles.chartContainer}>
              <TrafficVolumeChart 
                data={trafficVolumeData}
                className={styles.volumeChart}
              />
              <div className={styles.chartStats}>
                <span className={styles.statItem}>
                  Peak: {peakHourVolume} vehicles/hour
                </span>
                <span className={styles.statItem}>
                  Total: {totalVehiclesProcessed} vehicles
                </span>
              </div>
            </div>
          }
          tooltip="Hourly traffic volume through this tollbooth"
          uppercase={true}
          disableFocus={true}
          className={styles.chartInfoRow}
        />
      </InfoSection>

      {/* Traffic Flow Efficiency Section */}
      <InfoSection 
        focusKey="traffic-flow-section"
        tooltip="Traffic flow efficiency showing how smoothly traffic moves"
        className={styles.trafficFlowSection}
      >
        <InfoRow
          left="Traffic Flow Efficiency"
          right={
            <div className={styles.chartContainer}>
              <TrafficFlowChart 
                data={trafficFlowData}
                className={styles.flowChart}
              />
              <div className={styles.chartStats}>
                <span className={styles.statItem}>
                  Average: {averageFlowEfficiency.toFixed(1)}%
                </span>
                <span className={styles.statItem}>
                  Status: {averageFlowEfficiency > 80 ? 'Excellent' : 
                          averageFlowEfficiency > 60 ? 'Good' : 
                          averageFlowEfficiency > 40 ? 'Fair' : 'Poor'}
                </span>
              </div>
            </div>
          }
          tooltip="Traffic flow efficiency as a percentage"
          uppercase={true}
          disableFocus={true}
          className={styles.chartInfoRow}
        />
      </InfoSection>

      {/* Controls Section */}
      <InfoSection 
        focusKey="traffic-controls-section"
        tooltip="Traffic analysis controls and settings"
        className={styles.controlsSection}
      >
        <InfoRow
          left="Time Range"
          right={
            <div className={styles.controlsContainer}>
              <button 
                className={`${styles.timeRangeButton} ${selectedTimeRange === '1h' ? styles.active : ''}`}
                onClick={() => handleTimeRangeChange('1h')}
              >
                1 Hour
              </button>
              <button 
                className={`${styles.timeRangeButton} ${selectedTimeRange === '6h' ? styles.active : ''}`}
                onClick={() => handleTimeRangeChange('6h')}
              >
                6 Hours
              </button>
              <button 
                className={`${styles.timeRangeButton} ${selectedTimeRange === '24h' ? styles.active : ''}`}
                onClick={() => handleTimeRangeChange('24h')}
              >
                24 Hours
              </button>
              <button 
                className={`${styles.timeRangeButton} ${selectedTimeRange === '7d' ? styles.active : ''}`}
                onClick={() => handleTimeRangeChange('7d')}
              >
                7 Days
              </button>
            </div>
          }
          tooltip="Select time range for traffic analysis"
          uppercase={true}
          disableFocus={true}
          className={styles.controlInfoRow}
        />

        <InfoRow
          left="View Mode"
          right={
            <div className={styles.controlsContainer}>
              <button 
                className={`${styles.viewModeButton} ${!showDetailedView ? styles.active : ''}`}
                onClick={() => handleToggleDetailedView()}
              >
                📊 Standard View
              </button>
              <button 
                className={`${styles.viewModeButton} ${showDetailedView ? styles.active : ''}`}
                onClick={() => handleToggleDetailedView()}
              >
                📈 Detailed View
              </button>
            </div>
          }
          tooltip="Toggle between standard and detailed traffic analysis"
          uppercase={true}
          disableFocus={true}
          className={styles.controlInfoRow}
        />
      </InfoSection>
    </div>
  );
};