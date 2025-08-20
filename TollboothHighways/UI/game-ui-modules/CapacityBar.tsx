import React, { useMemo } from 'react';

// CSS class names for the capacity bar
const capacityBarClasses = {
  "capacity-bar": "capacity-bar_uEN",
  capacityBar: "capacity-bar_uEN",
  "progress-bar": "progress-bar_AtB",
  progressBar: "progress-bar_AtB",
  excellent: "excellent_cFG",
  good: "good_f9U",
  medium: "medium_P2l",
  bad: "bad_BRS",
  critical: "critical_cqP",
  "progress-bounds": "progress-bounds_D6g",
  progressBounds: "progress-bounds_D6g",
  progress: "progress_EvF",
  label: "label_y0j",
  "progress-label": "progress-label_DqS",
  progressLabel: "progress-label_DqS",
};

// Default theme for progress bar
const defaultProgressBarTheme = {
  progressBar: "progress-bar_d6t progress-bar_xWR",
  progress: "progress_egi progress_JAQ",
};

// Utility function to clamp values between 0 and 1
const clamp = (value: number, min: number, max: number): number => {
  return Math.min(Math.max(value, min), max);
};

// Utility function to combine class names
const classNames = (...classes: (string | undefined | null | false)[]): string => {
  return classes.filter(Boolean).join(' ');
};

interface ProgressBarProps {
  progress: number;
  max: number;
  theme?: Record<string, string>;
  className?: string;
  style?: React.CSSProperties;
  children?: React.ReactNode;
  [key: string]: any;
}

// Progress Bar component
const ProgressBar: React.FC<ProgressBarProps> = ({
  progress,
  max,
  theme = defaultProgressBarTheme,
  className,
  style,
  children,
  ...props
}) => {
  const mergedTheme = useMemo(() => ({ ...defaultProgressBarTheme, ...theme }), [theme]);
  const progressPercentage = max > 0 ? 100 * clamp(progress / max, 0, 1) : 0;

  return (
    <div
      {...props}
      className={classNames(mergedTheme.progressBar, className)}
      style={style}
    >
      {capacityBarClasses.progressLabel && children != null && (
              <div className={capacityBarClasses.label}>{children}</div>
      )}
      <div
              className={capacityBarClasses.progressBounds}
        style={{ width: `${progressPercentage}%` }}
      >
        <div
          className={mergedTheme.progress}
          style={{ width: '100%' }}
        >
          {capacityBarClasses.progressLabel && children != null && (
            <div className={capacityBarClasses.progressLabel}>{children}</div>
          )}
        </div>
      </div>
      {!capacityBarClasses.progressLabel && children != null && (
              <div className={capacityBarClasses.label}>{children}</div>
      )}
    </div>
  );
};

interface CapacityBarProps {
  progress: number;
  max: number;
  plain?: boolean;
  invertColorCodes?: boolean;
  children?: React.ReactNode;
  className?: string;
}

// Function to determine color class based on percentage
const getColorClass = (percentage: number, inverted: boolean): string => {
  if (percentage > 90) {
    return inverted ? capacityBarClasses.excellent : capacityBarClasses.critical;
  } else if (percentage > 80) {
    return inverted ? capacityBarClasses.good : capacityBarClasses.bad;
  } else if (percentage > 50) {
    return capacityBarClasses.medium;
  } else if (percentage > 20) {
    return inverted ? capacityBarClasses.bad : capacityBarClasses.good;
  } else {
    return inverted ? capacityBarClasses.critical : capacityBarClasses.excellent;
  }
};

// Capacity Bar component
export const CapacityBar: React.FC<CapacityBarProps> = ({
  progress,
  max,
  plain = false,
  invertColorCodes = false,
  children,
  className,
}) => {
  const progressPercentage = max > 0 ? 100 * clamp(progress / max, 0, 1) : 0;

  return (
    <div className={classNames(capacityBarClasses.capacityBar, className)}>
      <ProgressBar
        progress={progress}
        max={max}
        theme={capacityBarClasses}
        className={classNames(!plain && getColorClass(progressPercentage, invertColorCodes))}
      >
        {children}
      </ProgressBar>
    </div>
  );
};

export default CapacityBar;