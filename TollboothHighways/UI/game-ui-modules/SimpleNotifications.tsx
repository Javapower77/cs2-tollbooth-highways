import React, { useState, useCallback } from 'react';

// Default CSS classes theme
const NotificationCSS = {
  notification: "notification_CLy item-focused_FuT",
  fullWidth: "full-width_Qk1",
  icon: "icon_UMr",
  label: "label_RLF",
  badge: "badge_ooc",
  badgeIcon: "badge-icon_ubF",
};

// Component interfaces
interface NotificationData {
  key: string;
  iconPath: string;
  count?: number;
}

interface NotificationProps {
  notification: NotificationData;
  anchorElRef?: React.RefObject<HTMLElement>;
  tooltipTags?: string[];
  className?: string;
}

// Simple implementations
const SimpleNotification: React.FC<NotificationProps> = ({
  notification,
  className
}) => {
  const [focused, setFocused] = useState(false);

  return React.createElement("div", {
    className: [NotificationCSS.notification, NotificationCSS.fullWidth, className]
      .filter(Boolean).join(' '),
    onFocus: () => setFocused(true),
    onBlur: () => setFocused(false),
    tabIndex: 0,
    "data-focused": focused
  }, [
    React.createElement("img", {
      src: notification.iconPath,
      className: NotificationCSS.icon,
      key: "icon",
      alt: ""
    }),
    React.createElement("div", {
      className: NotificationCSS.label,
      key: "label"
    }, [
      React.createElement("span", {
        key: "title"
      }, `Notification: ${notification.key}`),
      notification.count && notification.count > 1 && React.createElement("div", {
        className: NotificationCSS.badge,
        key: "badge"
      }, [
        notification.count.toString(),
        React.createElement("img", {
          src: "Media/Game/Icons/Citizen.svg",
          className: NotificationCSS.badgeIcon,
          key: "badge-icon",
          alt: ""
        })
      ])
    ])
  ]);
};

const SimpleHappinessNotification: React.FC<NotificationProps> = ({
  notification,
  tooltipTags,
  className
}) => {
  const isMale = tooltipTags?.includes("Male");
  const [focused, setFocused] = useState(false);

  return React.createElement("div", {
    className: [NotificationCSS.notification, className].filter(Boolean).join(' '),
    onFocus: () => setFocused(true),
    onBlur: () => setFocused(false),
    tabIndex: 0,
    "data-focused": focused,
    title: `Happiness description for ${notification.key}`
  }, [
    React.createElement("img", {
      src: notification.iconPath,
      className: NotificationCSS.icon,
      key: "icon",
      alt: ""
    }),
    React.createElement("div", {
      className: NotificationCSS.label,
      key: "label"
    }, `${isMale ? 'Male' : 'Female'} happiness: ${notification.key}`)
  ]);
};

const SimpleConditionNotification: React.FC<NotificationProps> = ({
  notification,
  tooltipTags,
  className
}) => {
  const isMale = tooltipTags?.includes("Male");
  const [focused, setFocused] = useState(false);

  return React.createElement("div", {
    className: [NotificationCSS.notification, className].filter(Boolean).join(' '),
    onFocus: () => setFocused(true),
    onBlur: () => setFocused(false),
    tabIndex: 0,
    "data-focused": focused,
    title: `Condition description for ${notification.key}`
  }, [
    React.createElement("img", {
      src: notification.iconPath,
      className: NotificationCSS.icon,
      key: "icon",
      alt: ""
    }),
    React.createElement("div", {
      className: NotificationCSS.label,
      key: "label"
    }, `${isMale ? 'Male' : 'Female'} condition: ${notification.key}`)
  ]);
};

const SimpleProfitabilityNotification: React.FC<NotificationProps> = ({
  notification,
  className
}) => {
  const [focused, setFocused] = useState(false);

  return React.createElement("div", {
    className: [NotificationCSS.notification, className].filter(Boolean).join(' '),
    onFocus: () => setFocused(true),
    onBlur: () => setFocused(false),
    tabIndex: 0,
    "data-focused": focused
  }, [
    React.createElement("img", {
      src: notification.iconPath,
      className: NotificationCSS.icon,
      key: "icon",
      alt: ""
    }),
    React.createElement("div", {
      className: NotificationCSS.label,
      key: "label"
    }, `Profitability: ${notification.key}`)
  ]);
};

const SimpleAverageHappinessNotification: React.FC<NotificationProps> = ({
  notification,
  className
}) => {
  const [focused, setFocused] = useState(false);

  return React.createElement("div", {
    className: [NotificationCSS.notification, className].filter(Boolean).join(' '),
    onFocus: () => setFocused(true),
    onBlur: () => setFocused(false),
    tabIndex: 0,
    "data-focused": focused
  }, [
    React.createElement("img", {
      src: notification.iconPath,
      className: NotificationCSS.icon,
      key: "icon",
      alt: ""
    }),
    React.createElement("div", {
      className: NotificationCSS.label,
      key: "label"
    }, `Average happiness: ${notification.key}`)
  ]);
};

const SimpleNotificationBadge: React.FC<{
  className?: string;
  children: React.ReactNode;
}> = ({ className, children }) => {
  return React.createElement("div", {
    className: [NotificationCSS.badge, className].filter(Boolean).join(' '),
  }, [
    children,
    React.createElement("img", {
      src: "Media/Game/Icons/Citizen.svg",
      className: NotificationCSS.badgeIcon,
      key: "badge-icon",
      alt: ""
    })
  ]);
};

// Export components
export {
  SimpleNotification as Notification,
  SimpleHappinessNotification as HappinessNotification,
  SimpleAverageHappinessNotification as AverageHappinessNotification,
  SimpleProfitabilityNotification as ProfitabilityNotification,
  SimpleConditionNotification as ConditionNotification,
  SimpleNotificationBadge as NotificationBadge,
  NotificationCSS as NotificationTheme
};

export default {
  Notification: SimpleNotification,
  HappinessNotification: SimpleHappinessNotification,
  AverageHappinessNotification: SimpleAverageHappinessNotification,
  ProfitabilityNotification: SimpleProfitabilityNotification,
  ConditionNotification: SimpleConditionNotification,
  NotificationBadge: SimpleNotificationBadge,
  NotificationTheme: NotificationCSS
};