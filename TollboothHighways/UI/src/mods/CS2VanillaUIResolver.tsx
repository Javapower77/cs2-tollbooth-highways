import { BalloonAlignment, BalloonDirection, Color, FocusKey, Theme, UniqueFocusKey } from "cs2/bindings";
import { InputAction } from "cs2/input";
import { ModuleRegistry } from "cs2/modding";
import { HTMLAttributes, ReactNode, RefObject } from "react";

// This is an auto-generated file based on analysis of Cities: Skylines II vanilla UI modules
// Last updated: 2025-07-29

// COMPREHENSIVE PROPS INTERFACES
type PropsToolButton = {
    focusKey?: UniqueFocusKey | null;
    src?: string;
    selected?: boolean;
    multiSelect?: boolean;
    disabled?: boolean;
    tooltip?: ReactNode | null;
    selectSound?: any;
    uiTag?: string;
    className?: string;
    children?: ReactNode;
    onSelect?: (x: any) => any;
} & HTMLAttributes<any>;

type PropsSection = {
    title?: string | null;
    uiTag?: string;
    children: ReactNode;
};

type PropsInfoSection = {
    title?: string;
    children: ReactNode;
    className?: string;
};

type PropsDescriptionTooltip = {
    title: string | null;
    description: string | null;
    content?: JSX.Element | null;
    shortcut?: ReactNode | null;
    children?: ReactNode;
};

type PropsTooltip = {
    tooltip: ReactNode;
    disabled?: boolean;
    forceVisible?: boolean;
    direction?: BalloonDirection;
    alignment?: BalloonAlignment;
    className?: string;
    children: React.ReactElement & { ref?: React.Ref<HTMLElement> };
    anchorElRef?: RefObject<HTMLElement>;
};

type PropsTooltipRenderer = {
    title: string;
    description?: string;
    icon?: string;
    shortcut?: ReactNode;
    children?: ReactNode;
    theme?: any;
};

type PropsBoundTooltipGroup = {
    children: ReactNode;
};

type PropsSelectedInfoPanel = {
    children: ReactNode;
    title?: string;
    onClose?: () => void;
};

type PropsColorField = {
    focusKey?: FocusKey;
    disabled?: boolean;
    value?: Color;
    className?: string;
    selectAction?: InputAction;
    alpha?: any;
    popupDirection?: BalloonDirection;
    onChange?: (e: Color) => void;
    onClick?: (e: any) => void;
    onMouseEnter?: (e: any) => void;
    onMouseLeave?: (e: any) => void;
};

type PropsLayout = {
    style?: React.CSSProperties;
    className?: string;
    children: ReactNode;
};

type PropsButton = {
    variant?: "flat" | "primary" | "round" | "menu" | "default" | "icon" | "floating";
    src?: string;
    tinted?: boolean;
    children?: ReactNode;
} & HTMLAttributes<any>;

type PropsIcon = {
    tinted?: boolean;
    className?: string;
    src?: string;
    children?: ReactNode;
};

type PropsPanel = {
    draggable?: boolean;
    header?: ReactNode | null;
    theme?: any;
    children: ReactNode;
} & HTMLAttributes<any>;

type PropsGamePanelRenderer = {
    children: ReactNode;
    __Type: "InfoPanel";
};

type PropsSlider = {
    focusKey?: FocusKey;
    disabled?: boolean;
    min?: number;
    max?: number;
    step?: number;
    value?: number;
    className?: string;
    onChange?: (value: number) => void;
};

type PropsTextField = {
    focusKey?: FocusKey;
    disabled?: boolean;
    value?: string;
    placeholder?: string;
    className?: string;
    onChange?: (value: string) => void;
};

type PropsCheckbox = {
    focusKey?: FocusKey;
    disabled?: boolean;
    checked?: boolean;
    className?: string;
    children?: ReactNode;
    onChange?: (checked: boolean) => void;
};


type PropsProgressBar = {
    value?: number;
    max?: number;
    className?: string;
    theme?: any;
};

type PropsScrollable = {
    className?: string;
    children: ReactNode;
    direction?: "vertical" | "horizontal" | "both";
};

type PropsModal = {
    visible?: boolean;
    title?: string;
    onClose?: () => void;
    children: ReactNode;
    className?: string;
};

type PropsTabContainer = {
    selectedIndex?: number;
    onTabSelect?: (index: number) => void;
    children: ReactNode;
    className?: string;
};

type PropsTab = {
    label?: string;
    disabled?: boolean;
    children: ReactNode;
};

type PropsFormGroup = {
    label?: string;
    children: ReactNode;
    className?: string;
};

type PropsToggle = {
    checked?: boolean;
    disabled?: boolean;
    onChange?: (checked: boolean) => void;
    className?: string;
};

// worked great after de-obfuscation
type CapacityBarProps = {
    progress: number;
    max: number;
    plain?: boolean;
    invertColorCodes?: boolean;
    children?: React.ReactNode;
    className?: string;
};

type DropdownProps = {
    focusKey?: string;
    initialFocused?: string;
    theme?: any;
    content?: ReactNode;
    alignment?: string;
    children: ReactNode;
    onToggle?: (visible: boolean) => void;
    className?: string;
};

type DropdownToggleProps = {
    theme?: any;
    buttonTheme?: any;
    sounds?: any;
    showHint?: boolean;
    selectSound?: string;
    className?: string;
    children: ReactNode;
    tooltipLabel?: string;
    [key: string]: any;
    openIconComponent?: ReactNode;
    closeIconComponent?: ReactNode;
};

type DropdownItemProps = {
    focusKey?: string;
    value?: any;
    selected?: boolean;
    theme?: any;
    sounds?: any;
    className?: string;
    onChange?: (value: any) => void;
    onToggleSelected?: (value: any) => void;
    closeOnSelect?: boolean;
    children: ReactNode;
}

type InfoRowProps = {
    icon?: string;
    left?: ReactNode;
    right?: ReactNode;
    tooltip?: ReactNode;
    link?: ReactNode;
    uppercase?: boolean;
    subRow?: boolean;
    disableFocus?: boolean;
    className?: string;
    noShrinkRight?: boolean;
}

type TooltipRowProps = {
    icon?: string;
    left?: ReactNode;
    right?: ReactNode;
    uppercase?: boolean;
    subRow?: boolean;
    className?: string;
};

type InfoSectionFoldoutProps = {
    header: ReactNode;
    initialExpanded?: boolean;
    expandFromContent?: boolean;
    focusKey?: string;
    tooltip?: ReactNode;
    disableFocus?: boolean;
    className?: string;
    onToggleExpanded?: (expanded: boolean) => void;
    children: ReactNode;
};

type TrafficChartProps = {
    data: number[];
    className?: string;
}

type DescriptionRowProps = {
    children: React.ReactNode;
    className?: string;
}

type InfoButtonProps = {
    label: string;
    icon?: string;
    selected?: boolean;
    onSelect?: () => void;
    className?: string;
}

type InfoLinkProps = {
    icon?: string;
    tooltip?: React.ReactNode;
    uppercase?: boolean;
    onSelect?: () => void;
    children: React.ReactNode;
    className?: string;
}

type InfoWrapBoxProps = {
    className?: string
    children: ReactNode;
};

// NOTIFICATION CONTROLS PROPS INTERFACES
type NotificationData = {
    key: string;
    iconPath: string;
    count?: number;
    priority?: 'low' | 'medium' | 'high' | 'critical';
    timestamp?: Date;
    persistent?: boolean;
}

type NotificationProps = {
    notification: NotificationData;
    anchorElRef?: React.RefObject<HTMLElement>;
    tooltipTags?: string[];
    theme?: any;
    onClick?: () => void;
    onDismiss?: () => void;
    'data-testid'?: string;
    className?: string;
};

type HappinessNotificationProps = {
    notification: NotificationData;
    anchorElRef?: React.RefObject<HTMLElement>;
    tooltipTags?: string[];
    theme?: any;
    className?: string;
};

type AverageHappinessNotificationProps = {
    notification: NotificationData;
    theme?: any;
    className?: string;
};

type ProfitabilityNotificationProps = {
    notification: NotificationData;
    theme?: any;
    className?: string;
};

type ConditionNotificationProps = {
    notification: NotificationData;
    anchorElRef?: React.RefObject<HTMLElement>;
    tooltipTags?: string[];
    theme?: any;
    className?: string;
};

type NotificationBadgeProps = {
    className?: string;
    children: ReactNode;
    theme?: any;
};

interface VehicleData {
    entity: string;
    id: string;
    thumbnail: string;
    name?: string;
}

type SelectVehiclesSectionProps = {
    group: string;
    tooltipKeys: string[];
    tooltipTags: string[];
    primaryVehicle?: VehicleData;
    secondaryVehicle?: VehicleData;
    primaryVehicles: VehicleData[];
    secondaryVehicles?: VehicleData[];
}


// COMPREHENSIVE REGISTRY INDEX
const registryIndex = {

    FOCUS_DISABLED: ["game-ui/common/focus/focus-key.ts", "FOCUS_DISABLED"],
    FOCUS_AUTO: ["game-ui/common/focus/focus-key.ts", "FOCUS_AUTO"],
    useUniqueFocusKey: ["game-ui/common/focus/focus-key.ts", "useUniqueFocusKey"],
    Section: ["game-ui/game/components/tool-options/mouse-tool-options/mouse-tool-options.tsx", "Section"],

    // VANILLA UI COMPONENTS DEOBSFUSCATED
    //de-ofuscated 
    CapacityBar: ["game-ui/game/components/selected-info-panel/shared-components/capacity-bar/capacity-bar.tsx", "CapacityBar"],
    CapacityBarTheme: ["game-ui/game/components/selected-info-panel/shared-components/capacity-bar/capacity-bar.module.scss", "classes"],
    //de-ofuscated
    Dropdown: ["game-ui/common/input/dropdown/dropdown.tsx", "Dropdown"],
    DropdownToggle: ["game-ui/common/input/dropdown/dropdown-toggle.tsx", "DropdownToggle"],
    DropdownItem: ["game-ui/common/input/dropdown/items/dropdown-item.tsx", "DropdownItem"],
    DropdownTheme: ["game-ui/common/input/dropdown/themes/default.module.scss", "classes"],
    //de-ofuscated
    InfoRow: ["game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.tsx", "InfoRow"],
    TooltipRow: ["game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.tsx", "TooltipRow"],
    InfoRowTheme: ["game-ui/game/components/selected-info-panel/shared-components/info-row/info-row.module.scss", "classes"],
    //de-ofuscated
    InfoSectionFoldout: ["game-ui/game/components/selected-info-panel/shared-components/info-section/info-section-foldout.tsx", "InfoSectionFoldout"],
    //de-ofuscated
    TrafficVolumeChart: ["game-ui/game/components/selected-info-panel/shared-components/traffic-charts/traffic-chart.tsx", "TrafficVolumeChart"],
    TrafficFlowChart: ["game-ui/game/components/selected-info-panel/shared-components/traffic-charts/traffic-chart.tsx", "TrafficFlowChart"],
    //de-ofuscated
    DescriptionRow: ["game-ui/game/components/selected-info-panel/shared-components/description-row/description-row.tsx", "DescriptionRow"],
    //de-ofuscated
    InfoButton: ["game-ui/game/components/selected-info-panel/shared-components/info-button/info-button.tsx", "InfoButton"],
    //de-ofuscated
    InfoButtonLink: ["game-ui/game/components/selected-info-panel/shared-components/info-link/info-link.tsx", "InfoLink"],
    //de-ofuscated
    InfoWrapBox: ["game-ui/game/components/selected-info-panel/shared-components/info-section/info-wrap-box.tsx", "InfoWrapBox"],
    //de-ofuscated
    Notification: ["game-ui/game/components/selected-info-panel/shared-components/notification/notification.tsx", "Notification"],
    HappinessNotification: ["game-ui/game/components/selected-info-panel/shared-components/notification/notification.tsx", "HappinessNotification"],
    AverageHappinessNotification: ["game-ui/game/components/selected-info-panel/shared-components/notification/notification.tsx", "AverageHappinessNotification"],
    ProfitabilityNotification: ["game-ui/game/components/selected-info-panel/shared-components/notification/notification.tsx", "ProfitabilityNotification"],
    ConditionNotification: ["game-ui/game/components/selected-info-panel/shared-components/notification/notification.tsx", "ConditionNotification"],
    NotificationBadge: ["game-ui/game/components/selected-info-panel/shared-components/notification/notification.tsx", "NotificationBadge"],
    NotificationTheme: ["game-ui/game/components/selected-info-panel/shared-components/notification/notification.module.scss", "classes"],
    //de-ofuscated
    SelectVehiclesSection: ["game-ui/game/components/selected-info-panel/selected-info-sections/route-sections/select-vehicles-section.tsx", "SelectVehiclesSection"]
};

export class CS2VanillaUIResolver {
    public static get instance(): CS2VanillaUIResolver { return this._instance!!; }
    private static _instance?: CS2VanillaUIResolver;

    public static setRegistry(in_registry: ModuleRegistry) { this._instance = new CS2VanillaUIResolver(in_registry); }
    private registryData: ModuleRegistry;

    constructor(in_registry: ModuleRegistry) {
        this.registryData = in_registry;
    }

    private cachedData: Partial<Record<keyof typeof registryIndex, any>> = {};
    private updateCache(entry: keyof typeof registryIndex) {
        const entryData = registryIndex[entry];
        return this.cachedData[entry] = this.registryData.registry.get(entryData[0])!![entryData[1]];
    }

    public get FOCUS_DISABLED(): UniqueFocusKey { return this.cachedData["FOCUS_DISABLED"] ?? this.updateCache("FOCUS_DISABLED") }
    public get FOCUS_AUTO(): UniqueFocusKey { return this.cachedData["FOCUS_AUTO"] ?? this.updateCache("FOCUS_AUTO") }
    public get useUniqueFocusKey(): (focusKey: FocusKey, debugName: string) => UniqueFocusKey | null { return this.cachedData["useUniqueFocusKey"] ?? this.updateCache("useUniqueFocusKey") }
    public get Section(): (props: PropsSection) => JSX.Element { return this.cachedData["Section"] ?? this.updateCache("Section") }

    // DE-OBFUSCATED COMPONENTS
    public get CapacityBar(): (props: CapacityBarProps) => JSX.Element { return this.cachedData["CapacityBar"] ?? this.updateCache("CapacityBar"); }
    public get CapacityBarTheme(): Theme | any { return this.cachedData["CapacityBarTheme"] ?? this.updateCache("CapacityBarTheme"); }
    public get Dropdown(): (props: DropdownProps) => JSX.Element { return this.cachedData["Dropdown"] ?? this.updateCache("Dropdown"); }
    public get DropdownToggle(): (props: DropdownToggleProps) => JSX.Element { return this.cachedData["DropdownToggle"] ?? this.updateCache("DropdownToggle"); }
    public get DropdownItem(): (props: DropdownItemProps) => JSX.Element { return this.cachedData["DropdownItem"] ?? this.updateCache("DropdownItem"); }  
    public get DropdownTheme(): Theme | any { return this.cachedData["DropdownTheme"] ?? this.updateCache("DropdownTheme"); }   
    public get InfoRow(): (props: InfoRowProps) => JSX.Element { return this.cachedData["InfoRow"] ?? this.updateCache("InfoRow"); }
    public get TooltipRow(): (props: TooltipRowProps) => JSX.Element { return this.cachedData["TooltipRow"] ?? this.updateCache("TooltipRow"); }
    public get InfoRowTheme(): Theme | any { return this.cachedData["InfoRowTheme"] ?? this.updateCache("InfoRowTheme"); }
    public get InfoSectionFoldout(): (props: InfoSectionFoldoutProps) => JSX.Element { return this.cachedData["InfoSectionFoldout"] ?? this.updateCache("InfoSectionFoldout"); }
    public get TrafficVolumeChart(): (props: TrafficChartProps) => JSX.Element { return this.cachedData["TrafficVolumeChart"] ?? this.updateCache("TrafficVolumeChart"); }
    public get TrafficFlowChart(): (props: TrafficChartProps) => JSX.Element { return this.cachedData["TrafficFlowChart"] ?? this.updateCache("TrafficFlowChart"); }
    public get DescriptionRow(): (props: DescriptionRowProps) => JSX.Element { return this.cachedData["DescriptionRow"] ?? this.updateCache("DescriptionRow"); }
    public get InfoButton(): (props: InfoButtonProps) => JSX.Element { return this.cachedData["InfoButton"] ?? this.updateCache("InfoButton"); }
    public get InfoButtonLink(): (props: InfoLinkProps) => JSX.Element { return this.cachedData["InfoButtonLink"] ?? this.updateCache("InfoButtonLink"); }  
    public get InfoWrapBox(): (props: InfoWrapBoxProps) => JSX.Element { return this.cachedData["InfoWrapBox"] ?? this.updateCache("InfoWrapBox"); }    
    public get Notification(): (props: NotificationProps) => JSX.Element { return this.cachedData["Notification"] ?? this.updateCache("Notification"); }
    public get HappinessNotification(): (props: HappinessNotificationProps) => JSX.Element { return this.cachedData["HappinessNotification"] ?? this.updateCache("HappinessNotification"); }
    public get AverageHappinessNotification(): (props: AverageHappinessNotificationProps) => JSX.Element { return this.cachedData["AverageHappinessNotification"] ?? this.updateCache("AverageHappinessNotification"); }
    public get ProfitabilityNotification(): (props: ProfitabilityNotificationProps) => JSX.Element { return this.cachedData["ProfitabilityNotification"] ?? this.updateCache("ProfitabilityNotification"); }
    public get ConditionNotification(): (props: ConditionNotificationProps) => JSX.Element { return this.cachedData["ConditionNotification"] ?? this.updateCache("ConditionNotification"); }
    public get NotificationBadge(): (props: NotificationBadgeProps) => JSX.Element { return this.cachedData["NotificationBadge"] ?? this.updateCache("NotificationBadge"); }
    public get NotificationTheme(): Theme | any { return this.cachedData["NotificationTheme"] ?? this.updateCache("NotificationTheme"); }
    public get SelectVehiclesSection(): (props: SelectVehiclesSectionProps) => JSX.Element { return this.cachedData["SelectVehiclesSection"] ?? this.updateCache("SelectVehiclesSection"); }

}