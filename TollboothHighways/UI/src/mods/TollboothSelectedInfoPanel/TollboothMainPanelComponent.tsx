import { getModule } from "cs2/modding";
import { Theme, Color } from "cs2/bindings";
import { bindValue, trigger, useValue } from "cs2/api"; 
import { LocalizedNumber, LocalizedNumber, useLocalization } from "cs2/l10n";
import mod from "../../../mod.json";
import { tool } from "cs2/bindings";
import { Button } from "cs2/ui";
import { Entity } from "cs2/utils";
import { FocusDisabled } from "cs2/input";
import { CS2VanillaUIResolver } from "mods/CS2VanillaUIResolver";
import { Unit } from "cs2/l10n";
import { useState, useMemo } from "react";
import { VehicleStatisticsTable, VehicleStatistic } from './VehicleStatisticsTable';

const uilJavapower = "coui://javapower-tollbooth-highways/";
const AdministrationVehicle01Src = uilJavapower + "CarPrefab.AdministrationVehicle01.png";
const Bus01Src = uilJavapower + "CarPrefab.Bus01.png";
const Bus02Src = uilJavapower + "CarPrefab.Bus02.png";
const Bus03Src = uilJavapower + "CarPrefab.Bus03.png";
const BusCO01Src = uilJavapower + "CarPrefab.BusCO01.png";
const BusCO02Src = uilJavapower + "CarPrefab.BusCO02.png";
const Car01Src = uilJavapower + "CarPrefab.Car01.png";
const Car02Src = uilJavapower + "CarPrefab.Car02.png";
const Car03Src = uilJavapower + "CarPrefab.Car03.png";
const Car04Src = uilJavapower + "CarPrefab.Car04.png";
const Car05Src = uilJavapower + "CarPrefab.Car05.png";
const Car06Src = uilJavapower + "CarPrefab.Car06.png";
const Car07Src = uilJavapower + "CarPrefab.Car07.png";
const Car08Src = uilJavapower + "CarPrefab.Car08.png";
const Car09Src = uilJavapower + "CarPrefab.Car09.png";
const CoalTruck01Src = uilJavapower + "CarPrefab.CoalTruck01.png";
const EU_Ambulance01Src = uilJavapower + "CarPrefab.EU_Ambulance01.png";
const EU_DeliveryVan01Src = uilJavapower + "CarPrefab.EU_DeliveryVan01.png";
const EU_FireTruck01Src = uilJavapower + "CarPrefab.EU_FireTruck01.png";
const EU_GarbageTruck01Src = uilJavapower + "CarPrefab.EU_GarbageTruck01.png";
const EU_PoliceVehicle01Src = uilJavapower + "CarPrefab.EU_PoliceVehicle01.png";
const EU_PoliceVehicle02Src = uilJavapower + "CarPrefab.EU_PoliceVehicle02.png";
const EU_PostVan01Src = uilJavapower + "CarPrefab.EU_PostVan01.png";
const EU_Snowplow01Src = uilJavapower + "CarPrefab.EU_Snowplow01.png";
const EU_TruckTractor01Src = uilJavapower + "CarPrefab.EU_TruckTractor01.png";
const ForestForwarder01Src = uilJavapower + "CarPrefab.ForestForwarder01.png";
const Hearse01Src = uilJavapower + "CarPrefab.Hearse01.png";
const Motorbike01Src = uilJavapower + "CarPrefab.Motorbike01.png";
const MotorbikeDelivery01 = uilJavapower + "CarPrefab.MotorbikeDelivery01.png";
const MuscleCar01Src = uilJavapower + "CarPrefab.MuscleCar01.png";
const MuscleCar02Src = uilJavapower + "CarPrefab.MuscleCar02.png";
const MuscleCar03Src = uilJavapower + "CarPrefab.MuscleCar03.png";
const MuscleCar04Src = uilJavapower + "CarPrefab.MuscleCar04.png";
const MuscleCar05Src = uilJavapower + "CarPrefab.MuscleCar05.png";
const NA_Ambulance01Src = uilJavapower + "CarPrefab.NA_Ambulance01.png";
const NA_DeliveryVan01Src = uilJavapower + "CarPrefab.NA_DeliveryVan01.png";
const NA_FireTruck01Src = uilJavapower + "CarPrefab.NA_FireTruck01.png";
const NA_GarbageTruck01Src = uilJavapower + "CarPrefab.NA_GarbageTruck01.png";
const NA_PoliceVehicle01Src = uilJavapower + "CarPrefab.NA_PoliceVehicle01.png";
const NA_PoliceVehicle02Src = uilJavapower + "CarPrefab.NA_PoliceVehicle02.png";
const NA_PostVan01Src = uilJavapower + "CarPrefab.NA_PostVan01.png";
const NA_Snowplow01Src = uilJavapower + "CarPrefab.NA_Snowplow01.png";
const NA_TruckTractor01Src = uilJavapower + "CarPrefab.NA_TruckTractor01.png";
const OilTruck01Src = uilJavapower + "CarPrefab.OilTruck01.png";
const OreMiningTruck01Src = uilJavapower + "CarPrefab.OreMiningTruck01.png";
const ParkMaintenanceVehicle01Src = uilJavapower + "CarPrefab.ParkMaintenanceVehicle01.png";
const PrisonVan01Src = uilJavapower + "CarPrefab.PrisonVan01.png";
const RoadMaintenanceVehicle01Src = uilJavapower + "CarPrefab.RoadMaintenanceVehicle01.png";
const Scooter01Src = uilJavapower + "CarPrefab.Scooter01.png";
const Taxi01Src = uilJavapower + "CarPrefab.Taxi01.png";
const Taxi02Src = uilJavapower + "CarPrefab.Taxi02.png";
const Tractor01Src = uilJavapower + "CarPrefab.Tractor01.png";
const Van01Src = uilJavapower + "CarPrefab.Van01.png";
const CarTrailer01Src = uilJavapower + "CarTrailerPrefab.CarTrailer01.png";
const TruckTrailer01Src = uilJavapower + "CarTrailerPrefab.TruckTrailer01.png";
const EconomySrc = uilJavapower + "Economy.svg";
const MoneySrc = uilJavapower + "Money.svg";
const RouteTicketPriceSrc = uilJavapower + "RouteTicketPrice.svg";
const PrivateTransportSrc = uilJavapower + "Traffic.svg";
const PublicTransportSrc = uilJavapower + "Bus.svg";
const FreightTrucksSrc = uilJavapower + "CargoTruck.svg";
const ServiceVehiclesSrc = uilJavapower + "ServiceVehicles.png";
const TollboothOverviewSrc = uilJavapower + "TollboothOverview.png";
const VehiclesGroupSrc = uilJavapower + "VehiclesGroup.png";

// Add these bindings to get data from your C# TollBoothInsight component
const m_TollBoothInsight$ = bindValue<any>(mod.id, "m_TollBoothInsight");

export const InfoRowTheme: Theme | any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.module.scss",
    "classes"
)

export const InfoSection: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.tsx",
    "InfoSection"
) 

export const descriptionToolTipStyle = getModule("game-ui/common/tooltip/description-tooltip/description-tooltip.module.scss", "classes");
export const roundButtonHighlightStyle = getModule("game-ui/common/input/button/themes/round-highlight-button.module.scss", "classes");

// Tooltip component
export function DescriptionTooltip(tooltipTitle: string | null, tooltipDescription: string | null): JSX.Element {
    return (
        <>
            <div className={descriptionToolTipStyle.title}>{tooltipTitle}</div>
            <div className={descriptionToolTipStyle.content}>{tooltipDescription}</div>
        </>
    );
}

// Enum definitions matching C# enums
enum VehicleGroup {
    PrivateTransport = 0,
    Trucks = 1,
    PublicTransport = 2,
    ServiceVehicles = 3
}

// Vehicle type to group mapping (matching the C# vehicleTypeToGroupMap)
const vehicleTypeToGroupMap: Record<string, VehicleGroup> = {
    'PersonalCar': VehicleGroup.PrivateTransport,
    'PersonalCarWithTrailer': VehicleGroup.PrivateTransport,
    'Motorcycle': VehicleGroup.PrivateTransport,
    'Taxi': VehicleGroup.PublicTransport,
    'Truck': VehicleGroup.Trucks,
    'TruckWithTrailer': VehicleGroup.Trucks,
    'Bus': VehicleGroup.PublicTransport,
    'ParkMaintenance': VehicleGroup.ServiceVehicles,
    'RoadMaintenance': VehicleGroup.ServiceVehicles,
    'Ambulance': VehicleGroup.ServiceVehicles,
    'EvacuatingTransport': VehicleGroup.ServiceVehicles,
    'FireEngine': VehicleGroup.ServiceVehicles,
    'GarbageTruck': VehicleGroup.ServiceVehicles,
    'Hearse': VehicleGroup.ServiceVehicles,
    'PoliceCar': VehicleGroup.ServiceVehicles,
    'PostVan': VehicleGroup.ServiceVehicles,
    'PrisonerTransport': VehicleGroup.ServiceVehicles
};

// Group display information
const vehicleGroupInfo = {
    [VehicleGroup.PrivateTransport]: {
        name: 'Private Transport',
        description: 'Personal vehicles and motorcycles',
        icon: PrivateTransportSrc,
        color: '#4CAF50'
    },
    [VehicleGroup.PublicTransport]: {
        name: 'Public Transport', 
        description: 'Buses, taxis and other public transportation',
        icon: PublicTransportSrc,
        color: '#2196F3'
    },
    [VehicleGroup.Trucks]: {
        name: 'Freight & Trucks',
        description: 'Commercial trucks and freight vehicles',
        icon: FreightTrucksSrc,
        color: '#FF9800'
    },
    [VehicleGroup.ServiceVehicles]: {
        name: 'Service Vehicles',
        description: 'Emergency and municipal service vehicles',
        icon: ServiceVehiclesSrc,
        color: '#F44336'
    }
};

// Map vehicle types to their display information
const vehicleTypeMapping = {
    'PersonalCar': {
        name: 'Personal Car',
        icon: Car01Src,
        tollPrice: 2.50
    },
    'PersonalCarWithTrailer': {
        name: 'Car with Trailer',
        icon: CarTrailer01Src,
        tollPrice: 4.00
    },
    'Truck': {
        name: 'Truck',
        icon: EU_TruckTractor01Src,
        tollPrice: 8.00
    },
    'TruckWithTrailer': {
        name: 'Truck with Trailer',
        icon: TruckTrailer01Src,
        tollPrice: 12.00
    },
    'Bus': {
        name: 'Bus',
        icon: Bus01Src,
        tollPrice: 6.00
    },
    'Taxi': {
        name: 'Taxi',
        icon: Taxi01Src,
        tollPrice: 2.50
    },
    'ParkMaintenance': {
        name: 'Park Maintenance',
        icon: ParkMaintenanceVehicle01Src,
        tollPrice: 0.00
    },
    'RoadMaintenance': {
        name: 'Road Maintenance',
        icon: RoadMaintenanceVehicle01Src,
        tollPrice: 0.00
    },
    'Ambulance': {
        name: 'Ambulance',
        icon: EU_Ambulance01Src,
        tollPrice: 0.00
    },
    'EvacuatingTransport': {
        name: 'Evacuation Transport',
        icon: Bus02Src,
        tollPrice: 0.00
    },
    'FireEngine': {
        name: 'Fire Engine',
        icon: EU_FireTruck01Src,
        tollPrice: 0.00
    },
    'GarbageTruck': {
        name: 'Garbage Truck',
        icon: EU_GarbageTruck01Src,
        tollPrice: 0.00
    },
    'Hearse': {
        name: 'Hearse',
        icon: Hearse01Src,
        tollPrice: 0.00
    },
    'PoliceCar': {
        name: 'Police Car',
        icon: EU_PoliceVehicle01Src,
        tollPrice: 0.00
    },
    'PostVan': {
        name: 'Post Van',
        icon: EU_PostVan01Src,
        tollPrice: 3.00
    },
    'PrisonerTransport': {
        name: 'Prisoner Transport',
        icon: PrisonVan01Src,
        tollPrice: 0.00
    },
    'Motorcycle': {
        name: 'Motorcycle',
        icon: Motorbike01Src,
        tollPrice: 1.50
    }
};

// Interface for grouped vehicle statistics
interface GroupedVehicleStatistics {
    group: VehicleGroup;
    groupInfo: typeof vehicleGroupInfo[VehicleGroup];
    vehicles: VehicleStatistic[];
    totalQuantity: number;
    totalEarnings: number;
}

export const TollboothMainPanelComponent = () => {
    const [selectedTab, setSelectedTab] = useState<string>('overview');

    // Get tollbooth insight data from C# binding
    const tollBoothInsight = useValue(m_TollBoothInsight$);

    // Create vehicle statistics from tollbooth insight data
    const vehicleStatistics = useMemo((): VehicleStatistic[] => {
        if (!tollBoothInsight) {
            return [];
        }

        const stats: VehicleStatistic[] = [];

        // Iterate through all vehicle types and create statistics
        Object.entries(vehicleTypeMapping).forEach(([vehicleType, config]) => {
            const countProperty = `${vehicleType.charAt(0).toLowerCase() + vehicleType.slice(1)}Count`;
            const quantity = tollBoothInsight[countProperty] || 0;
            
            // Only include vehicles that have passed through
            if (quantity > -1) {
                const accumulativeEarnings = quantity * config.tollPrice;
                
                stats.push({
                    vehicleType,
                    icon: config.icon,
                    name: config.name,
                    tollPrice: config.tollPrice,
                    quantity,
                    accumulativeEarnings
                });
            }
        });

        // Sort by quantity (highest first)
        return stats.sort((a, b) => b.quantity - a.quantity);
    }, [tollBoothInsight]);

    // Group vehicles by their VehicleGroup
    const groupedVehicleStatistics = useMemo((): GroupedVehicleStatistics[] => {
        const groups = new Map<VehicleGroup, VehicleStatistic[]>();

        // Group vehicles by their type
        vehicleStatistics.forEach(vehicle => {
            const group = vehicleTypeToGroupMap[vehicle.vehicleType];
            if (group !== undefined) {
                if (!groups.has(group)) {
                    groups.set(group, []);
                }
                groups.get(group)!.push(vehicle);
            }
        });

        // Convert to GroupedVehicleStatistics array
        const result: GroupedVehicleStatistics[] = [];
        groups.forEach((vehicles, group) => {
            const totalQuantity = vehicles.reduce((sum, v) => sum + v.quantity, 0);
            const totalEarnings = vehicles.reduce((sum, v) => sum + v.accumulativeEarnings, 0);
            
            result.push({
                group,
                groupInfo: vehicleGroupInfo[group],
                vehicles: vehicles.sort((a, b) => b.quantity - a.quantity), // Sort within group
                totalQuantity,
                totalEarnings
            });
        });

        // Sort groups by total quantity (most active groups first)
        return result.sort((a, b) => b.totalQuantity - a.totalQuantity);
    }, [vehicleStatistics]);

    const renderTabContent = () => {
        switch (selectedTab) {
            case 'traffic':
                return (
                    <>
                        {/* Grouped Vehicle Statistics */}
                        {groupedVehicleStatistics.map((groupStats, index) => (
                            <CS2VanillaUIResolver.instance.InfoSectionFoldout
                                key={groupStats.group}
                                tooltip={DescriptionTooltip(groupStats.groupInfo.name, groupStats.groupInfo.description)}
                                header={groupStats.groupInfo.name}
                                initialExpanded={index === 0} // Expand first group by default
                            >
                                {/* Group Summary */}
                                <div style={{ 
                                    marginBottom: '8px',
                                    marginRight: '11px', 
                                    padding: '8px', 
                                    backgroundColor: 'rgba(0, 0, 0, 0.3)',
                                    borderRadius: '4px',
                                    border: `2px solid ${groupStats.groupInfo.color}`,
                                    borderLeft: `6px solid ${groupStats.groupInfo.color}`
                                }}>
                                    <div style={{ 
                                        display: 'flex', 
                                        alignItems: 'center', 
                                        gap: '8px',
                                        marginBottom: '4px',
                                        marginRight: '11px'
                                    }}>
                                        <img 
                                            src={groupStats.groupInfo.icon}
                                            style={{ width: '24px', height: '24px', marginRight: '10px' }} 
                                            alt={groupStats.groupInfo.name}
                                        />                                    
                                        <span style={{ 
                                            fontSize: '14px', 
                                            fontWeight: 'bold',
                                            color: '#ffffff'
                                        }}>
                                            {groupStats.groupInfo.name}
                                        </span>
                                    </div>
                                    
                                    <div style={{ 
                                        display: 'flex', 
                                        justifyContent: 'space-between',
                                        fontSize: '12px',
                                        color: '#cccccc'
                                    }}>
                                        <span>Total Vehicles: <LocalizedNumber unit={Unit.Integer} value={groupStats.totalQuantity as number}></LocalizedNumber></span>
                                        <span>Total Revenue: <LocalizedNumber unit={Unit.Money} value={groupStats.totalEarnings as number}></LocalizedNumber></span>
                                    </div>
                                </div>

                                {/* Vehicle Statistics Table for this group */}
                                <VehicleStatisticsTable statistics={groupStats.vehicles} />
                            </CS2VanillaUIResolver.instance.InfoSectionFoldout>
                        ))}

                        {/* Overall Summary Information */}
                        <CS2VanillaUIResolver.instance.InfoSectionFoldout
                            tooltip={DescriptionTooltip("Overall Statistics", "Total statistics across all vehicle groups")}
                            header="Overall Traffic Summary"
                            initialExpanded={false}
                        >
                            <div style={{ 
                                marginTop: '12px', 
                                padding: '8px', 
                                backgroundColor: 'rgba(0, 0, 0, 0.3)',
                                borderRadius: '4px',
                                border: '1px solid rgba(255, 255, 255, 0.1)'
                            }}>
                                <CS2VanillaUIResolver.instance.InfoRow
                                    left="Total Vehicles Processed"
                                    right={
                                        <LocalizedNumber unit={Unit.Integer} value={tollBoothInsight?.totalVehiclesPassed as number}></LocalizedNumber>
                                        || <LocalizedNumber unit={Unit.Integer} value={0}></LocalizedNumber>
                                    }
                                    disableFocus={true}
                                    className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                                />
                                <CS2VanillaUIResolver.instance.InfoRow
                                    left="Total Revenue Generated"
                                    right={
                                        <LocalizedNumber unit={Unit.Money} signed={true} value={tollBoothInsight?.totalRevenue as number}></LocalizedNumber>
                                        || <LocalizedNumber unit={Unit.Money} signed={true} value={0}></LocalizedNumber>
                                    }
                                    disableFocus={true}
                                    className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                                />
                                <CS2VanillaUIResolver.instance.InfoRow
                                    left="Average Revenue per Vehicle"
                                    right={
                                        <LocalizedNumber unit={Unit.Money} signed={true} value={((tollBoothInsight?.totalRevenue || 0) / Math.max(tollBoothInsight?.totalVehiclesPassed || 1, 1)) as number}></LocalizedNumber>
                                    }
                                    disableFocus={true}
                                    className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                                />
                                <CS2VanillaUIResolver.instance.InfoRow
                                    left="Active Vehicle Groups"
                                    right={
                                        <LocalizedNumber unit={Unit.Integer} value={groupedVehicleStatistics.length as number}></LocalizedNumber>
                                    }
                                    disableFocus={true}
                                    className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                                />
                            </div>
                        </CS2VanillaUIResolver.instance.InfoSectionFoldout>

                        {/* Additional Traffic Information */}
                        <InfoSection focusKey={CS2VanillaUIResolver.instance.FOCUS_DISABLED} disableFocus={true}>
                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Traffic Analysis"
                                tooltip={DescriptionTooltip("Traffic Analysis", "Real-time traffic flow analysis")}
                                uppercase={true}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />
                            
                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Peak Traffic Period"
                                right="6:00 AM - 9:00 AM"
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />
                            
                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Current Traffic Flow"
                                right="Normal"
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            {/* Most Active Group */}
                            {groupedVehicleStatistics.length > 0 && (
                                <CS2VanillaUIResolver.instance.InfoRow
                                    left="Most Active Vehicle Group"
                                    right={groupedVehicleStatistics[0].groupInfo.name}
                                    disableFocus={true}
                                    className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                                />
                            )}
                        </InfoSection>
                    </>
                );

            default:
                return (
                    <>
                        {/* Overview content */}
                        <InfoSection focusKey={CS2VanillaUIResolver.instance.FOCUS_DISABLED} disableFocus={true}>
                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Tollbooth Status"
                                right="Active"
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />
                            
                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Total Revenue Today"
                                right={
                                    <LocalizedNumber unit={Unit.Money} signed={true} value={tollBoothInsight?.totalRevenue as number}></LocalizedNumber> 
                                     || <LocalizedNumber unit={Unit.Money} signed={true} value={0}></LocalizedNumber>
                                }
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />
                            
                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Vehicles Processed Today"
                                right={
                                    <LocalizedNumber unit={Unit.Integer} value={tollBoothInsight?.totalVehiclesPassed as number}></LocalizedNumber>
                                    || <LocalizedNumber unit={Unit.Integer} value={0}></LocalizedNumber>
                                }
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            {/* Most Active Group in Overview */}
                            {groupedVehicleStatistics.length > 0 && (
                                <CS2VanillaUIResolver.instance.InfoRow
                                    left="Primary Traffic Type"
                                    right={groupedVehicleStatistics[0].groupInfo.name}
                                    disableFocus={true}
                                    className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                                />
                            )}
                        </InfoSection>
                        
                        {/* Mini grouped statistics for overview */}
                        <CS2VanillaUIResolver.instance.InfoSectionFoldout
                            tooltip={DescriptionTooltip("Vehicle Group Summary", "Summary of vehicle activity by group")}
                            header="Vehicle Groups Overview"
                            initialExpanded={false}
                        >
                            {groupedVehicleStatistics.slice(0, 3).map(groupStats => (
                                <div 
                                    key={groupStats.group}
                                    style={{ 
                                        marginBottom: '8px',
                                        marginRight: '11px',
                                        padding: '8px', 
                                        backgroundColor: 'rgba(0, 0, 0, 0.2)',
                                        borderRadius: '4px',
                                        borderLeft: `4px solid ${groupStats.groupInfo.color}`
                                    }}
                                >
                                    <div style={{ 
                                        display: 'flex', 
                                        justifyContent: 'space-between',
                                        alignItems: 'center',
                                        msFlexDirection: 'column'
                                    }}>
                                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', flexBasis: '20px' }}>
                                            <img 
                                                src={groupStats.groupInfo.icon}
                                                style={{ width: '20px', height: '20px', marginRight: '10px' }} 
                                                alt={groupStats.groupInfo.name}
                                            />
                                        </div>
                                        <div style={{ display: 'flex', alignItems: 'left', gap: '8px', flexBasis: '200px' }}>
                                            <span style={{ fontSize: '13px', color: '#ffffff' }}>
                                                {groupStats.groupInfo.name}
                                            </span>
                                        </div>
                                        <div style={{ 
                                            fontSize: '12px', 
                                            color: '#cccccc',
                                            textAlign: 'right',
                                            display: 'flex',
                                            flexBasis: '60px'
                                        }}>
                                            <span><LocalizedNumber value={groupStats.totalQuantity as number} unit={Unit.Integer}></LocalizedNumber>&nbsp;vehicles</span>
                                        </div>
                                        <div style={{
                                            fontSize: '12px',
                                            color: '#cccccc',
                                            textAlign: 'right',
                                            display: 'flex',
                                            flexBasis: '30px'
                                        }}>
                                            <span><LocalizedNumber value={groupStats.totalEarnings as number} unit={Unit.Money}></LocalizedNumber></span>
                                        </div>
                                    </div>
                                </div>
                            ))}
                        </CS2VanillaUIResolver.instance.InfoSectionFoldout>
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
                        This is an automatic tollbooth that collects tolls from vehicles passing through excluding public transportation.
                    </div>
                </CS2VanillaUIResolver.instance.DescriptionRow>
            </InfoSection>

            {/* INFO BUTTON NAVIGATION SECTION */}
            <InfoSection focusKey={CS2VanillaUIResolver.instance.FOCUS_DISABLED} disableFocus={true}>
                <CS2VanillaUIResolver.instance.InfoRow
                    left={"Navigation"}
                    tooltip={DescriptionTooltip("Panel Navigation", "Switch between different sections of the tollbooth information")}
                    uppercase={true}
                    disableFocus={true}
                    className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                />

                <div style={{
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '8px',
                    padding: '8px 0',
                    flexWrap: 'wrap'
                }}>
                    <CS2VanillaUIResolver.instance.InfoButton
                        label="Overview" 
                        icon={TollboothOverviewSrc}
                        selected={selectedTab === 'overview'}
                        onSelect={() => {
                            console.log("Overview tab selected");
                            setSelectedTab('overview');
                        }}
                    />

                    <CS2VanillaUIResolver.instance.InfoButton
                        label="Traffic by Groups"
                        icon={VehiclesGroupSrc}
                        selected={selectedTab === 'traffic'}
                        onSelect={() => {
                            console.log("Traffic tab selected");
                            setSelectedTab('traffic');
                        }}
                    />                
                </div>
            </InfoSection>

            {/* Dynamic Content Based on Selected Tab */}
            {renderTabContent()}
        </>
    );
};
