import { getModule } from "cs2/modding";
import { Theme, Color } from "cs2/bindings";
import { bindValue, trigger, useValue } from "cs2/api"; 
import { useLocalization } from "cs2/l10n";
import mod from "../../../mod.json";
import { tool } from "cs2/bindings";
import { Button } from "cs2/ui";
import { Entity } from "cs2/utils";
import { FocusDisabled } from "cs2/input";
import { CS2VanillaUIResolver } from "mods/CS2VanillaUIResolver";
import {
    formatLargeNumber,
    useFormattedLargeNumber,
    makePretty,
    makePrettyUppercase,
    formatInteger,
    capitalize,
    formatFixedLengthInt
} from '../../../game-ui-modules/format';
import { TextInput } from "../../../game-ui-modules/text-input";

import {
    Notification,
    HappinessNotification,
    AverageHappinessNotification,
    ProfitabilityNotification,
    ConditionNotification,
    NotificationBadge,
    EnhancedNotification,
    NotificationContainer
} from '../../../game-ui-modules/NotificationControl.2';

import {
    TrafficVolumeChart,
    TrafficFlowChart,
    TrafficChartTheme
} from "../../../game-ui-modules/TrafficChartComponents";
import { InfoLink } from '../../../game-ui-modules/InfoLinkComponent';

import { Unit } from "cs2/l10n";
//import styles from "mods/TollboothSelectedInfoPanel/TollboothMainPanelComponent.module.scss";
import { useState, useMemo } from "react";
import tollboothStyles from "./TollboothDropdown.module.scss";
import React from "react";
//import { InfoSectionFoldout } from '../../../game-ui-modules/InfoSectionFoldoutControl';
import { InfoButton } from '../../../game-ui-modules/InfoButtonComponent';

//import { InfoRow, TooltipRow } from '../../../game-ui-modules/InfoRowControl';

const m_TollAmount$ = bindValue<string>(mod.id, "m_TollAmount");   
const m_TotalIncome$ = bindValue<string>(mod.id, "m_TotalIncome");

// Your existing image imports...
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




// Vehicle type definitions for dropdowns
interface VehicleType {
    id: string;
    name: string;
    toll: number;
    icon?: string;
}


export const InfoRowTheme: Theme | any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.module.scss",
    "classes"
)


export const InfoSection: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.tsx",
    "InfoSection"
) 

/*
export const InfoRow: any = getModule(
    "game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.tsx",
    "InfoRow"
)
*/

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

export const TollboothMainPanelComponent = () => {
    const m_TollAmount = useValue(m_TollAmount$);
    const m_TotalIncome = useValue(m_TotalIncome$);
    console.log("TollboothMainPanelComponent called", m_TollAmount, m_TotalIncome);

    // Additional values for progress bar examples
    const m_VehiclesPassed = 125;
    const m_DailyTarget = 500;
    const m_MaintenanceLevel = 85;
    const m_TrafficLoad = 65;
    const m_CollectionEfficiency = 92;

    const vehicleTypes: VehicleType[] = [
        { id: 'car', name: 'Personal Vehicle', toll: 5.0, icon: Car01Src },
        { id: 'bus', name: 'Public Bus', toll: 15.0, icon: Bus01Src },
        { id: 'truck', name: 'Freight Truck', toll: 25.0, icon: NA_TruckTractor01Src },
        { id: 'emergency', name: 'Emergency Vehicle', toll: 0.0, icon: EU_Ambulance01Src },
        { id: 'service', name: 'Service Vehicle', toll: 10.0, icon: NA_PostVan01Src },
    ];

    const qualityOptions = [
        { id: 'low', name: 'Low Quality', description: 'Basic toll collection', efficiency: 70 },
        { id: 'medium', name: 'Medium Quality', description: 'Standard toll collection', efficiency: 85 },
        { id: 'high', name: 'High Quality', description: 'Advanced toll collection', efficiency: 95 },
        { id: 'ultra', name: 'Ultra Quality', description: 'Premium toll collection', efficiency: 99 },
    ];

    const timeOfDayOptions = [
        { id: 'peak', name: 'Peak Hours', multiplier: 1.5, description: 'Higher rates during busy times' },
        { id: 'normal', name: 'Normal Hours', multiplier: 1.0, description: 'Standard toll rates' },
        { id: 'night', name: 'Night Hours', multiplier: 0.8, description: 'Reduced rates for night time' },
        { id: 'weekend', name: 'Weekend', multiplier: 1.2, description: 'Weekend pricing' },
    ];

    const paymentMethods = [
        { id: 'electronic', name: 'Electronic Only', speed: 'Fast', description: 'Automatic electronic toll collection' },
        { id: 'cash', name: 'Cash Only', speed: 'Slow', description: 'Manual cash collection booth' },
        { id: 'mixed', name: 'Mixed Payment', speed: 'Medium', description: 'Both electronic and cash accepted' },
    ];

    // State for dropdowns
    const [selectedQuality, setSelectedQuality] = useState('high');
    const [selectedVehicleType, setSelectedVehicleType] = useState('car');
    const [selectedTollRate, setSelectedTollRate] = useState(5.0);
    const [selectedTimeOfDay, setSelectedTimeOfDay] = useState('normal');
    const [selectedPaymentMethod, setSelectedPaymentMethod] = useState('mixed');

    // State for InfoButton tabs
    const [selectedTab, setSelectedTab] = useState<string>('overview');


    // Handle quality selection
    const handleQualityChange = (value: string) => {
        console.log("Quality changed to:", value);
        setSelectedQuality(value);
        // Trigger any backend updates here
        trigger(mod.id, "setTollboothQuality", value);
    };

    // Handle vehicle type selection
    const handleVehicleTypeChange = (value: string) => {
        console.log("Vehicle type changed to:", value);
        setSelectedVehicleType(value);
        const vehicle = vehicleTypes.find(v => v.id === value);
        if (vehicle) {
            setSelectedTollRate(vehicle.toll);
            // Trigger backend update for toll rates
            trigger(mod.id, "setVehicleTollRate", value, vehicle.toll);
        }
    };

    // Handle time of day selection
    const handleTimeOfDayChange = (value: string) => {
        console.log("Time of day changed to:", value);
        setSelectedTimeOfDay(value);
        trigger(mod.id, "setTimeOfDayPricing", value);
    };

    // Handle payment method selection
    const handlePaymentMethodChange = (value: string) => {
        console.log("Payment method changed to:", value);
        setSelectedPaymentMethod(value);
        trigger(mod.id, "setPaymentMethod", value);
    };

    // Helper functions
    const getCurrentQualityName = () => {
        const quality = qualityOptions.find(q => q.id === selectedQuality);
        return quality?.name || 'Unknown';
    };

    const getCurrentVehicleName = () => {
        const vehicle = vehicleTypes.find(v => v.id === selectedVehicleType);
        return vehicle?.name || 'Unknown';
    };

    const getCurrentTimeOfDayName = () => {
        const timeOfDay = timeOfDayOptions.find(t => t.id === selectedTimeOfDay);
        return timeOfDay?.name || 'Unknown';
    };

    const getCurrentPaymentMethodName = () => {
        const paymentMethod = paymentMethods.find(p => p.id === selectedPaymentMethod);
        return paymentMethod?.name || 'Unknown';
    };

    const trafficVolumeData = [0, 5, 10, 23, 41];
    const trafficFlowData = [1, 22, 24, 54, 66];

    // Sample notification data for demonstrations
    const sampleNotifications = {
        tollboothAlert: {
            key: "tollbooth_maintenance_required",
            iconPath: "Media/Misc/Warning.svg",
            count: 3
        },
        citizenHappiness: {
            key: "citizen_happy_with_toll_rates",
            iconPath: "Media/Game/Icons/Happy.svg"
        },
        averageHappiness: {
            key: "district_happiness_average",
            iconPath: "Media/Game/Icons/Happy.svg"
        },
        profitability: {
            key: "tollbooth_profitability_high",
            iconPath: MoneySrc
        },
        citizenCondition: {
            key: "citizen_traffic_stress",
            iconPath: "Media/Game/Icons/Healthcare.svg"
        },
        trafficJam: {
            key: "traffic_congestion_warning",
            iconPath: "Media/Game/Icons/Traffic.svg",
            count: 12
        }
    };

    // Function to render content based on selected tab
    const renderTabContent = () => {
        switch (selectedTab) {
            case 'traffic':
                return (
                    <>
                        {/* Traffic Charts Section */}
                        <InfoSection focusKey={CS2VanillaUIResolver.instance.FOCUS_DISABLED} disableFocus={true}>
                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Traffic Volume (24h)"
                                tooltip="Hourly traffic volume through this tollbooth"
                                uppercase={true}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />
                            <div>
                                <CS2VanillaUIResolver.instance.TrafficVolumeChart data={trafficVolumeData}></CS2VanillaUIResolver.instance.TrafficVolumeChart>
                            </div>
                        </InfoSection>

                        {/* Traffic Flow Chart Section */}
                        <InfoSection focusKey={CS2VanillaUIResolver.instance.FOCUS_DISABLED} disableFocus={true}>
                            <CS2VanillaUIResolver.instance.InfoRow
                                left="Traffic Flow (24h)"
                                tooltip="Traffic flow efficiency through this tollbooth"
                                uppercase={true}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />
                            <div>
                                <CS2VanillaUIResolver.instance.TrafficFlowChart data={trafficFlowData}></CS2VanillaUIResolver.instance.TrafficFlowChart>
                            </div>
                        </InfoSection>

                        <InfoSection focusKey={CS2VanillaUIResolver.instance.FOCUS_DISABLED} disableFocus={true}>
                            <CS2VanillaUIResolver.instance.InfoRow
                                left={"Traffic Load"}
                                right={`${m_TrafficLoad}%`}
                                uppercase={true}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />
                            <CS2VanillaUIResolver.instance.InfoRow
                                left={"Daily Vehicle Target"}
                                tooltip={DescriptionTooltip("Daily Vehicle Target", "Progress towards daily vehicle processing target")}
                                uppercase={true}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />
                            <CS2VanillaUIResolver.instance.CapacityBar
                                progress={m_VehiclesPassed}
                                max={m_DailyTarget}
                                invertColorCodes={false}
                                children={<span>{m_VehiclesPassed} / {m_DailyTarget} vehicles</span>}
                            />
                        </InfoSection>
                    </>
                );

            case 'finance':
                return (
                    <>
                        {/* Financial Information */}
                        <InfoSection focusKey={CS2VanillaUIResolver.instance.FOCUS_DISABLED} disableFocus={true}>
                            <CS2VanillaUIResolver.instance.InfoRow
                                left={"Toll Amount"}
                                right={m_TollAmount}
                                tooltip={DescriptionTooltip("Current Toll Amount", "The toll amount for the selected vehicle type")}
                                uppercase={true}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />
                            <CS2VanillaUIResolver.instance.InfoRow
                                left={"Total Income"}
                                right={m_TotalIncome}
                                tooltip={DescriptionTooltip("Total Income", "Total revenue generated by this tollbooth")}
                                uppercase={true}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />
                            <CS2VanillaUIResolver.instance.InfoRow
                                left={"Collection Efficiency"}
                                right={`${m_CollectionEfficiency}%`}
                                uppercase={true}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />
                        </InfoSection>

                        {/* Vehicle Type Pricing */}
                        <InfoSection focusKey={CS2VanillaUIResolver.instance.FOCUS_DISABLED} disableFocus={true}>
                            <CS2VanillaUIResolver.instance.InfoRow
                                left={"Vehicle Type Configuration"}
                                right={
                                    <CS2VanillaUIResolver.instance.Dropdown
                                        focusKey="Dropdown De-obfuscated"
                                        onToggle={(visible) => console.log("Vehicle dropdown toggled:", visible)}
                                        className={CS2VanillaUIResolver.instance.DropdownTheme.dropdownMenu}
                                        content={
                                            <>
                                                {vehicleTypes.map(vehicle => (
                                                    <CS2VanillaUIResolver.instance.DropdownItem
                                                        key={vehicle.id}
                                                        value={vehicle.id}
                                                        selected={vehicle.id === selectedVehicleType}
                                                        onChange={handleVehicleTypeChange}
                                                        focusKey={`vehicle-${vehicle.id}`}
                                                        className={CS2VanillaUIResolver.instance.DropdownTheme.dropdownItem}
                                                    >
                                                        <div style={{ display: 'flex', alignItems: 'center', gap: '12px', width: '100%' }}>
                                                            {vehicle.icon && (
                                                                <img
                                                                    src={vehicle.icon}
                                                                    style={{ width: '32px', height: '32px' }}
                                                                    alt=""
                                                                />
                                                            )}
                                                            <div style={{ flex: 1 }}>
                                                                <div><strong>{vehicle.name}</strong></div>
                                                                <div style={{ fontSize: '0.9em', opacity: 0.8 }}>
                                                                    Toll: ${vehicle.toll.toFixed(2)}
                                                                </div>
                                                            </div>
                                                        </div>
                                                    </CS2VanillaUIResolver.instance.DropdownItem>
                                                ))}
                                            </>
                                        }
                                    >
                                        <CS2VanillaUIResolver.instance.DropdownToggle
                                            showHint={true}
                                            tooltipLabel="Select vehicle type to configure toll rates"
                                            className={CS2VanillaUIResolver.instance.DropdownTheme.dropdownToggle}
                                        >
                                            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                                                {vehicleTypes.find(v => v.id === selectedVehicleType)?.icon && (
                                                    <img
                                                        src={vehicleTypes.find(v => v.id === selectedVehicleType)?.icon}
                                                        style={{ width: '24px', height: '24px' }}
                                                        alt=""
                                                    />
                                                )}
                                                <span>{getCurrentVehicleName()}</span>
                                                <span style={{ marginLeft: 'auto', opacity: 0.8 }}>
                                                    ${selectedTollRate.toFixed(2)}
                                                </span>
                                            </div>
                                        </CS2VanillaUIResolver.instance.DropdownToggle>
                                    </CS2VanillaUIResolver.instance.Dropdown>
                                }
                                tooltip={DescriptionTooltip("Vehicle Type Rates", "Configure toll rates for different vehicle types")}
                                uppercase={true}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />
                        </InfoSection>
                    </>
                );

            case 'settings':
                return (
                    <>
                        {/* Quality & Settings */}
                        <InfoSection focusKey={CS2VanillaUIResolver.instance.FOCUS_DISABLED} disableFocus={true}>
                            <CS2VanillaUIResolver.instance.InfoRow
                                left={"Tollbooth Quality"}
                                right={getCurrentQualityName()}
                                tooltip={DescriptionTooltip("Quality Level", "Higher quality tollbooths process vehicles faster")}
                                uppercase={true}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            <CS2VanillaUIResolver.instance.InfoRow
                                left={"Time-based Pricing"}
                                right={getCurrentTimeOfDayName()}
                                tooltip={DescriptionTooltip("Pricing Schedule", "Current time-based pricing configuration")}
                                uppercase={true}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            <CS2VanillaUIResolver.instance.InfoRow
                                left={"Payment Method"}
                                right={getCurrentPaymentMethodName()}
                                tooltip={DescriptionTooltip("Payment Options", "Accepted payment methods at this tollbooth")}
                                uppercase={true}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />

                            <CS2VanillaUIResolver.instance.InfoRow
                                left={"Maintenance Level"}
                                right={`${m_MaintenanceLevel}%`}
                                uppercase={true}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />
                        </InfoSection>
                    </>
                );

            case 'actions':
                return (
                    <>
                        {/* Management Actions */}
                        <InfoSection focusKey={CS2VanillaUIResolver.instance.FOCUS_DISABLED} disableFocus={true}>
                            <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', padding: '8px' }}>
                                <Button
                                    variant="primary"
                                    onClick={() => {
                                        console.log("Collect all tolls");
                                        trigger(mod.id, "collectAllTolls");
                                    }}
                                    className={roundButtonHighlightStyle.button}
                                >
                                    💰 Collect All Tolls
                                </Button>

                                <Button
                                    variant="round"
                                    onClick={() => {
                                        console.log("Perform maintenance");
                                        trigger(mod.id, "performMaintenance");
                                    }}
                                    className={roundButtonHighlightStyle.button}
                                >
                                    🔧 Perform Maintenance
                                </Button>

                                <Button
                                    variant="default"
                                    onClick={() => {
                                        console.log("Reset statistics");
                                        trigger(mod.id, "resetStatistics");
                                    }}
                                    className={roundButtonHighlightStyle.button}
                                >
                                    📊 Reset Statistics
                                </Button>

                                <Button
                                    variant="default"
                                    onClick={() => {
                                        console.log("Upgrade tollbooth");
                                        trigger(mod.id, "upgradeTollbooth");
                                    }}
                                    className={roundButtonHighlightStyle.button}
                                >
                                    ⬆️ Upgrade Booth
                                </Button>
                            </div>
                        </InfoSection>
                    </>
                );

            default:
            case 'overview':
                return (
                    <>
                        {/* Basic Information Section */}
                        <InfoSection focusKey={CS2VanillaUIResolver.instance.FOCUS_DISABLED} disableFocus={true}>
                            <CS2VanillaUIResolver.instance.InfoRow
                                left={"Toll Amount"}
                                right={m_TollAmount}
                                tooltip={DescriptionTooltip("Current Toll Amount", "The toll amount for the selected vehicle type")}
                                uppercase={true}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />
                            <CS2VanillaUIResolver.instance.InfoRow
                                left={"Total Income"}
                                right={m_TotalIncome}
                                tooltip={DescriptionTooltip("Total Income", "Total revenue generated by this tollbooth")}
                                uppercase={true}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />
                        </InfoSection>

                        {/* Overview Performance */}
                        <InfoSection focusKey={CS2VanillaUIResolver.instance.FOCUS_DISABLED} disableFocus={true}>
                            <CS2VanillaUIResolver.instance.InfoRow
                                left={"Daily Vehicle Target"}
                                tooltip={DescriptionTooltip("Daily Vehicle Target", "Progress towards daily vehicle processing target")}
                                uppercase={true}
                                disableFocus={true}
                                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                            />
                            <CS2VanillaUIResolver.instance.CapacityBar
                                progress={m_VehiclesPassed}
                                max={m_DailyTarget}
                                invertColorCodes={false}
                                children={<span>{m_VehiclesPassed} / {m_DailyTarget} vehicles</span>}
                            />
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
                        Here is all the information about your tollbooth. You can configure toll rates, view income statistics, and manage operational settings.
                    </div>
                </CS2VanillaUIResolver.instance.DescriptionRow>
            </InfoSection>

            <InfoSection focusKey={CS2VanillaUIResolver.instance.FOCUS_DISABLED} disableFocus={true}>
                <CS2VanillaUIResolver.instance.InfoWrapBox>
                    This is an example of InfoWrapBox. You can put any content you want in here and it will wrap nicely within the panel.
                </CS2VanillaUIResolver.instance.InfoWrapBox>
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

                {/* Navigation Buttons Row */}

                <div style={{
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '8px',
                    padding: '8px 0',
                    flexWrap: 'wrap'
                }}>
                    <CS2VanillaUIResolver.instance.InfoButton
                        label="Overview"
                        icon={EconomySrc}
                        selected={selectedTab === 'overview'}
                        onSelect={() => {
                            console.log("Overview tab selected");
                            setSelectedTab('overview');
                        }}
                    />

                    <CS2VanillaUIResolver.instance.InfoButton
                        label="Traffic"
                        icon="Media/Game/Icons/Traffic.svg"
                        selected={selectedTab === 'traffic'}
                        onSelect={() => {
                            console.log("Traffic tab selected");
                            setSelectedTab('traffic');
                        }}
                    />

                    <CS2VanillaUIResolver.instance.InfoButton
                        label="Finance"
                        icon={MoneySrc}
                        selected={selectedTab === 'finance'}
                        onSelect={() => {
                            console.log("Finance tab selected");
                            setSelectedTab('finance');
                        }}
                    />

                    <CS2VanillaUIResolver.instance.InfoButton
                        label="Settings"
                        icon="Media/Glyphs/Gear.svg"
                        selected={selectedTab === 'settings'}
                        onSelect={() => {
                            console.log("Settings tab selected");
                            setSelectedTab('settings');
                        }}
                    />

                    <CS2VanillaUIResolver.instance.InfoButton
                        label="Actions"
                        icon="Media/Glyphs/Dice.svg"
                        selected={selectedTab === 'actions'}
                        onSelect={() => {
                            console.log("Actions tab selected");
                            setSelectedTab('actions');
                        }}
                    />

                    {/* Add this new notifications button
                    <CS2VanillaUIResolver.instance.InfoButton
                        label="Notifications"
                        icon="Media/Game/Icons/Notifications.svg"
                        selected={selectedTab === 'notifications'}
                        onSelect={() => {
                            console.log("Notifications tab selected");
                            setSelectedTab('notifications');
                        }}
                    /> */}
                </div>
            </InfoSection>

            {/* Dynamic Content Based on Selected Tab */}
            {renderTabContent()}
        </>
    );
};
