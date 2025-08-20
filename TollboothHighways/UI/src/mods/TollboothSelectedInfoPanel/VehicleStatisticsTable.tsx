import React, { useMemo } from 'react';
import { useFormattedLargeNumber } from "cs2/utils";
import { LocalizedNumber, Unit } from "cs2/l10n";

// CSS for the table styling using flexbox
export const VehicleTableTheme = {
    container: "vehicle-statistics-container",
    header: "vehicle-header",
    headerList: "vehicle-header-list",
    headerItem: "vehicle-header-item",
    body: "vehicle-body",
    bodyList: "vehicle-body-list",
    row: "vehicle-row",
    rowItem: "vehicle-row-item",
    cellIcon: "vehicle-cell-icon",
    cellText: "vehicle-cell-text",
    cellNumber: "vehicle-cell-number",
    evenRow: "vehicle-even-row",
    oddRow: "vehicle-odd-row"
};

// Vehicle data interface matching the C# TollBoothInsight component
export interface VehicleStatistic {
    vehicleType: string;
    icon: string;
    name: string;
    tollPrice: number;
    quantity: number;
    accumulativeEarnings: number;
}

export interface VehicleStatisticsTableProps {
    statistics: VehicleStatistic[];
    className?: string;
    headers?: string[];
    showColumns?: ColumnConfig[];
}

// Column configuration interface
export interface ColumnConfig {
    key: keyof VehicleStatistic | 'icon';
    header: string;
    flex: string; // Using flex instead of width for better responsive design
    align: 'left' | 'center' | 'right';
    type: 'icon' | 'text' | 'currency' | 'number';
}

// Create table-like component using lists and flexbox
export const VehicleStatisticsTable: React.FC<VehicleStatisticsTableProps> = ({
    statistics,
    className,
    headers,
    showColumns
}) => {
    // Default column configuration using flex values
    const defaultColumns: ColumnConfig[] = useMemo(() => [
        { key: 'icon', header: '', flex: '0 0 32px', align: 'center', type: 'icon' },
        { key: 'name', header: 'Vehicle Type', flex: '2 1 80px', align: 'left', type: 'text' },
        { key: 'tollPrice', header: 'Toll Price', flex: '1 0 80px', align: 'right', type: 'currency' },
        { key: 'quantity', header: 'Quantity', flex: '1 0 60px', align: 'right', type: 'number' },
        { key: 'accumulativeEarnings', header: 'Total Earned', flex: '1 0 80px', align: 'right', type: 'currency' }
    ], []);

    // Use provided columns or default
    const columns = useMemo(() => showColumns || defaultColumns, [showColumns, defaultColumns]);

    // Use provided headers or derive from columns
    const tableHeaders = useMemo(() =>
        headers || columns.map(col => col.header),
        [headers, columns]
    );

    // Container styles
    const containerStyle: React.CSSProperties = {
        margin: '8px 0',
        marginRight: '11px',
        overflow: 'hidden',
        borderRadius: '4px',
        border: '1px solid rgba(255, 255, 255, 0.1)',
        backgroundColor: 'rgba(0, 0, 0, 0.2)',
        fontFamily: 'inherit'
    };

    // Header container styles
    const headerContainerStyle: React.CSSProperties = {
        backgroundColor: 'rgba(0, 0, 0, 0.4)',
        borderBottom: '2px solid rgba(255, 255, 255, 0.2)'
    };

    // Header list styles
    const headerListStyle: React.CSSProperties = {
        margin: 0,
        padding: 0,
        listStyle: 'none',
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        minHeight: '32px'
    };

    // Body container styles
    const bodyContainerStyle: React.CSSProperties = {
        backgroundColor: 'transparent'
    };

    // Body list styles (ordered list for semantic rows)
    const bodyListStyle: React.CSSProperties = {
        margin: 0,
        padding: 0,
        listStyle: 'none',
        display: 'flex',
        flexDirection: 'column'
    };

    // Row styles (list items)
    const getRowStyle = (index: number): React.CSSProperties => ({
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        minHeight: '28px',
        borderBottom: '1px solid rgba(255, 255, 255, 0.1)',
        backgroundColor: index % 2 === 0 ? 'rgba(255, 255, 255, 0.02)' : 'rgba(255, 255, 255, 0.05)',
        transition: 'background-color 0.2s ease',
        fontFamily: 'Overpass, sans-serif'
    });

    // Header item styles
    const getHeaderItemStyle = (column: ColumnConfig): React.CSSProperties => ({
        flex: column.flex,
        padding: '8px 4px',
        fontSize: '10px',
        fontWeight: 'bold',
        color: '#ffffff',
        textTransform: 'uppercase',
        textAlign: column.align,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        display: 'flex',
        alignItems: 'center',
        justifyContent: getJustifyContent(column.align),
        paddingLeft: column.align === 'left' ? '8px' : '4px',
        paddingRight: column.align === 'right' ? '8px' : '4px'
    });

    // Cell item styles
    const getCellItemStyle = (column: ColumnConfig): React.CSSProperties => ({
        flex: column.flex,
        padding: '6px 4px',
        fontSize: '11px',
        color: '#ffffff',
        textAlign: column.align,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        display: 'flex',
        alignItems: 'center',
        justifyContent: getJustifyContent(column.align),
        paddingLeft: column.align === 'left' ? '8px' : '4px',
        paddingRight: column.align === 'right' ? '8px' : '4px'
    });

    // Helper function to get justify-content value
    const getJustifyContent = (align: string): string => {
        switch (align) {
            case 'center': return 'center';
            case 'right': return 'flex-end';
            case 'left':
            default: return 'flex-start';
        }
    };

    // Formatting functions
    const formatters = {
        currency: (amount: number): string => `$${amount.toFixed(2)}`,
        number: (quantity: number): string => useFormattedLargeNumber(quantity),
        text: (text: string): string => text,
        icon: (iconUrl: string, name: string): React.ReactElement =>
            React.createElement("img", {
                src: iconUrl,
                style: {
                    width: '20px',
                    height: '20px',
                    display: 'block',
                    objectFit: 'contain'
                },
                alt: name
            })
    };

    // Get cell content based on column configuration
    const getCellContent = (stat: VehicleStatistic, column: ColumnConfig): React.ReactNode => {
        if (column.type === 'icon') {
            return formatters.icon(stat.icon, stat.name);
        }

        const value = stat[column.key as keyof VehicleStatistic];

        switch (column.type) {
            case 'currency':
                //return formatters.currency(value as number);
                return (<LocalizedNumber unit={Unit.Money} value={value as number}></LocalizedNumber>);
            case 'number':
                //return formatters.number(value as number);
                return (<LocalizedNumber unit={Unit.Integer} value={value as number}></LocalizedNumber>);
            case 'text':
            default:
                return formatters.text(value as string);
        }
    };

    // If no statistics, show empty state
    if (!statistics || statistics.length === 0) {
        return React.createElement("div", {
            className: className,
            style: {
                ...containerStyle,
                padding: '16px',
                textAlign: 'center',
                color: '#ffffff',
                fontSize: '10px',
                fontStyle: 'italic'
            }
        }, "No vehicle data available");
    }

    return React.createElement("div", {
        className: className,
        style: containerStyle
    }, [
        // Header section with map/key dynamically -- Thanks krzychu124 for the suggestion!!!
        React.createElement("header", {
            key: "table-header",
            style: headerContainerStyle
        }, React.createElement("ul", {
            style: headerListStyle,
            role: "row"
        }, tableHeaders.map((header, index) =>
            React.createElement("li", {
                key: `header-${index}`,
                style: getHeaderItemStyle(columns[index]),
                role: "columnheader"
            }, header)
        ))),

        // Body section using ol/li/ul for rows and cells -- Thanks again krzychu124 for the suggestion!!!
        React.createElement("section", {
            key: "table-body",
            style: bodyContainerStyle
        }, React.createElement("ol", {
            style: bodyListStyle,
            role: "rowgroup"
        }, statistics.map((stat, rowIndex) =>
            React.createElement("li", {
                key: stat.vehicleType,
                style: getRowStyle(rowIndex),
                role: "row"
            }, React.createElement("ul", {
                style: {
                    margin: 0,
                    padding: 0,
                    listStyle: 'none',
                    display: 'flex',
                    flexDirection: 'row',
                    alignItems: 'center',
                    width: '100%'
                }
            }, columns.map((column, colIndex) =>
                React.createElement("li", {
                    key: `${stat.vehicleType}-${column.key}`,
                    style: getCellItemStyle(column),
                    role: "cell"
                }, getCellContent(stat, column))
            )))
        )))
    ]);
};

// Enhanced Flexbox Grid implementation for better performance
export const VehicleStatisticsFlexGrid: React.FC<VehicleStatisticsTableProps> = ({
    statistics,
    className,
    headers,
    showColumns
}) => {
    // Default column configuration using flex values
    const defaultColumns: ColumnConfig[] = useMemo(() => [
        { key: 'icon', header: '', flex: '0 0 32px', align: 'center', type: 'icon' },
        { key: 'name', header: 'Vehicle Type', flex: '2 1 auto', align: 'left', type: 'text' },
        { key: 'tollPrice', header: 'Toll Price', flex: '1 0 80px', align: 'right', type: 'currency' },
        { key: 'quantity', header: 'Quantity', flex: '1 0 80px', align: 'right', type: 'number' },
        { key: 'accumulativeEarnings', header: 'Total Earned', flex: '1 0 100px', align: 'right', type: 'currency' }
    ], []);

    const columns = useMemo(() => showColumns || defaultColumns, [showColumns, defaultColumns]);
    const tableHeaders = useMemo(() =>
        headers || columns.map(col => col.header),
        [headers, columns]
    );

    const containerStyle: React.CSSProperties = {
        margin: '8px 0',
        overflow: 'hidden',
        borderRadius: '4px',
        border: '1px solid rgba(255, 255, 255, 0.1)',
        backgroundColor: 'rgba(0, 0, 0, 0.2)',
        display: 'flex',
        flexDirection: 'column'
    };

    const headerRowStyle: React.CSSProperties = {
        display: 'flex',
        flexDirection: 'row',
        backgroundColor: 'rgba(0, 0, 0, 0.4)',
        borderBottom: '2px solid rgba(255, 255, 255, 0.2)',
        minHeight: '32px'
    };

    const dataRowStyle: React.CSSProperties = {
        display: 'flex',
        flexDirection: 'row',
        minHeight: '28px',
        borderBottom: '1px solid rgba(255, 255, 255, 0.1)'
    };

    const cellStyle: React.CSSProperties = {
        padding: '6px 4px',
        fontSize: '11px',
        color: '#ffffff',
        display: 'flex',
        alignItems: 'center',
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis'
    };

    const headerCellStyle: React.CSSProperties = {
        ...cellStyle,
        fontSize: '12px',
        fontWeight: 'bold',
        textTransform: 'uppercase',
        padding: '8px 4px'
    };

    const formatters = {
        currency: (amount: number): string => `$${amount.toFixed(2)}`,
        number: (quantity: number): string => quantity.toLocaleString(),
        text: (text: string): string => text,
        icon: (iconUrl: string, name: string): React.ReactElement =>
            React.createElement("img", {
                src: iconUrl,
                style: { width: '20px', height: '20px', objectFit: 'contain' },
                alt: name
            })
    };

    const getCellContent = (stat: VehicleStatistic, column: ColumnConfig): React.ReactNode => {
        if (column.type === 'icon') {
            return formatters.icon(stat.icon, stat.name);
        }

        const value = stat[column.key as keyof VehicleStatistic];

        switch (column.type) {
            case 'currency':
                return formatters.currency(value as number);
            case 'number':
                return formatters.number(value as number);
            case 'text':
            default:
                return formatters.text(value as string);
        }
    };

    const getJustifyContent = (align: string): string => {
        switch (align) {
            case 'center': return 'center';
            case 'right': return 'flex-end';
            case 'left':
            default: return 'flex-start';
        }
    };

    if (!statistics || statistics.length === 0) {
        return React.createElement("div", {
            className: className,
            style: {
                ...containerStyle,
                padding: '16px',
                justifyContent: 'center',
                alignItems: 'center',
                color: '#ffffff',
                fontSize: '12px',
                fontStyle: 'italic'
            }
        }, "No vehicle data available");
    }

    return React.createElement("div", {
        className: className,
        style: containerStyle
    }, [
        // Header row using unordered list
        React.createElement("ul", {
            key: "header-row",
            style: {
                ...headerRowStyle,
                margin: 0,
                padding: 0,
                listStyle: 'none'
            }
        }, tableHeaders.map((header, index) =>
            React.createElement("li", {
                key: `header-${index}`,
                style: {
                    ...headerCellStyle,
                    flex: columns[index].flex,
                    justifyContent: getJustifyContent(columns[index].align),
                    paddingLeft: columns[index].align === 'left' ? '8px' : '4px',
                    paddingRight: columns[index].align === 'right' ? '8px' : '4px'
                }
            }, header)
        )),

        // Data rows using ordered list
        React.createElement("ol", {
            key: "data-rows",
            style: {
                margin: 0,
                padding: 0,
                listStyle: 'none',
                display: 'flex',
                flexDirection: 'column'
            }
        }, statistics.map((stat, rowIndex) =>
            React.createElement("li", {
                key: stat.vehicleType,
                style: {
                    ...dataRowStyle,
                    backgroundColor: rowIndex % 2 === 0 ? 'rgba(255, 255, 255, 0.02)' : 'rgba(255, 255, 255, 0.05)'
                }
            }, React.createElement("ul", {
                style: {
                    margin: 0,
                    padding: 0,
                    listStyle: 'none',
                    display: 'flex',
                    flexDirection: 'row',
                    width: '100%'
                }
            }, columns.map((column, colIndex) =>
                React.createElement("li", {
                    key: `${stat.vehicleType}-${column.key}`,
                    style: {
                        ...cellStyle,
                        flex: column.flex,
                        justifyContent: getJustifyContent(column.align),
                        paddingLeft: column.align === 'left' ? '8px' : '4px',
                        paddingRight: column.align === 'right' ? '8px' : '4px'
                    }
                }, getCellContent(stat, column))
            )))
        ))
    ]);
};

// Utility function to create custom column configurations
export const createColumnConfig = (
    key: keyof VehicleStatistic | 'icon',
    header: string,
    flex: string, // Changed from width to flex
    align: 'left' | 'center' | 'right' = 'left',
    type: 'icon' | 'text' | 'currency' | 'number' = 'text'
): ColumnConfig => ({
    key,
    header,
    flex,
    align,
    type
});

export default VehicleStatisticsTable;