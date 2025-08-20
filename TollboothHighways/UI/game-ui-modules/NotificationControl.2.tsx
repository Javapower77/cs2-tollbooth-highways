import React, { ReactNode, useRef, useCallback, useState, useEffect, useMemo, forwardRef } from 'react';

// CSS class utility function (equivalent to Zu() in obfuscated code)
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

// Default CSS classes theme (equivalent to vme in obfuscated code)
const defaultNotificationTheme = {
  notification: "notification_CLy item-focused_FuT",
  "full-width": "full-width_Qk1",
  fullWidth: "full-width_Qk1",
  icon: "icon_UMr",
  label: "label_RLF",
  badge: "badge_ooc",
  "badge-icon": "badge-icon_ubF",
  badgeIcon: "badge-icon_ubF",
};

// Mock localization dictionary (equivalent to _c in obfuscated code)
const localizationDict = {
  Notifications: {
    DESCRIPTION: ({ hash }: { hash: string }) => `Notification description for ${hash}`,
    TITLE: ({ hash }: { hash: string }) => `Notification: ${hash}`,
  },
  SelectedInfoPanel: {
    CITIZEN_HAPPINESS_DESCRIPTION: ({ hash }: { hash: string }) => `Happiness description for ${hash}`,
    CITIZEN_HAPPINESS_TITLE_MALE: ({ hash }: { hash: string }) => `Male happiness: ${hash}`,
    CITIZEN_HAPPINESS_TITLE_FEMALE: ({ hash }: { hash: string }) => `Female happiness: ${hash}`,
    CITIZEN_HAPPINESS_TITLE: ({ hash }: { hash: string }) => `Citizen happiness: ${hash}`,
    COMPANY_PROFITABILITY_TITLE: ({ hash }: { hash: string }) => `Company profit: ${hash}`,
    CITIZEN_CONDITION_DESCRIPTION: ({ hash }: { hash: string }) => `Condition description for ${hash}`,
    CITIZEN_CONDITION_TITLE_MALE: ({ hash }: { hash: string }) => `Male condition: ${hash}`,
    CITIZEN_CONDITION_TITLE_FEMALE: ({ hash }: { hash: string }) => `Female condition: ${hash}`,
  },
};

// Mock components for CS2 integration

// Tinted Icon component (equivalent to Z_ in obfuscated code)
const TintedIcon = forwardRef<HTMLDivElement, { 
  src: string; 
  className?: string; 
  style?: React.CSSProperties; 
  [key: string]: any 
}>(({ src, className, style, ...props }, ref) => (
  <div
    {...props}
    ref={ref}
    className={classNames(src && "tintedIcon", className)}
    style={{ 
      ...style, 
      maskImage: src ? `url(${src})` : undefined,
      WebkitMaskImage: src ? `url(${src})` : undefined
    }}
  />
));

// Active Focus Div component (equivalent to Sp in obfuscated code)
const ActiveFocusDiv = forwardRef<HTMLDivElement, {
  focusKey?: string;
  debugName?: string;
  activation?: string;
  focusSound?: string;
  className?: string;
  children: ReactNode;
  onFocusChange?: (focused: boolean) => void;
  [key: string]: any;
}>(({ 
  focusKey = "AUTO", 
  debugName = "ActiveFocusDiv", 
  activation = "Always", 
  focusSound, 
  className, 
  children, 
  onFocusChange, 
  ...props 
}, ref) => {
  const [focused, setFocused] = useState(false);

  const handleFocus = useCallback(() => {
    setFocused(true);
    onFocusChange?.(true);
  }, [onFocusChange]);

  const handleBlur = useCallback(() => {
    setFocused(false);
    onFocusChange?.(false);
  }, [onFocusChange]);

  return (
    <div
      {...props}
      ref={ref}
      className={classNames(className, focused && "focused")}
      onFocus={handleFocus}
      onBlur={handleBlur}
      onMouseEnter={handleFocus}
      onMouseLeave={handleBlur}
      tabIndex={0}
    >
      {children}
    </div>
  );
});

// Formatted Paragraphs component (equivalent to Cb in obfuscated code)
const FormattedParagraphs: React.FC<{
  focusKey?: string;
  text?: string;
  theme?: any;
  renderer?: any;
  className?: string;
  children?: ReactNode;
  onLinkSelect?: (link: string) => void;
  selectAction?: string;
  maxLineLength?: number;
  splitLineLength?: number;
}> = ({ 
  focusKey, 
  text, 
  theme, 
  renderer, 
  className, 
  children, 
  onLinkSelect, 
  selectAction, 
  maxLineLength, 
  splitLineLength,
  ...props 
}) => {
  const content = children || text || "";
  
  return (
    <div {...props} className={classNames("paragraphs", className)}>
      <div>
        {content}
      </div>
    </div>
  );
};

// Tooltip component (equivalent to Eg in obfuscated code)
const Tooltip: React.FC<{
  tooltip?: ReactNode;
  forceVisible?: boolean;
  disabled?: boolean;
  theme?: any;
  direction?: string;
  alignment?: string;
  className?: string;
  children: ReactNode;
  anchorElRef?: React.RefObject<HTMLElement>;
}> = ({ 
  tooltip, 
  forceVisible = false, 
  disabled = false, 
  theme, 
  direction = "top", 
  alignment = "center", 
  className, 
  children, 
  anchorElRef 
}) => {
  const [visible, setVisible] = useState(false);
  const [focused, setFocused] = useState(false);

  const handleMouseEnter = useCallback(() => setVisible(true), []);
  const handleMouseLeave = useCallback(() => setVisible(false), []);
  const handleMouseDown = useCallback(() => setVisible(false), []);

  const shouldShowTooltip = !!tooltip && ((visible || focused || forceVisible) && !disabled);

  return (
    <div className="tooltip-wrapper">
      <div
        onMouseEnter={handleMouseEnter}
        onMouseLeave={handleMouseLeave}
        onMouseDown={handleMouseDown}
        onFocus={() => setFocused(true)}
        onBlur={() => setFocused(false)}
      >
        {children}
      </div>
      {shouldShowTooltip && (
        <div 
          className={classNames("tooltip", `tooltip-${direction}`, `tooltip-${alignment}`, className)}
          style={{
            position: 'absolute',
            zIndex: 1000,
            background: 'rgba(0, 0, 0, 0.8)',
            color: 'white',
            padding: '8px',
            borderRadius: '4px',
            fontSize: '14px',
            maxWidth: '300px',
          }}
        >
          {tooltip}
        </div>
      )}
    </div>
  );
};

// Number formatter component (equivalent to Oc in obfuscated code)
const NumberFormatter: React.FC<{ value: number }> = ({ value }) => {
  return <span>{value.toLocaleString()}</span>;
};

// Interfaces
interface NotificationData {
  key: string;
  iconPath: string;
  count?: number;
}

interface NotificationProps {
  notification: NotificationData;
  anchorElRef?: React.RefObject<HTMLElement>;
  tooltipTags?: string[];
}

// Notification Badge Component (equivalent to yme in obfuscated code)
export const NotificationBadge: React.FC<{
  className?: string;
  children: ReactNode;
}> = ({ className, children }) => (
  <div className={classNames(defaultNotificationTheme.badge, className)}>
    {children}
    <TintedIcon
      src="Media/Game/Icons/Citizen.svg"
      className={defaultNotificationTheme.badgeIcon}
    />
  </div>
);

// Basic Notification Component (equivalent to bme in obfuscated code)
export const Notification: React.FC<NotificationProps> = ({ 
  notification, 
  anchorElRef 
}) => (
  <Tooltip
    direction="right"
    alignment="center"
    anchorElRef={anchorElRef}
    tooltip={
      <FormattedParagraphs>
        <localizationDict.Notifications.DESCRIPTION hash={notification.key} />
      </FormattedParagraphs>
    }
  >
    <ActiveFocusDiv className={classNames(
      defaultNotificationTheme.notification, 
      defaultNotificationTheme.fullWidth
    )}>
      <img src={notification.iconPath} className={defaultNotificationTheme.icon} alt="" />
      <div className={defaultNotificationTheme.label}>
        <localizationDict.Notifications.TITLE hash={notification.key} />
        {notification.count && notification.count > 1 && (
          <NotificationBadge className={defaultNotificationTheme.badge}>
            <NumberFormatter value={notification.count} />
          </NotificationBadge>
        )}
      </div>
    </ActiveFocusDiv>
  </Tooltip>
);

// Happiness Notification Component (equivalent to Tme in obfuscated code)
export const HappinessNotification: React.FC<NotificationProps> = ({ 
  notification, 
  anchorElRef, 
  tooltipTags 
}) => {
  const isMale = tooltipTags?.includes("Male");
  
  return (
    <Tooltip
      direction="right"
      alignment="center"
      anchorElRef={anchorElRef}
      tooltip={
        <FormattedParagraphs>
          <localizationDict.SelectedInfoPanel.CITIZEN_HAPPINESS_DESCRIPTION 
            hash={notification.key} 
          />
        </FormattedParagraphs>
      }
    >
      <ActiveFocusDiv className={defaultNotificationTheme.notification}>
        <img src={notification.iconPath} className={defaultNotificationTheme.icon} alt="" />
        <div className={defaultNotificationTheme.label}>
          {isMale ? (
            <localizationDict.SelectedInfoPanel.CITIZEN_HAPPINESS_TITLE_MALE 
              hash={notification.key} 
            />
          ) : (
            <localizationDict.SelectedInfoPanel.CITIZEN_HAPPINESS_TITLE_FEMALE 
              hash={notification.key} 
            />
          )}
        </div>
      </ActiveFocusDiv>
    </Tooltip>
  );
};

// Average Happiness Notification Component (equivalent to Eme in obfuscated code)
export const AverageHappinessNotification: React.FC<{ notification: NotificationData }> = ({ 
  notification 
}) => (
  <ActiveFocusDiv className={defaultNotificationTheme.notification}>
    <img src={notification.iconPath} className={defaultNotificationTheme.icon} alt="" />
    <div className={defaultNotificationTheme.label}>
      <localizationDict.SelectedInfoPanel.CITIZEN_HAPPINESS_TITLE hash={notification.key} />
    </div>
  </ActiveFocusDiv>
);

// Profitability Notification Component (equivalent to Ime in obfuscated code)
export const ProfitabilityNotification: React.FC<{ notification: NotificationData }> = ({ 
  notification 
}) => (
  <ActiveFocusDiv className={defaultNotificationTheme.notification}>
    <img src={notification.iconPath} className={defaultNotificationTheme.icon} alt="" />
    <div className={defaultNotificationTheme.label}>
      <localizationDict.SelectedInfoPanel.COMPANY_PROFITABILITY_TITLE hash={notification.key} />
    </div>
  </ActiveFocusDiv>
);

// Condition Notification Component (equivalent to Sme in obfuscated code)
export const ConditionNotification: React.FC<NotificationProps> = ({ 
  notification, 
  anchorElRef, 
  tooltipTags 
}) => {
  const isMale = tooltipTags?.includes("Male");
  
  return (
    <Tooltip
      direction="right"
      alignment="center"
      anchorElRef={anchorElRef}
      tooltip={
        <FormattedParagraphs>
          <localizationDict.SelectedInfoPanel.CITIZEN_CONDITION_DESCRIPTION 
            hash={notification.key} 
          />
        </FormattedParagraphs>
      }
    >
      <ActiveFocusDiv className={defaultNotificationTheme.notification}>
        <img src={notification.iconPath} className={defaultNotificationTheme.icon} alt="" />
        <div className={defaultNotificationTheme.label}>
          {isMale ? (
            <localizationDict.SelectedInfoPanel.CITIZEN_CONDITION_TITLE_MALE 
              hash={notification.key} 
            />
          ) : (
            <localizationDict.SelectedInfoPanel.CITIZEN_CONDITION_TITLE_FEMALE 
              hash={notification.key} 
            />
          )}
        </div>
      </ActiveFocusDiv>
    </Tooltip>
  );
};

// Enhanced interfaces
interface NotificationTheme {
    notification: string;
    fullWidth: string;
    icon: string;
    label: string;
    badge: string;
    badgeIcon: string;
}

interface EnhancedNotificationData {
    key: string;
    iconPath: string;
    count?: number;
    priority?: 'low' | 'medium' | 'high' | 'critical';
    timestamp?: Date;
    persistent?: boolean;
}

interface EnhancedNotificationProps {
    notification: EnhancedNotificationData;
    anchorElRef?: React.RefObject<HTMLElement>;
    tooltipTags?: string[];
    theme?: Partial<NotificationTheme>;
    onClick?: () => void;
    onDismiss?: () => void;
    'data-testid'?: string;
}

// Enhanced notification theme with priority support
const enhancedNotificationTheme: NotificationTheme = {
    notification: "notification",
    fullWidth: "full-width",
    icon: "notification-icon",
    label: "notification-label",
    badge: "notification-badge",
    badgeIcon: "notification-badge-icon",
};

// Priority-based styling
const getPriorityClasses = (priority?: string) => {
    switch (priority) {
        case 'critical': return 'notification-critical';
        case 'high': return 'notification-high';
        case 'medium': return 'notification-medium';
        case 'low': return 'notification-low';
        default: return '';
    }
};

// Enhanced Notification with priority and accessibility
export const EnhancedNotification: React.FC<EnhancedNotificationProps> = ({
    notification,
    anchorElRef,
    tooltipTags,
    theme,
    onClick,
    onDismiss,
    'data-testid': testId
}) => {
    const resolvedTheme = { ...enhancedNotificationTheme, ...theme };
    const priorityClass = getPriorityClasses(notification.priority);

    return (
        <Tooltip
            direction="right"
            alignment="center"
            anchorElRef={anchorElRef}
            tooltip={
                <FormattedParagraphs>
                    <div>
                        <strong>{notification.key}</strong>
                        <p>Priority: {notification.priority || 'normal'}</p>
                        {notification.timestamp && (
                            <p>Time: {notification.timestamp.toLocaleTimeString()}</p>
                        )}
                    </div>
                </FormattedParagraphs>
            }
        >
            <ActiveFocusDiv
                className={classNames(
                    resolvedTheme.notification,
                    priorityClass,
                    onClick && 'clickable'
                )}
                onClick={onClick}
                data-testid={testId}
                role="alert"
                aria-label={`Notification: ${notification.key}`}
            >
                <img
                    src={notification.iconPath}
                    className={resolvedTheme.icon}
                    alt=""
                    aria-hidden="true"
                />
                <div className={resolvedTheme.label}>
                    <span>{notification.key}</span>
                    {notification.count && notification.count > 1 && (
                        <NotificationBadge className={resolvedTheme.badge}>
                            <NumberFormatter value={notification.count} />
                        </NotificationBadge>
                    )}
                </div>
                {onDismiss && (
                    <button
                        onClick={(e) => {
                            e.stopPropagation();
                            onDismiss();
                        }}
                        className="notification-dismiss"
                        aria-label="Dismiss notification"
                    >
                        ×
                    </button>
                )}
            </ActiveFocusDiv>
        </Tooltip>
    );
};

// Notification Container for managing multiple notifications
export const NotificationContainer: React.FC<{
    notifications: EnhancedNotificationData[];
    maxVisible?: number;
    onNotificationClick?: (notification: EnhancedNotificationData) => void;
    onNotificationDismiss?: (notificationKey: string) => void;
    className?: string;
}> = ({
    notifications,
    maxVisible = 5,
    onNotificationClick,
    onNotificationDismiss,
    className
}) => {
        const sortedNotifications = [...notifications]
            .sort((a, b) => {
                const priorityOrder = { critical: 4, high: 3, medium: 2, low: 1 };
                const aPriority = priorityOrder[a.priority || 'medium'] || 2;
                const bPriority = priorityOrder[b.priority || 'medium'] || 2;
                return bPriority - aPriority;
            })
            .slice(0, maxVisible);

        return (
            <div className={classNames('notification-container', className)}>
                {sortedNotifications.map((notification) => (
                    <EnhancedNotification
                        key={notification.key}
                        notification={notification}
                        onClick={() => onNotificationClick?.(notification)}
                        onDismiss={() => onNotificationDismiss?.(notification.key)}
                        data-testid={`notification-${notification.key}`}
                    />
                ))}
                {notifications.length > maxVisible && (
                    <div className="notification-overflow">
                        +{notifications.length - maxVisible} more notifications
                    </div>
                )}
            </div>
        );
    };

// Export theme and utilities
export { 
  defaultNotificationTheme, 
  classNames,
  localizationDict
};

// Default export
export default {
  Notification,
  HappinessNotification,
  AverageHappinessNotification,
  ProfitabilityNotification,
  ConditionNotification,
  NotificationBadge,
  EnhancedNotification,
  NotificationContainer
};