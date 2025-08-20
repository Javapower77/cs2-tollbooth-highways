import React, { useState } from 'react';
import { getModule } from "cs2/modding";
import { useValue, bindValue } from "cs2/api";
import { CS2VanillaUIResolver } from "mods/CS2VanillaUIResolver";

// Import the deobfuscated NotificationsSection component
const { NotificationsSection } = getModule(
    "game-ui/game/components/selected-info-panel/selected-info-sections/shared-sections/notification-sections/notifications-section.tsx",
    "NotificationsSection"
);

// Example notification data structure based on the Cities Skylines 2 format
interface Notification {
    key: string;
    iconPath: string;
    count?: number;
}

// Example component showing NotificationsSection usage
export const TollboothNotificationsExample = () => {
    // Sample notification data for different scenarios
    const [notifications, setNotifications] = useState<Notification[]>([
        {
            key: "tollbooth_maintenance_required",
            iconPath: "Media/Game/Icons/MaintenanceVehicle.svg",
            count: 2
        },
        {
            key: "high_traffic_congestion",
            iconPath: "Media/Game/Icons/Traffic.svg",
            count: 5
        },
        {
            key: "citizen_complaints_toll_rates",
            iconPath: "Media/Game/Icons/Unhappy.svg",
            count: 12
        },
        {
            key: "revenue_target_exceeded",
            iconPath: "Media/Game/Icons/Money.svg",
            count: 1
        },
        {
            key: "equipment_malfunction",
            iconPath: "Media/Game/Icons/Warning.svg",
            count: 3
        }
    ]);

    return (
        <CS2VanillaUIResolver.instance.InfoSection focusKey="tollbooth-notifications" disableFocus={true}>
            <CS2VanillaUIResolver.instance.InfoRow
                left="Tollbooth Status"
                tooltip="Current notifications and alerts for this tollbooth"
                uppercase={true}
                disableFocus={true}
                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
            />
            
            {/* Using the deobfuscated NotificationsSection */}
            <NotificationsSection
                focusKey="notifications-container"
                notifications={notifications}
            />
        </CS2VanillaUIResolver.instance.InfoSection>
    );
};

// Example showing dynamic notification updates
export const DynamicNotificationsExample = () => {
    const [activeNotifications, setActiveNotifications] = useState<Notification[]>([]);

    // Function to add a new notification
    const addNotification = (notification: Notification) => {
        setActiveNotifications(prev => {
            const existing = prev.find(n => n.key === notification.key);
            if (existing) {
                // Update count if notification already exists
                return prev.map(n => 
                    n.key === notification.key 
                        ? { ...n, count: (n.count || 0) + (notification.count || 1) }
                        : n
                );
            } else {
                // Add new notification
                return [...prev, notification];
            }
        });
    };

    // Function to remove a notification
    const removeNotification = (key: string) => {
        setActiveNotifications(prev => prev.filter(n => n.key !== key));
    };

    // Function to clear all notifications
    const clearAllNotifications = () => {
        setActiveNotifications([]);
    };

    return (
        <CS2VanillaUIResolver.instance.InfoSection focusKey="dynamic-notifications" disableFocus={true}>
            <CS2VanillaUIResolver.instance.InfoRow
                left="Dynamic Notifications"
                tooltip="Notifications that update in real-time based on tollbooth conditions"
                uppercase={true}
                disableFocus={true}
                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
            />

            {/* Control buttons for demonstration */}
            <div style={{ display: 'flex', gap: '8px', marginBottom: '12px', flexWrap: 'wrap' }}>
                <CS2VanillaUIResolver.instance.Button
                    variant="round"
                    onClick={() => addNotification({
                        key: "traffic_jam_detected",
                        iconPath: "Media/Game/Icons/Traffic.svg",
                        count: 1
                    })}
                >
                    Add Traffic Alert
                </CS2VanillaUIResolver.instance.Button>

                <CS2VanillaUIResolver.instance.Button
                    variant="round"
                    onClick={() => addNotification({
                        key: "maintenance_due",
                        iconPath: "Media/Game/Icons/MaintenanceVehicle.svg",
                        count: 1
                    })}
                >
                    Add Maintenance Alert
                </CS2VanillaUIResolver.instance.Button>

                <CS2VanillaUIResolver.instance.Button
                    variant="round"
                    onClick={clearAllNotifications}
                >
                    Clear All
                </CS2VanillaUIResolver.instance.Button>
            </div>

            {/* NotificationsSection with dynamic data */}
            <NotificationsSection
                focusKey="dynamic-notifications-container"
                notifications={activeNotifications}
            />

            {/* Show message when no notifications */}
            {activeNotifications.length === 0 && (
                <div style={{ 
                    padding: '16px', 
                    textAlign: 'center', 
                    opacity: 0.6,
                    fontStyle: 'italic'
                }}>
                    No active notifications
                </div>
            )}
        </CS2VanillaUIResolver.instance.InfoSection>
    );
};

// Example showing different notification categories
export const CategorizedNotificationsExample = () => {
    const operationalNotifications: Notification[] = [
        {
            key: "booth_operational",
            iconPath: "Media/Game/Icons/CheckCircle.svg",
            count: 1
        },
        {
            key: "processing_efficiency_high",
            iconPath: "Media/Game/Icons/Efficiency.svg",
            count: 1
        }
    ];

    const warningNotifications: Notification[] = [
        {
            key: "revenue_below_target",
            iconPath: "Media/Game/Icons/Warning.svg",
            count: 2
        },
        {
            key: "queue_length_increasing",
            iconPath: "Media/Game/Icons/Traffic.svg",
            count: 7
        }
    ];

    const criticalNotifications: Notification[] = [
        {
            key: "system_failure",
            iconPath: "Media/Game/Icons/Error.svg",
            count: 1
        },
        {
            key: "emergency_maintenance_required",
            iconPath: "Media/Game/Icons/Emergency.svg",
            count: 1
        }
    ];

    return (
        <>
            {/* Operational Status */}
            <CS2VanillaUIResolver.instance.InfoSection focusKey="operational-notifications" disableFocus={true}>
                <CS2VanillaUIResolver.instance.InfoRow
                    left="Operational Status"
                    tooltip="Normal operational notifications"
                    uppercase={true}
                    disableFocus={true}
                    className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                />
                <NotificationsSection
                    focusKey="operational-notifications-container"
                    notifications={operationalNotifications}
                />
            </CS2VanillaUIResolver.instance.InfoSection>

            {/* Warnings */}
            <CS2VanillaUIResolver.instance.InfoSection focusKey="warning-notifications" disableFocus={true}>
                <CS2VanillaUIResolver.instance.InfoRow
                    left="Warnings"
                    tooltip="Issues that require attention"
                    uppercase={true}
                    disableFocus={true}
                    className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                />
                <NotificationsSection
                    focusKey="warning-notifications-container"
                    notifications={warningNotifications}
                />
            </CS2VanillaUIResolver.instance.InfoSection>

            {/* Critical Alerts */}
            <CS2VanillaUIResolver.instance.InfoSection focusKey="critical-notifications" disableFocus={true}>
                <CS2VanillaUIResolver.instance.InfoRow
                    left="Critical Alerts"
                    tooltip="Urgent issues requiring immediate action"
                    uppercase={true}
                    disableFocus={true}
                    className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                />
                <NotificationsSection
                    focusKey="critical-notifications-container"
                    notifications={criticalNotifications}
                />
            </CS2VanillaUIResolver.instance.InfoSection>
        </>
    );
};

// Example showing integration with game state
export const GameStateNotificationsExample = () => {
    // Bind to actual game data (example)
    const tollboothNotifications$ = bindValue<Notification[]>("your-mod-id", "tollbooth_notifications");
    const notifications = useValue(tollboothNotifications$) || [];

    return (
        <CS2VanillaUIResolver.instance.InfoSection focusKey="game-state-notifications" disableFocus={true}>
            <CS2VanillaUIResolver.instance.InfoRow
                left="Live Notifications"
                tooltip="Real-time notifications from the game engine"
                uppercase={true}
                disableFocus={true}
                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
            />
            
            <NotificationsSection
                focusKey="game-state-notifications-container"
                notifications={notifications}
            />
            
            {/* Display notification count */}
            <CS2VanillaUIResolver.instance.InfoRow
                left="Active Notifications"
                right={notifications.length.toString()}
                uppercase={true}
                disableFocus={true}
                className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
            />
        </CS2VanillaUIResolver.instance.InfoSection>
    );
};

// Main component combining all examples
export const TollboothNotificationsPanel = () => {
    const [selectedExample, setSelectedExample] = useState<string>('static');

    const renderSelectedExample = () => {
        switch (selectedExample) {
            case 'static':
                return <TollboothNotificationsExample />;
            case 'dynamic':
                return <DynamicNotificationsExample />;
            case 'categorized':
                return <CategorizedNotificationsExample />;
            case 'gamestate':
                return <GameStateNotificationsExample />;
            default:
                return <TollboothNotificationsExample />;
        }
    };

    return (
        <>
            {/* Example selector */}
            <CS2VanillaUIResolver.instance.InfoSection focusKey="example-selector" disableFocus={true}>
                <CS2VanillaUIResolver.instance.InfoRow
                    left="Notification Examples"
                    tooltip="Select different notification usage examples"
                    uppercase={true}
                    disableFocus={true}
                    className={CS2VanillaUIResolver.instance.InfoRowTheme.infoRow}
                />
                
                <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', padding: '8px 0' }}>
                    <CS2VanillaUIResolver.instance.Button
                        variant={selectedExample === 'static' ? 'primary' : 'default'}
                        onClick={() => setSelectedExample('static')}
                    >
                        Static
                    </CS2VanillaUIResolver.instance.Button>
                    
                    <CS2VanillaUIResolver.instance.Button
                        variant={selectedExample === 'dynamic' ? 'primary' : 'default'}
                        onClick={() => setSelectedExample('dynamic')}
                    >
                        Dynamic
                    </CS2VanillaUIResolver.instance.Button>
                    
                    <CS2VanillaUIResolver.instance.Button
                        variant={selectedExample === 'categorized' ? 'primary' : 'default'}
                        onClick={() => setSelectedExample('categorized')}
                    >
                        Categorized
                    </CS2VanillaUIResolver.instance.Button>
                    
                    <CS2VanillaUIResolver.instance.Button
                        variant={selectedExample === 'gamestate' ? 'primary' : 'default'}
                        onClick={() => setSelectedExample('gamestate')}
                    >
                        Game State
                    </CS2VanillaUIResolver.instance.Button>
                </div>
            </CS2VanillaUIResolver.instance.InfoSection>

            {/* Render selected example */}
            {renderSelectedExample()}
        </>
    );
};