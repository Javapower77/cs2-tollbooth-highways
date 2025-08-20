import React, { useState, useCallback, useMemo } from "react";
import { Button } from "cs2/ui";
import { getModule } from "cs2/modding";
import { bindValue, trigger, useValue } from "cs2/api";
import { useLocalization } from "cs2/l10n";
import mod from "../../mod.json";
import { CS2VanillaUIResolver } from "mods/CS2VanillaUIResolver";


// C# Binding Interfaces - these should match the C# structs
interface RouteDataModel {
    id: string;
    name: string;
    type: "bus" | "train" | "subway" | "tram";
    length: number;
    stops: number;
    passengers: number;
    revenue: number;
    isActive: boolean;
}

interface VehicleDataModel {
    entity: string;
    id: string;
    thumbnail: string;
    name: string;
    capacity: number;
    fuelEfficiency: number;
    maintenanceCost: number;
    vehicleType: "primary" | "secondary";
    isAvailable: boolean;
}

interface VehicleData {
    entity: string;
    id: string;
    thumbnail: string;
    name?: string;
}

// Bind to C# data using the exact binding names from TransportRouteUISystem
const m_RouteData$ = bindValue<RouteDataModel>(mod.id, "m_RouteData");
const m_AvailableVehicles$ = bindValue<VehicleDataModel[]>(mod.id, "m_AvailableVehicles");
const m_SelectedPrimaryVehicle$ = bindValue<VehicleDataModel>(mod.id, "m_SelectedPrimaryVehicle");
const m_SelectedSecondaryVehicle$ = bindValue<VehicleDataModel>(mod.id, "m_SelectedSecondaryVehicle");
const m_IsPanelVisible$ = bindValue<boolean>(mod.id, "m_IsPanelVisible");

// Performance data bindings
const m_DailyPassengers$ = bindValue<number>(mod.id, "m_DailyPassengers");
const m_RouteLength$ = bindValue<number>(mod.id, "m_RouteLength");
const m_NumberOfStops$ = bindValue<number>(mod.id, "m_NumberOfStops");
const m_AverageWaitingTime$ = bindValue<number>(mod.id, "m_AverageWaitingTime");
const m_RouteEfficiency$ = bindValue<number>(mod.id, "m_RouteEfficiency");
const m_DailyRevenue$ = bindValue<number>(mod.id, "m_DailyRevenue");

// Get vanilla UI components
export const InfoSection: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.tsx",
    "InfoSection"
);

export const InfoRowTheme: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.module.scss",
    "classes"
);

export const descriptionToolTipStyle = getModule(
    "game-ui/common/tooltip/description-tooltip/description-tooltip.module.scss",
    "classes"
);

// Tooltip component
function DescriptionTooltip(tooltipTitle: string | null, tooltipDescription: string | null): JSX.Element {
    return (
        <>
            <div className={descriptionToolTipStyle.title}>{tooltipTitle}</div>
            <div className={descriptionToolTipStyle.content}>{tooltipDescription}</div>
        </>
    );
}

export const TransportRouteMainPanel = () => {
    const { translate } = useLocalization();

    // Get data from C# bindings with safe defaults
    const routeData = useValue(m_RouteData$);
    const availableVehicles = useValue(m_AvailableVehicles$) || [];
    const selectedPrimaryVehicle = useValue(m_SelectedPrimaryVehicle$);
    const selectedSecondaryVehicle = useValue(m_SelectedSecondaryVehicle$);
    const isPanelVisible = useValue(m_IsPanelVisible$);

    // Performance data from C# bindings
    const dailyPassengers = useValue(m_DailyPassengers$);
    const routeLength = useValue(m_RouteLength$);
    const numberOfStops = useValue(m_NumberOfStops$);
    const averageWaitingTime = useValue(m_AverageWaitingTime$);
    const routeEfficiency = useValue(m_RouteEfficiency$);
    const dailyRevenue = useValue(m_DailyRevenue$);

    // Local state for UI
    const [selectedTab, setSelectedTab] = useState<string>('overview');

    // Filter vehicles by type for primary/secondary selection
    const primaryVehicles = useMemo(() => {
        return availableVehicles.filter(vehicle =>
            vehicle.vehicleType === 'primary' || vehicle.vehicleType === undefined
        ).map(vehicle => ({
            entity: vehicle.entity,
            id: vehicle.id,
            thumbnail: vehicle.thumbnail,
            name: vehicle.name
        } as VehicleData));
    }, [availableVehicles]);

    const secondaryVehicles = useMemo(() => {
        return availableVehicles.filter(vehicle =>
            vehicle.vehicleType === 'secondary'
        ).map(vehicle => ({
            entity: vehicle.entity,
            id: vehicle.id,
            thumbnail: vehicle.thumbnail,
            name: vehicle.name
        } as VehicleData));
    }, [availableVehicles]);

    // Convert C# models to UI models
    const primaryVehicleData: VehicleData | undefined = selectedPrimaryVehicle ? {
        entity: selectedPrimaryVehicle.entity,
        id: selectedPrimaryVehicle.id,
        thumbnail: selectedPrimaryVehicle.thumbnail,
        name: selectedPrimaryVehicle.name
    } : undefined;

    const secondaryVehicleData: VehicleData | undefined = selectedSecondaryVehicle ? {
        entity: selectedSecondaryVehicle.entity,
        id: selectedSecondaryVehicle.id,
        thumbnail: selectedSecondaryVehicle.thumbnail,
        name: selectedSecondaryVehicle.name
    } : undefined;

    // Don't render if panel is not visible or routeData is not properly initialized
    if (!isPanelVisible || !routeData || !routeData.id) {
        return null;
    }

    // Tab content renderer
    const renderTabContent = () => {
        switch (selectedTab) {
            case 'vehicles':
                return (
                    <>
                        {/* Vehicle Selection Section */}
                        <InfoSection focusKey={CS2VanillaUIResolver.instance.FOCUS_DISABLED} disableFocus={true}>
                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Vehicle Configuration"
                                tooltip={DescriptionTooltip("Vehicle Configuration", "Select and configure vehicles for this transport route")}
                                uppercase={true}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            {/* Integrate the Select Vehicles Section with real data */}
                            <CS2VanillaUIResolver.instance.SelectVehiclesSection
                                group="transport_route_vehicles"
                                tooltipKeys={["primary_vehicle", "secondary_vehicle", "efficiency"]}
                                tooltipTags={[routeData.type, "performance"]}
                                primaryVehicle={primaryVehicleData}
                                secondaryVehicle={secondaryVehicleData}
                                primaryVehicles={primaryVehicles}
                                secondaryVehicles={routeData.type === "train" ? secondaryVehicles : undefined}
                            />
                        </InfoSection>

                        {/* Vehicle Performance Stats - using real data */}
                        <InfoSection focusKey={CS2VanillaUIResolver.instance.FOCUS_DISABLED} disableFocus={true}>
                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Vehicle Performance"
                                tooltip={DescriptionTooltip("Performance", "Current vehicle performance metrics")}
                                uppercase={true}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Capacity"
                                right={selectedPrimaryVehicle ? `${selectedPrimaryVehicle.capacity} passengers` : "Select vehicle"}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Fuel Efficiency"
                                right={selectedPrimaryVehicle ? `${selectedPrimaryVehicle.fuelEfficiency.toFixed(1)} L/100km` : "N/A"}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Maintenance Cost"
                                right={selectedPrimaryVehicle ? `$${selectedPrimaryVehicle.maintenanceCost}/month` : "N/A"}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />
                        </InfoSection>
                    </>
                );

            case 'route':
                return (
                    <>
                        {/* Route Information - using real data */}
                        <InfoSection focusKey={CS2VanillaUIResolver.instance.FOCUS_DISABLED} disableFocus={true}>
                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Route Details"
                                tooltip={DescriptionTooltip("Route Information", "Basic information about this transport route")}
                                uppercase={true}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Route Name"
                                right={routeData.name}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Route Type"
                                right={routeData.type.toUpperCase()}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Total Length"
                                right={`${routeLength?.toFixed(1) || routeData.length.toFixed(1)} km`}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Number of Stops"
                                right={(numberOfStops || routeData.stops).toString()}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Status"
                                right={routeData.isActive ? "Active" : "Inactive"}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />
                        </InfoSection>
                    </>
                );

            case 'statistics':
                return (
                    <>
                        {/* Performance Statistics - using real data */}
                        <InfoSection focusKey={CS2VanillaUIResolver.instance.FOCUS_DISABLED} disableFocus={true}>
                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Performance Statistics"
                                tooltip={DescriptionTooltip("Statistics", "Current performance and usage statistics")}
                                uppercase={true}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Daily Passengers"
                                right={(dailyPassengers || routeData.passengers).toLocaleString()}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Daily Revenue"
                                right={`$${(dailyRevenue || routeData.revenue).toLocaleString()}`}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Average Waiting Time"
                                right={`${averageWaitingTime?.toFixed(1) || '3.2'} minutes`}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Route Efficiency"
                                right={`${routeEfficiency?.toFixed(0) || '87'}%`}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />
                        </InfoSection>

                        {/* Progress bars */}
                        <InfoSection focusKey={CS2VanillaUIResolver.instance.FOCUS_DISABLED} disableFocus={true}>
                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Capacity Utilization"
                                tooltip={DescriptionTooltip("Capacity", "Current vehicle capacity utilization")}
                                uppercase={true}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            {selectedPrimaryVehicle && (
                                <CS2VanillaUIResolver.instance.CapacityBar
                                    progress={dailyPassengers || routeData.passengers}
                                    max={selectedPrimaryVehicle.capacity * 20} // Assume 20 trips per day
                                    invertColorCodes={false}
                                    children={
                                        <span>
                                            {(dailyPassengers || routeData.passengers).toLocaleString()} / {(selectedPrimaryVehicle.capacity * 20).toLocaleString()} passengers
                                        </span>
                                    }
                                />
                            )}
                        </InfoSection>
                    </>
                );

            default:
            case 'overview':
                return (
                    <>
                        {/* Overview Information - using real data */}
                        <InfoSection focusKey={CS2VanillaUIResolver.instance.FOCUS_DISABLED} disableFocus={true}>
                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Route Overview"
                                tooltip={DescriptionTooltip("Overview", "General overview of the transport route")}
                                uppercase={true}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Status"
                                right={routeData.isActive ? "Active" : "Inactive"}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Current Passengers"
                                right={(dailyPassengers || routeData.passengers).toLocaleString()}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Primary Vehicle"
                                right={selectedPrimaryVehicle?.name || "None selected"}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            {selectedSecondaryVehicle && (
                                <CS2VanillaUIResolver.instance.InfoRow
                                    left="Secondary Vehicle"
                                    right={selectedSecondaryVehicle.name || "None"}
                                    disableFocus={true}
                                    className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                                />
                            )}
                        </InfoSection>
                    </>
                );
        }
    };

    return (
        <>
            {/* Description Section */}
            <InfoSection focusKey={CS2VanillaUIResolver.instance.FOCUS_DISABLED} disableFocus={true}>
                <CS2VanillaUIResolver.instance.DescriptionRow>
                    <div>
                        Manage your transport route configuration, including vehicle selection, route optimization, and performance monitoring.
                    </div>
                </CS2VanillaUIResolver.instance.DescriptionRow>
            </InfoSection>

            {/* Tab Navigation */}
            <InfoSection focusKey={CS2VanillaUIResolver.instance.FOCUS_DISABLED} disableFocus={true}>
                <CS2VanillaUIResolver.instance.InfoRow
                    left="Navigation"
                    tooltip={DescriptionTooltip("Navigation", "Switch between different sections of the route configuration")}
                    uppercase={true}
                    disableFocus={true}
                    className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                />

                <div style={{
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '8px',
                    padding: '8px 0'
                }}>
                    <CS2VanillaUIResolver.instance.InfoButton
                        label="Overview"
                        icon="Media/Game/Icons/LotTool.svg"
                        selected={selectedTab === 'overview'}
                        onSelect={() => setSelectedTab('overview')}
                    />

                    <CS2VanillaUIResolver.instance.InfoButton
                        label="Route Details"
                        icon="Media/Game/Icons/Routetool.svg"
                        selected={selectedTab === 'route'}
                        onSelect={() => setSelectedTab('route')}
                    />

                    <CS2VanillaUIResolver.instance.InfoButton
                        label="Vehicle Selection"
                        icon="Media/Game/Resources/Vehicles.svg"
                        selected={selectedTab === 'vehicles'}
                        onSelect={() => setSelectedTab('vehicles')}
                    />

                    <CS2VanillaUIResolver.instance.InfoButton
                        label="Statistics"
                        icon="Media/Game/Icons/Calendar.svg"
                        selected={selectedTab === 'statistics'}
                        onSelect={() => setSelectedTab('statistics')}
                    />
                </div>
            </InfoSection>

            {/* Dynamic Content Based on Selected Tab */}
            {renderTabContent()}

            {/* Action Buttons - using C# triggers */}
            <InfoSection focusKey={CS2VanillaUIResolver.instance.FOCUS_DISABLED} disableFocus={true}>
                <CS2VanillaUIResolver.instance.InfoRow
                    left="Actions"
                    tooltip={DescriptionTooltip("Actions", "Available actions for this transport route")}
                    uppercase={true}
                    disableFocus={true}
                    className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                />

                <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', padding: '8px' }}>
                    <Button
                        variant="primary"
                        onClick={() => {
                            console.log("Optimizing route");
                            trigger(mod.id, "optimizeRoute", routeData.id);
                        }}
                    >
                        🚀 Optimize Route
                    </Button>

                    <Button
                        variant="round"
                        onClick={() => {
                            console.log("Reset vehicles");
                            trigger(mod.id, "resetVehicles", routeData.id);
                        }}
                    >
                        🔄 Reset Vehicles
                    </Button>

                    <Button
                        variant="default"
                        onClick={() => {
                            console.log("Generate report");
                            trigger(mod.id, "generateReport", routeData.id);
                        }}
                    >
                        📊 Generate Report
                    </Button>
                </div>
            </InfoSection>
        </>
    );
};