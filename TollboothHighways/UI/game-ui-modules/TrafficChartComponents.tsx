import React, { useMemo, useRef, useEffect, useCallback } from 'react';

// CSS class utility function (equivalent to Zu() in the obfuscated code)
const classNames = (...classes: (string | undefined | null | boolean | { [key: string]: boolean })[]): string => {
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

// Default CSS theme classes (equivalent to s8 object)
export const TrafficChartTheme = {
    chartFontColor: "rgba(255, 255, 255, 0.6)",
    chartLineColor: "rgba(255, 255, 255, 0.1)",
    trafficVolumeBorderColor: "rgba(29, 130, 184, 1)",
    trafficVolumeBackgroundColor: "rgba(29, 130, 184, 0.5)",
    trafficFlowBorderColor: "rgba(30, 179, 184, 1)",
    trafficFlowBackgroundColor: "rgba(30, 179, 184, 0.5)",
    trafficChart: "traffic-chart_r8c",
};

// Chart configuration constants
const CHART_FONT_SIZE = 10;
const CHART_PADDING = 10;

// Unit types for chart formatting (equivalent to vc enum)
export enum ChartUnit {
    Integer = 'integer',
    IntegerPerHour = 'integerPerHour',
    Percentage = 'percentage',
    FloatSingleFraction = 'floatSingleFraction',
    Money = 'money',
}

// Component interfaces
export interface TrafficChartProps {
    data: number[];
    className?: string;
}

export interface ChartData {
    labels: string[];
    datasets: Array<{
        label: string;
        data: number[];
        type?: string;
        borderColor?: string;
        backgroundColor?: string;
        borderWidth?: number;
        fill?: boolean;
    }>;
}

export interface ChartOptions {
    responsive?: boolean;
    maintainAspectRatio?: boolean;
    devicePixelRatio?: number;
    onResize?: (chart: any) => void;
    elements?: any;
    scales?: any;
    plugins?: any;
}

// Chart component (equivalent to nY function)
const Chart: React.FC<{
    type: 'line';
    data: ChartData;
    options: ChartOptions;
    className?: string;
    mergeCallback?: (oldData: any[], newData: any[]) => any[];
}> = ({ type, data, options, className, mergeCallback = defaultMergeCallback, ...props }) => {
    const canvasRef = useRef<HTMLCanvasElement>(null);
    const chartRef = useRef<any>(null);
    const containerRef = useRef<HTMLDivElement>(null);

    // Mock chart size calculation
    const size = { x: 300, y: 150 };
    const hasDimensions = size.x > 0 && size.y > 0;
    const devicePixelRatio = 1; // Mock device pixel ratio

    const onResize = useCallback((chart: any) => {
        if (chart?.canvas?.style) {
            chart.canvas.style.width = (100 * Math.trunc(size.x)) / size.x + "%";
            chart.canvas.style.height = (100 * Math.trunc(size.y)) / size.y + "%";
        }
    }, [size]);

    const mergedOptions = useMemo(() => ({
        ...options,
        responsive: false,
        maintainAspectRatio: false,
        devicePixelRatio: devicePixelRatio,
        onResize: onResize,
    }), [devicePixelRatio, onResize, options]);

    useEffect(() => {
        if (hasDimensions) {
            if (chartRef.current &&
                (chartRef.current.canvas !== canvasRef.current ||
                    chartRef.current.config.type !== type)) {
                chartRef.current.destroy();
                chartRef.current = null;
            }

            if (!chartRef.current) {
                if (!canvasRef.current) return;

                const chartData = { datasets: [] };
                syncChartData(chartData, data, mergeCallback);

                // Mock chart creation - in real implementation this would be Chart.js
                chartRef.current = {
                    canvas: canvasRef.current,
                    config: { type, data: chartData, options: mergedOptions },
                    data: chartData,
                    options: mergedOptions,
                    update: () => { },
                    resize: () => { },
                    destroy: () => { }
                };
            } else {
                syncChartData(chartRef.current.data, data, mergeCallback);
                chartRef.current.options = mergedOptions;
                chartRef.current.update();
            }
        }
    }, [hasDimensions, type, data, mergedOptions, mergeCallback]);

    useEffect(() => {
        if (chartRef.current && hasDimensions) {
            chartRef.current.resize(size.x / devicePixelRatio, size.y / devicePixelRatio);
            chartRef.current.update("none");
            onResize(chartRef.current);
        }
    }, [hasDimensions, size, devicePixelRatio, onResize]);

    useEffect(() => {
        return () => {
            if (chartRef.current) {
                chartRef.current.destroy();
            }
        };
    }, []);

    const canvasStyle = useMemo(() => ({
        width: "388px",
        height: "182px",
        display: "flex"
    }), []);

    return React.createElement("div", {
        ref: containerRef,
        className: className,
        ...props
    }, React.createElement("canvas", {
        ref: canvasRef,
        style: canvasStyle
    }));
};

// Chart data synchronization function (equivalent to sY function)
function syncChartData(target: any, source: ChartData, mergeCallback: (oldData: any[], newData: any[]) => any[]) {
    if (source !== target) {
        Object.assign(target, {
            ...source,
            datasets: source.datasets.map((sourceDataset) => {
                const existingDataset = target.datasets.find(
                    (targetDataset: any) =>
                        targetDataset.label === sourceDataset.label &&
                        targetDataset.type === sourceDataset.type
                );

                if (existingDataset) {
                    if (sourceDataset !== existingDataset) {
                        const mergedData = mergeCallback(existingDataset.data, sourceDataset.data);
                        Object.assign(existingDataset, { ...sourceDataset, mergedData: mergedData });
                    }
                    return existingDataset;
                }

                return { ...sourceDataset, data: [...sourceDataset.data] };
            })
        });
    }
}

// Default merge callback (equivalent to iY function)
function defaultMergeCallback(oldData: any[], newData: any[]): any[] {
    const result = new Array(newData.length);
    return Object.assign(result, oldData, newData);
}

// Mock localization hook
const useLocalization = () => {
    return {
        translate: (key: string) => key
    };
};

// Time formatter hook (equivalent to Ou function)
const useTimeFormatter = () => {
    const { translate } = useLocalization();

    return ({ hour, minute }: { hour: number; minute: number }) => {
        return `${hour.toString().padStart(2, '0')}:${minute.toString().padStart(2, '0')}`;
    };
};

// Number formatter hook (equivalent to Ac function)
const useNumberFormatter = (unit: ChartUnit) => {
    const { translate } = useLocalization();

    return (value: number) => {
        switch (unit) {
            case ChartUnit.IntegerPerHour:
                return `${Math.round(value)}/h`;
            case ChartUnit.Percentage:
                return `${Math.round(value)}%`;
            case ChartUnit.Money:
                return `$${value.toFixed(2)}`;
            case ChartUnit.FloatSingleFraction:
                return value.toFixed(1);
            case ChartUnit.Integer:
            default:
                return Math.round(value).toString();
        }
    };
};

// UI Scale hook (equivalent to sl function)
const useUIScale = () => {
    return 1; // Mock UI scale
};

// Chart data preparation function (equivalent to u8 function)
function prepareChartData(data: number[]): ChartData {
    const timeFormatter = useTimeFormatter();

    return useMemo(() => ({
        labels: [
            timeFormatter({ hour: 0, minute: 0 }),   // 00:00
            timeFormatter({ hour: 6, minute: 0 }),   // 06:00
            timeFormatter({ hour: 12, minute: 0 }),  // 12:00
            timeFormatter({ hour: 18, minute: 0 }),  // 18:00
            timeFormatter({ hour: 0, minute: 0 }),   // 00:00 (next day)
        ],
        datasets: [{ label: 'data', data: data }],
    }), [data, timeFormatter]);
}

// Chart options creation function (equivalent to c8 function)
function createChartOptions(
    unit: ChartUnit,
    suggestedMax: number,
    borderColor: string,
    backgroundColor: string
): ChartOptions {
    const numberFormatter = useNumberFormatter(unit);
    const uiScale = useUIScale();

    return useMemo(() => ({
        responsive: false,
        maintainAspectRatio: false,
        elements: {
            line: {
                borderColor: borderColor,
                backgroundColor: backgroundColor,
                borderWidth: 2,
                fill: true,
            },
            point: {
                borderColor: borderColor,
                backgroundColor: borderColor,
                radius: 2,
                hoverRadius: 2,
                borderWidth: 2,
                hoverBorderWidth: 2,
            },
        },
        scales: {
            x: {
                beginAtZero: true,
                grid: {
                    lineWidth: 2,
                    color: TrafficChartTheme.chartLineColor,
                },
                ticks: {
                    font: {
                        size: CHART_FONT_SIZE * uiScale,
                        weight: 'bold',
                    },
                    color: TrafficChartTheme.chartFontColor,
                    padding: CHART_PADDING,
                    autoSkip: false,
                },
            },
            y: {
                min: 0,
                suggestedMax: suggestedMax,
                grid: {
                    lineWidth: 2,
                    color: TrafficChartTheme.chartLineColor,
                },
                ticks: {
                    font: {
                        size: CHART_FONT_SIZE * uiScale,
                        weight: 'bold',
                    },
                    color: TrafficChartTheme.chartFontColor,
                    padding: CHART_PADDING,
                    autoSkip: false,
                    callback: function (value: any) {
                        return numberFormatter(value);
                    },
                    maxTicksLimit: 6,
                },
            },
        },
        plugins: {
            legend: {
                display: false,
            },
            tooltip: {
                backgroundColor: 'rgba(0, 0, 0, 0.8)',
                titleColor: TrafficChartTheme.chartFontColor,
                bodyColor: TrafficChartTheme.chartFontColor,
                borderColor: borderColor,
                borderWidth: 1,
                callbacks: {
                    label: function (context: any) {
                        return `${context.label}: ${numberFormatter(context.parsed.y)}`;
                    },
                },
            },
        },
    }), [backgroundColor, borderColor, numberFormatter, suggestedMax, uiScale]);
}

// Traffic Volume Chart component (equivalent to a8 function)
export const TrafficVolumeChart: React.FC<TrafficChartProps> = ({
    data,
    className
}) => {
    const chartData = prepareChartData(data);
    const chartOptions = createChartOptions(
        ChartUnit.IntegerPerHour,
        5,
        TrafficChartTheme.trafficVolumeBorderColor,
        TrafficChartTheme.trafficVolumeBackgroundColor
    );

    return React.createElement(Chart, {
        type: "line",
        data: chartData,
        options: chartOptions,
        className: classNames(TrafficChartTheme.trafficChart, className),
    });
};

// Traffic Flow Chart component (equivalent to o8 function)
export const TrafficFlowChart: React.FC<TrafficChartProps> = ({
    data,
    className
}) => {
    const chartData = prepareChartData(data);
    const chartOptions = createChartOptions(
        ChartUnit.Percentage,
        100,
        TrafficChartTheme.trafficFlowBorderColor,
        TrafficChartTheme.trafficFlowBackgroundColor
    );

    return React.createElement(Chart, {
        type: "line",
        data: chartData,
        options: chartOptions,
        className: classNames(TrafficChartTheme.trafficChart, className),
    });
};

// Export utility functions and components
export { classNames, prepareChartData, createChartOptions, Chart };