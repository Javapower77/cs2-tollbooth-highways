import React, { useState, useCallback, useMemo, useRef } from 'react';

// ========== rz(): CSS classes for the Select Vehicles Section ==========
export const SelectVehiclesSectionTheme = {
    dropdown: "dropdown_zWC",
    item: "item_Bmu",
    thumb: "thumb_eC2",
    label: "label_jXx",
    hint: "hint_hNH",
    "pad-left": "pad-left_jYb",
    padLeft: "pad-left_jYb",
    "dropdown-gamepad": "dropdown-gamepad_HFi",
    dropdownGamepad: "dropdown-gamepad_HFi",
    "pad-right": "pad-right_o6g",
    padRight: "pad-right_o6g",
    "pad-thumbnail": "pad-thumbnail_pWJ",
    padThumbnail: "pad-thumbnail_pWJ",
    selectVehiclesSection: "select-vehicles-section_main", // Added main section class
};

// Default dropdown sounds (equivalent to cz)
const DropdownSounds = {
    select: "selectItem",
    hover: "hoverItem",
    focus: "hoverItem"
};

// ========== Type definitions ==========
export interface VehicleData {
    entity: string;
    id: string;
    thumbnail: string;
    name?: string;
}

export interface SelectVehiclesSectionProps {
    group: string;
    tooltipKeys: string[];
    tooltipTags: string[];
    primaryVehicle?: VehicleData;
    secondaryVehicle?: VehicleData;
    primaryVehicles: VehicleData[];
    secondaryVehicles?: VehicleData[];
}

export interface VehicleItemProps {
    vehicle: VehicleData;
    onSelect: (entityId: string) => void;
}

// ========== Utility functions ==========
const classNames = (...classes: Array<string | undefined | null | false>) =>
    classes.filter(Boolean).join(" ").trim();

// Mock for game-specific entity ID resolver
const getEntityId = (entity: string): string => entity || "default";

// Mock for gamepad detection
const useGamepadDetection = (): boolean => {
    const [isGamepad] = useState(false); // In real implementation, this would detect gamepad
    return isGamepad;
};

// Mock localization context
const LocalizationContext = {
    SelectedInfoPanel: {
        SELECT_VEHICLE: () => "Select Vehicle",
        SELECT_VEHICLE_SECONDARY: ({ hash }: { hash: string }) => `Select ${hash}`,
    },
    Assets: {
        NAME: ({ hash }: { hash: string }) => hash || "Unknown Vehicle",
    }
};

// ========== uz(): Trigger vehicle selection ==========
function selectVehicles(primaryEntity: string, secondaryEntity: string): void {
    // Mock implementation - in real CS2 this would trigger game events
    console.log("SelectVehiclesSection: selectVehicles", primaryEntity, secondaryEntity);
    // In real implementation: trigger("SelectVehiclesSection", "selectVehicles", primaryEntity, secondaryEntity);
}

// ========== tO(): Generate tooltip text ==========
function useGeneratedTooltip(
    baseKey: string,
    tooltipKeys: string[],
    tooltipTags: string[],
    skipBase: boolean = false
): string[] | null {
    const tooltipRef = useRef<string[]>();

    // Mock localization function
    const translate = useCallback((key: string) => {
        return `Tooltip for ${key}`;
    }, []);

    // Mock context values
    const contextTags = useMemo(() => tooltipTags || [], [tooltipTags]);

    return useMemo(() => {
        const generateTooltipKey = (suffix: string) => baseKey + suffix;

        const tooltipStrings = [
            !skipBase && translate(baseKey),
            ...tooltipKeys.map(key => translate(generateTooltipKey(key))),
            ...contextTags.filter(tag => tooltipTags.includes(tag)).map(tag => translate(generateTooltipKey(tag))),
        ].filter(Boolean) as string[];

        if (!tooltipStrings.length) return null;

        const combined = tooltipStrings.join("");
        if (tooltipRef.current && combined === tooltipRef.current.join("")) {
            return tooltipRef.current;
        }

        tooltipRef.current = tooltipStrings;
        return tooltipStrings;
    }, [baseKey, tooltipKeys, tooltipTags, skipBase, translate, contextTags]);
}

// ========== UT(): Input Hint Component ==========
const InputHint: React.FC<{
    action: string;
    active?: boolean;
    controlScheme?: string;
    className?: string;
}> = ({ action, active = true, controlScheme = "gamepad", className }) => {
    const isGamepad = useGamepadDetection();

    if (!active || !action || !isGamepad) {
        return null;
    }

    return React.createElement("div", {
        className: classNames("hint-indicator", className),
    }, action);
};

// ========== Ah(): Focus Boundary Component ==========
const FocusBoundary: React.FC<{
    onFocusChange?: (focused: boolean) => void;
    disabled?: boolean;
    children: React.ReactNode;
}> = ({ onFocusChange, disabled = false, children }) => {
    const [focused, setFocused] = useState(false);

    const handleFocus = useCallback(() => {
        if (!disabled) {
            setFocused(true);
            onFocusChange?.(true);
        }
    }, [disabled, onFocusChange]);

    const handleBlur = useCallback(() => {
        if (!disabled) {
            setFocused(false);
            onFocusChange?.(false);
        }
    }, [disabled, onFocusChange]);

    return React.createElement("div", {
        onFocus: handleFocus,
        onBlur: handleBlur,
        tabIndex: disabled ? -1 : 0,
    }, children);
};

// ========== Basic Button Component ==========
const Button: React.FC<{
    sounds?: any;
    className?: string;
    onClick?: () => void;
    children: React.ReactNode;
}> = ({ sounds, className, onClick, children }) => {
    return React.createElement("button", {
        className: classNames("button", className),
        onClick: onClick,
    }, children);
};

// ========== Dropdown Item Component ==========
const DropdownItem: React.FC<{
    value: string;
    focusKey: string;
    onChange: (value: string) => void;
    className?: string;
    children: React.ReactNode;
}> = ({ value, focusKey, onChange, className, children }) => {
    const handleClick = useCallback(() => {
        onChange(value);
    }, [value, onChange]);

    return React.createElement("div", {
        className: classNames("dropdown-item", className),
        onClick: handleClick,
        "data-focus-key": focusKey,
    }, children);
};

// ========== Scrollable Container ==========
const ScrollableContainer: React.FC<{
    className?: string;
    children: React.ReactNode;
}> = ({ className, children }) => {
    return React.createElement("div", {
        className: classNames("scrollable-container", className),
        style: { overflowY: "auto", maxHeight: "300px" }
    }, children);
};

// ========== Auto Navigation Scope ==========
const AutoNavigationScope: React.FC<{
    focusKey?: string;
    initialFocused?: string;
    direction?: string;
    allowLooping?: boolean;
    children: React.ReactNode;
}> = ({ focusKey, initialFocused, direction, allowLooping, children }) => {
    return React.createElement("div", {
        className: "auto-navigation-scope",
        "data-focus-key": focusKey,
        "data-initial-focused": initialFocused,
        "data-direction": direction,
        "data-allow-looping": allowLooping,
    }, children);
};

// ========== Simple Popup Container ==========
const PopupContainer: React.FC<{
    visible: boolean;
    className?: string;
    content: React.ReactNode;
    minHeight?: number;
    minWidth?: boolean;
    alignment?: string;
    style?: any;
    children: React.ReactNode;
}> = ({ visible, className, content, minHeight, minWidth, alignment, style, children }) => {
    return React.createElement("div", {
        className: classNames("popup-container", className),
        style: { position: "relative", ...style }
    }, [
        children,
        visible && React.createElement("div", {
            key: "popup",
            className: "popup-content",
            style: {
                position: "absolute",
                top: "100%",
                left: 0,
                minHeight: minHeight,
                minWidth: minWidth ? "100%" : undefined,
                zIndex: 1000,
                backgroundColor: "rgba(0, 0, 0, 0.9)",
                border: "1px solid rgba(255, 255, 255, 0.2)",
                borderRadius: "4px",
                padding: "8px",
            }
        }, content)
    ]);
};

// ========== fS(): Dropdown Component ==========
const Dropdown: React.FC<{
    focusKey?: string;
    initialFocused?: string;
    theme?: any;
    content?: React.ReactNode;
    alignment?: string;
    children: React.ReactNode;
    onToggle?: (visible: boolean) => void;
}> = ({ focusKey, initialFocused, theme, content, alignment, children, onToggle }) => {
    const [isOpen, setIsOpen] = useState(false);
    const dropdownRef = useRef<HTMLDivElement>(null);

    const toggle = useCallback(() => {
        const newState = !isOpen;
        setIsOpen(newState);
        onToggle?.(newState);
    }, [isOpen, onToggle]);

    const hide = useCallback(() => {
        setIsOpen(false);
        onToggle?.(false);
    }, [onToggle]);

    // Close dropdown when clicking outside
    React.useEffect(() => {
        if (isOpen) {
            const handleClickOutside = (event: MouseEvent) => {
                if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
                    hide();
                }
            };

            document.addEventListener("mousedown", handleClickOutside);
            return () => document.removeEventListener("mousedown", handleClickOutside);
        }
    }, [isOpen, hide]);

    const dropdownContent = content && React.createElement("div", {
        className: theme?.dropdownMenu,
        ref: dropdownRef,
    }, React.createElement(ScrollableContainer, {
        className: theme?.scrollable,
        children: React.createElement(AutoNavigationScope, {
            focusKey: "dropdown-nav",
            initialFocused: initialFocused,
            direction: "Vertical",
            allowLooping: true,
            children: content,
        })
    }));

    return React.createElement(React.Fragment, {}, [
        isOpen && React.createElement("div", {
            key: "overlay",
            className: "dropdown-overlay",
            onClick: hide,
            style: {
                position: "fixed",
                top: 0,
                left: 0,
                right: 0,
                bottom: 0,
                zIndex: 999,
            }
        }),
        React.createElement("div", {
            key: "dropdown",
            className: "dropdown-container",
            onClick: toggle,
        }, React.createElement(PopupContainer, {
            visible: isOpen,
            className: theme?.dropdownPopup,
            content: dropdownContent,
            minHeight: 300,
            minWidth: true,
            alignment: alignment,
            children: content,
        }))
    ]);
};

// ========== KS(): Info Row Component ==========
const InfoRow: React.FC<{
    icon?: string;
    left?: React.ReactNode;
    right?: React.ReactNode;
    tooltip?: React.ReactNode;
    link?: React.ReactNode;
    uppercase?: boolean;
    subRow?: boolean;
    disableFocus?: boolean;
    className?: string;
    noShrinkRight?: boolean;
}> = ({ icon, left, right, tooltip, link, uppercase = false, subRow = false, disableFocus = false, className, noShrinkRight }) => {
    const rightClassName = right ? classNames(
        "info-row-right",
        noShrinkRight && "no-shrink",
        link ? "link" : false
    ) : undefined;

    const content = React.createElement("div", {
        className: classNames(
            "info-row",
            subRow && "sub-row",
            link ? "link" : false, 
            disableFocus && "disable-focus-highlight",
            className
        ),
    }, [
        icon && React.createElement("img", {
            key: "icon",
            src: icon,
            className: classNames("info-row-icon", link ? "link" : false),
            alt: ""
        }),
        left && React.createElement("div", {
            key: "left",
            className: classNames(
                "info-row-left",
                uppercase && "uppercase",
                link ? "link" : false, 
                icon && "has-icon"
            ),
        }, left),
        link && React.createElement("div", {
            key: "link",
            className: classNames("info-row-right", "link"),
        }, link),
        right && React.createElement("div", {
            key: "right",
            className: rightClassName,
        }, right),
    ]);

    if (tooltip) {
        return React.createElement(Tooltip, {
            content: tooltip,
            direction: "right",
            alignment: "center",
            children: content,
        });
    }

    return content;
};

// ========== yS(): Info Section Component ==========
const InfoSection: React.FC<{
    focusKey?: string;
    tooltip?: React.ReactNode;
    disableFocus?: boolean;
    className?: string;
    children: React.ReactNode;
}> = ({ focusKey, tooltip, disableFocus = false, className, children }) => {
    const content = disableFocus
        ? React.createElement("div", {
            className: classNames("info-section-content", "disable-focus-highlight"),
        }, React.createElement(AutoNavigationScope, { children }))
        : React.createElement(FocusBoundary, {
            children: React.createElement(AutoNavigationScope, { children })
        });

    const section = React.createElement("div", {
        className: classNames("info-section", className),
    }, content);

        if (tooltip) {
        return React.createElement(Tooltip, {
            content: tooltip,
            direction: "right",
            alignment: "center",
            children: section,
        }, section);
    }

    return section;
};

// ========== Simple Tooltip Component ==========
const Tooltip: React.FC<{
    content?: React.ReactNode;
    direction?: string;
    alignment?: string;
    children: React.ReactNode;
}> = ({ content, direction = "top", alignment = "center", children }) => {
    const [isVisible, setIsVisible] = useState(false);

    if (!content) {
        return React.createElement(React.Fragment, {}, children);
    }

    return React.createElement("div", {
        style: { position: "relative" },
        onMouseEnter: () => setIsVisible(true),
        onMouseLeave: () => setIsVisible(false),
    }, [
        children,
        isVisible && React.createElement("div", {
            key: "tooltip",
            style: {
                position: "absolute",
                top: direction === "bottom" ? "100%" : undefined,
                bottom: direction === "top" ? "100%" : undefined,
                left: alignment === "center" ? "50%" : "0",
                transform: alignment === "center" ? "translateX(-50%)" : undefined,
                backgroundColor: "rgba(0, 0, 0, 0.9)",
                color: "white",
                padding: "6px 8px",
                borderRadius: "4px",
                fontSize: "12px",
                whiteSpace: "nowrap",
                zIndex: 1001,
                border: "1px solid rgba(255, 255, 255, 0.2)",
                pointerEvents: "none",
            }
        }, content)
    ]);
};

// ========== Text Component for tooltips ==========
const FormattedText: React.FC<{
    text?: string[] | string;
    children?: React.ReactNode;
}> = ({ text, children }) => {
    const content = text
        ? (Array.isArray(text) ? text.join(" ") : text)
        : children;

    return React.createElement("div", {
        className: "formatted-text"
    }, content);
};

// ========== mz(): Vehicle Item Component ==========
const VehicleItem: React.FC<VehicleItemProps> = ({ vehicle, onSelect }) => {
    const [isFocused, setIsFocused] = useState(false);
    const isGamepad = useGamepadDetection();

    const handleSelect = useCallback(() => {
        onSelect(vehicle.entity);
    }, [vehicle.entity, onSelect]);

        return React.createElement(FocusBoundary, {
        onFocusChange: setIsFocused,
        children: React.createElement(DropdownItem, {
            value: vehicle.entity,
            focusKey: vehicle.id,
            onChange: onSelect,
            className: classNames(
                isGamepad && SelectVehiclesSectionTheme.padRight,
                isGamepad && !isFocused && SelectVehiclesSectionTheme.padLeft
            ),
            children: React.createElement("div", {
                className: SelectVehiclesSectionTheme.item,
            }, [
                React.createElement(InputHint, {
                    key: "hint",
                    action: "Select",
                    active: isFocused,
                    className: SelectVehiclesSectionTheme.hint,
                }),
                React.createElement("img", {
                    key: "thumb",
                    src: vehicle.thumbnail,
                    className: classNames(
                        SelectVehiclesSectionTheme.thumb,
                        isGamepad && SelectVehiclesSectionTheme.padThumbnail
                    ),
                    alt: vehicle.name || "Vehicle thumbnail"
                }),
                React.createElement("div", {
                    key: "label",
                    className: SelectVehiclesSectionTheme.label,
                }, [
                    React.createElement(LocalizationContext.Assets.NAME, {
                        key: "name",
                        hash: vehicle.id
                    }),
                    " "
                ])
            ])
        })
  });
};

// ========== dz(): Main Select Vehicles Section Component ==========
export const SelectVehiclesSection: React.FC<SelectVehiclesSectionProps> = ({
    group,
    tooltipKeys,
    tooltipTags,
    primaryVehicle,
    secondaryVehicle,
    primaryVehicles,
    secondaryVehicles
}) => {
    // Primary vehicle selection handler
    const handlePrimaryVehicleSelect = useCallback((entityId: string) => {
        selectVehicles(entityId, secondaryVehicle?.entity ?? "");
    }, [secondaryVehicle?.entity]);

    // Secondary vehicle selection handler  
    const handleSecondaryVehicleSelect = useCallback((entityId: string) => {
        selectVehicles(primaryVehicle?.entity ?? "", entityId);
    }, [primaryVehicle?.entity]);

    // Generate tooltip content
    const tooltipContent = useGeneratedTooltip(group, tooltipKeys, tooltipTags, true);

    // Determine current vehicles
    const currentPrimaryVehicle = primaryVehicle ?? primaryVehicles[0];
    const currentSecondaryVehicle = useMemo(
        () => (secondaryVehicle ? secondaryVehicle : (secondaryVehicles && secondaryVehicles.length > 0 ? secondaryVehicles[0] : null)),
        [secondaryVehicle, secondaryVehicles]
    );

    // State for dropdown interactions
    const [isPrimaryFocused, setIsPrimaryFocused] = useState(false);
    const [isPrimaryOpen, setIsPrimaryOpen] = useState(false);
    const isGamepad = useGamepadDetection();

    // Secondary vehicle focus state
    const [isSecondaryFocused, setIsSecondaryFocused] = useState(false);
    const [isSecondaryOpen, setIsSecondaryOpen] = useState(false);

    return React.createElement(
        InfoSection,
        {
            disableFocus: true,
            className: SelectVehiclesSectionTheme.selectVehiclesSection,
            tooltip: tooltipContent && React.createElement(FormattedText, { text: tooltipContent }),
            children: [
                // Primary vehicle section header
                React.createElement(
                    InfoRow,
                    {
                        key: "primary-header",
                        uppercase: true,
                        disableFocus: true,
                        left: React.createElement(LocalizationContext.SelectedInfoPanel.SELECT_VEHICLE, {})
                    }
                ),
                // Primary vehicle dropdown
                React.createElement(
                    InfoRow,
                    {
                        key: "primary-dropdown",
                        disableFocus: true,
                        className: classNames(
                            SelectVehiclesSectionTheme.dropdownGamepad,
                            isGamepad && SelectVehiclesSectionTheme.padRight,
                            isGamepad && !isPrimaryFocused && SelectVehiclesSectionTheme.padLeft
                        ),
                        left: React.createElement(React.Fragment, {},
                            [
                                React.createElement(
                                    InputHint,
                                    {
                                        key: "hint",
                                        action: isPrimaryOpen ? "Back" : "Select",
                                        active: isPrimaryFocused,
                                        className: SelectVehiclesSectionTheme.hint
                                    }
                                ),
                                React.createElement(
                                    FocusBoundary,
                                    {
                                        key: "focus",
                                        onFocusChange: setIsPrimaryFocused,
                                        children: React.createElement(Dropdown,
                                            {
                                                focusKey: "primary-vehicle-dropdown",
                                                theme: SelectVehiclesSectionTheme,
                                                initialFocused: getEntityId(currentPrimaryVehicle.entity),
                                                onToggle: setIsPrimaryOpen,
                                                content: primaryVehicles?.map((vehicle) =>
                                                    React.createElement(VehicleItem, {
                                                        key: getEntityId(vehicle.entity),
                                                        vehicle: vehicle,
                                                        onSelect: handlePrimaryVehicleSelect,
                                                    })
                                                ),
                                                children: React.createElement(Button, {
                                                    sounds: DropdownSounds,
                                                    className: SelectVehiclesSectionTheme.dropdown,
                                                    children: React.createElement("div",
                                                        {
                                                            className: SelectVehiclesSectionTheme.item,
                                                        },
                                                        [
                                                            React.createElement("img",
                                                                {
                                                                    key: "thumb",
                                                                    src: currentPrimaryVehicle.thumbnail,
                                                                    className: SelectVehiclesSectionTheme.thumb,
                                                                    alt: "Primary vehicle thumbnail"
                                                                }
                                                            ),
                                                            React.createElement("div",
                                                                {
                                                                    key: "label",
                                                                    className: SelectVehiclesSectionTheme.label,
                                                                },
                                                                [
                                                                    React.createElement(LocalizationContext.Assets.NAME, {
                                                                        key: "name",
                                                                        hash: currentPrimaryVehicle.id
                                                                    }),
                                                                    " "
                                                                ]
                                                            )
                                                        ]
                                                    )
                                                })
                                            }
                                        )
                                    }
                                )
                            ]
                        )
                    }
                ),

                // Secondary vehicle section (if available)
                currentSecondaryVehicle && React.createElement(React.Fragment, { key: "secondary-section" }, [
                    React.createElement(InfoRow, {
                        key: "secondary-header",
                        uppercase: true,
                        disableFocus: true,
                        left: React.createElement(LocalizationContext.SelectedInfoPanel.SELECT_VEHICLE_SECONDARY, {
                            hash: "Train"
                        }),
                    }),

                    React.createElement(InfoRow, {
                        key: "secondary-dropdown",
                        disableFocus: true,
                        className: classNames(
                            SelectVehiclesSectionTheme.dropdownGamepad,
                            isGamepad && SelectVehiclesSectionTheme.padRight,
                            isGamepad && !isSecondaryFocused && SelectVehiclesSectionTheme.padLeft
                        ),
                        left: React.createElement(React.Fragment, {}, [
                            React.createElement(InputHint, {
                                key: "hint",
                                action: isSecondaryOpen ? "Back" : "Select",
                                active: isSecondaryFocused,
                                className: SelectVehiclesSectionTheme.hint
                            }),
                            React.createElement(FocusBoundary, {
                                key: "focus",
                                onFocusChange: setIsSecondaryFocused,
                                children: React.createElement(Dropdown, {
                                    key: "secondary-dropdown",
                                    focusKey: "secondary-vehicle-dropdown",
                                    theme: SelectVehiclesSectionTheme,
                                    initialFocused: currentSecondaryVehicle?.entity ? getEntityId(currentSecondaryVehicle.entity) : undefined,
                                    onToggle: setIsSecondaryOpen,
                                    content: secondaryVehicles?.map((vehicle) =>
                                        React.createElement(VehicleItem, {
                                            key: getEntityId(vehicle.entity),
                                            vehicle: vehicle,
                                            onSelect: handleSecondaryVehicleSelect,
                                        })
                                    ),
                                    children: React.createElement(Button, {
                                        sounds: DropdownSounds,
                                        className: SelectVehiclesSectionTheme.dropdown,
                                        children: React.createElement("div", {
                                            className: SelectVehiclesSectionTheme.item,
                                        }, [
                                            React.createElement("img", {
                                                key: "thumb",
                                                src: currentSecondaryVehicle?.thumbnail || "",
                                                className: SelectVehiclesSectionTheme.thumb,
                                                alt: "Secondary vehicle thumbnail"
                                            }),
                                            React.createElement("div", {
                                                key: "label",
                                                className: SelectVehiclesSectionTheme.label,
                                            }, [
                                                currentSecondaryVehicle && React.createElement(LocalizationContext.Assets.NAME, {
                                                    key: "name",
                                                    hash: currentSecondaryVehicle.id
                                                }),
                                                " "
                                            ])
                                        ])
                                    })
                                })
                            })
                        ])
                    })
                ])
            ]
        }
    );
};

// Export all components and utilities
export {
    SelectVehiclesSection as default,
    DropdownSounds,
    selectVehicles,
    useGeneratedTooltip,
    InputHint,
    FocusBoundary,
    Dropdown,
    InfoRow,
    InfoSection,
    VehicleItem,
    FormattedText,
    Tooltip,
};