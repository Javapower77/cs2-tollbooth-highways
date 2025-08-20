import React, { useMemo } from 'react';

// CSS for the table styling
export const VehicleTableTheme = {
    table: "vehicle-statistics-table",
    tableHeader: "table-header",
    tableRow: "table-row",
    tableCell: "table-cell",
    tableCellIcon: "table-cell-icon",
    tableCellText: "table-cell-text",
    tableCellNumber: "table-cell-number",
    headerCell: "header-cell",
    evenRow: "even-row",
    oddRow: "odd-row"
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
}

// Create table with proper styling for CS2
export const VehicleStatisticsTable: React.FC<VehicleStatisticsTableProps> = ({
    statistics,
    className
}) => {
    const tableStyle: React.CSSProperties = {
        width: '100%',
        borderCollapse: 'collapse',
        backgroundColor: 'rgba(0, 0, 0, 0.2)',
        border: '1px solid rgba(255, 255, 255, 0.1)',
        borderRadius: '4px',
        overflow: 'hidden'
    };

    const headerStyle: React.CSSProperties = {
        backgroundColor: 'rgba(0, 0, 0, 0.4)',
        borderBottom: '2px solid rgba(255, 255, 255, 0.2)',
        padding: '8px 4px',
        textAlign: 'left',
        fontSize: '12px',
        fontWeight: 'bold',
        color: '#ffffff',
        textTransform: 'uppercase'
    };

    const cellStyle: React.CSSProperties = {
        padding: '6px 4px',
        borderBottom: '1px solid rgba(255, 255, 255, 0.1)',
        fontSize: '11px',
        color: '#ffffff'
    };

    const iconCellStyle: React.CSSProperties = {
        ...cellStyle,
        width: '32px',
        textAlign: 'center'
    };

    const nameCellStyle: React.CSSProperties = {
        ...cellStyle,
        width: '40%',
        paddingLeft: '8px'
    };

    const numberCellStyle: React.CSSProperties = {
        ...cellStyle,
        width: '20%',
        textAlign: 'right',
        paddingRight: '8px'
    };

    const formatCurrency = (amount: number): string => {
        return `$${amount.toFixed(2)}`;
    };

    const formatQuantity = (quantity: number): string => {
        return quantity.toLocaleString();
    };

    return React.createElement("div", {
        className: className,
        style: { margin: '8px 0' }
    }, React.createElement("table", {
        style: tableStyle
    }, [
        // Table Header
        React.createElement("thead", { key: "header" }, 
            React.createElement("tr", {}, [
                React.createElement("th", {
                    key: "icon-header",
                    style: { ...headerStyle, width: '32px', textAlign: 'center' }
                }, ""),
                React.createElement("th", {
                    key: "name-header", 
                    style: { ...headerStyle, width: '40%' }
                }, "Vehicle Type"),
                React.createElement("th", {
                    key: "price-header",
                    style: { ...headerStyle, width: '20%', textAlign: 'right' }
                }, "Toll Price"),
                React.createElement("th", {
                    key: "quantity-header",
                    style: { ...headerStyle, width: '20%', textAlign: 'right' }
                }, "Quantity"),
                React.createElement("th", {
                    key: "earnings-header",
                    style: { ...headerStyle, width: '20%', textAlign: 'right' }
                }, "Total Earned")
            ])
        ),
        // Table Body
        React.createElement("tbody", { key: "body" },
            statistics.map((stat, index) => 
                React.createElement("tr", {
                    key: stat.vehicleType,
                    style: {
                        backgroundColor: index % 2 === 0 ? 'rgba(255, 255, 255, 0.02)' : 'rgba(255, 255, 255, 0.05)'
                    }
                }, [
                    // Icon cell
                    React.createElement("td", {
                        key: "icon",
                        style: iconCellStyle
                    }, React.createElement("img", {
                        src: stat.icon,
                        style: { width: '20px', height: '20px' },
                        alt: stat.name
                    })),
                    // Name cell
                    React.createElement("td", {
                        key: "name",
                        style: nameCellStyle
                    }, stat.name),
                    // Price cell
                    React.createElement("td", {
                        key: "price",
                        style: numberCellStyle
                    }, formatCurrency(stat.tollPrice)),
                    // Quantity cell
                    React.createElement("td", {
                        key: "quantity",
                        style: numberCellStyle
                    }, formatQuantity(stat.quantity)),
                    // Earnings cell
                    React.createElement("td", {
                        key: "earnings",
                        style: numberCellStyle
                    }, formatCurrency(stat.accumulativeEarnings))
                ])
            )
        )
    ]));
};

export default VehicleStatisticsTable;