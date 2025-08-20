import React, { useMemo } from 'react';

// CSS class utility function (equivalent to cs() in the obfuscated code)
const cs = (...classes: (string | undefined | null | boolean | { [key: string]: boolean })[]): string => {
    return classes
        .filter((cls) => cls)
        .map((cls) => {
            if (typeof cls === 'string') return cls;
            if (typeof cls === 'object' && cls !== null) {
                return Object.entries(cls)
                    .filter(([, value]) => value)
                    .map(([key]) => key)
                    .join(' ');
            }
            return '';
        })
        .join(' ')
        .trim();
};

// Default theme interface for progress bar styling
export interface ProgressBarTheme {
    progressBar: string;
    progressBounds: string;
    progress: string;
    label: string;
    progressLabel: string;
}

// Default theme object (equivalent to obfuscated 'Sse' and 'Ese')
const defaultProgressBarTheme: ProgressBarTheme = {
    progressBar: 'progress-bar_xWR',
    progressBounds: 'progress-bounds_Fjq',
    progress: 'progress_JAQ',
    label: 'label_nBK',
    progressLabel: 'progress-label'
};

// Props interface for the ProgressBar component
export interface ProgressBarProps extends React.HTMLAttributes<HTMLDivElement> {
    /** Current progress value */
    progress: number;
    /** Maximum progress value */
    max: number;
    /** Custom theme object to override default styles */
    theme?: Partial<ProgressBarTheme>;
    /** Additional CSS class name */
    className?: string;
    /** Inline styles */
    style?: React.CSSProperties;
    /** Children content (typically progress text or percentage) */
    children?: React.ReactNode;
}

/**
 * Progress Bar Component for Cities: Skylines 2 Mods
 * 
 * A clean, deobfuscated implementation of the progress bar control
 * that can be used in info panels and other UI components.
 * 
 * @param props - ProgressBarProps
 * @returns JSX.Element
 */
export const ProgressBar: React.FC<ProgressBarProps> = ({
    progress,
    max,
    theme = defaultProgressBarTheme,
    className,
    style,
    children,
    ...otherProps
}) => {
    // Merge default theme with provided theme
    const mergedTheme = useMemo(() => ({
        ...defaultProgressBarTheme,
        ...theme
    }), [theme]);

    // Calculate progress percentage, clamped between 0 and 100
    const progressPercentage = max > 0 ? Math.max(0, Math.min(100, (progress / max) * 100)) : 0;

    // Calculate inner progress width (handles the visual fill effect)
    const innerProgressWidth = progressPercentage > 0 ? (100 / progressPercentage) * 100 : 0;

    return (
        <div
            {...otherProps}
            className={cs(mergedTheme.progressBar, className)}
            style={style}
        >
            {/* Progress label above the bar (if children and theme allows) */}
            {mergedTheme.progressLabel && children != null && (
                <div className={mergedTheme.label}>
                    {children}
                </div>
            )}
            
            {/* Progress bar container */}
            <div 
                className={mergedTheme.progressBounds}
                style={{ width: `${progressPercentage}%` }}
            >
                {/* Progress fill */}
                <div 
                    className={mergedTheme.progress}
                    style={{ width: `${innerProgressWidth}%` }}
                >
                    {/* Progress label inside the bar */}
                    {mergedTheme.progressLabel && children != null && (
                        <div className={mergedTheme.progressLabel}>
                            {children}
                        </div>
                    )}
                </div>
            </div>
            
            {/* Progress label below the bar (if no progressLabel in theme) */}
            {!mergedTheme.progressLabel && children != null && (
                <div className={mergedTheme.label}>
                    {children}
                </div>
            )}
        </div>
    );
};

/**
 * Utility function to format progress as percentage
 * @param current - Current progress value
 * @param max - Maximum progress value
 * @returns Formatted percentage string
 */
export const formatProgressPercentage = (current: number, max: number): string => {
    if (max <= 0) return '0%';
    const percentage = Math.round((current / max) * 100);
    return `${percentage}%`;
};

/**
 * Utility function to format progress as fraction
 * @param current - Current progress value
 * @param max - Maximum progress value
 * @returns Formatted fraction string
 */
export const formatProgressFraction = (current: number, max: number): string => {
    return `${current}/${max}`;
};

/**
 * Hook for progress bar state management
 * @param initialProgress - Initial progress value
 * @param maxValue - Maximum progress value
 * @returns Object with progress state and update functions
 */
export const useProgressBar = (initialProgress: number = 0, maxValue: number = 100) => {
    const [progress, setProgress] = React.useState(initialProgress);
    
    const updateProgress = React.useCallback((newProgress: number) => {
        setProgress(Math.max(0, Math.min(maxValue, newProgress)));
    }, [maxValue]);
    
    const incrementProgress = React.useCallback((amount: number = 1) => {
        setProgress(prev => Math.max(0, Math.min(maxValue, prev + amount)));
    }, [maxValue]);
    
    const resetProgress = React.useCallback(() => {
        setProgress(0);
    }, []);
    
    const completeProgress = React.useCallback(() => {
        setProgress(maxValue);
    }, [maxValue]);
    
    const percentage = maxValue > 0 ? (progress / maxValue) * 100 : 0;
    const isComplete = progress >= maxValue;
    
    return {
        progress,
        percentage,
        isComplete,
        setProgress: updateProgress,
        incrementProgress,
        resetProgress,
        completeProgress
    };
};

export default ProgressBar;