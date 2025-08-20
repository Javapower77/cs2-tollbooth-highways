import React, { ReactNode, forwardRef } from 'react';

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