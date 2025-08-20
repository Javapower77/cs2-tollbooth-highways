import React from 'react';
import { jsx, jsxs } from 'react/jsx-runtime';

// --- Placeholder Components (representing obfuscated game components) ---
const TooltipWrapper = (props: any) => <div {...props} />;
const TooltipContent = (props: any) => <div {...props} />;
const FocusableButton = (props: any) => <button {...props} />;
const LocalizedText = (props: { hash: string }) => <span>{props.hash}</span>;
const NotificationBadge = (props: any) => <div {...props} />;
const CounterDisplay = (props: { value: number }) => <span>{props.value}</span>;
// --- End Placeholder Components ---

// Deobfuscated from _me/vme
const notificationStyles = {
  notification: "notification-item",
  fullWidth: "full-width",
  icon: "notification-icon",
  label: "notification-label",
  badge: "notification-badge",
};

interface Notification {
  key: string;
  iconPath: string;
  count?: number;
}

interface NotificationControlProps {
  notification: Notification;
  anchorElRef?: React.RefObject<HTMLElement>;
}

/**
 * Displays a single notification with an icon, title, and optional count badge.
 * (Deobfuscated from bme)
 */
export const NotificationControl: React.FC<NotificationControlProps> = ({ notification, anchorElRef }) => {
  return jsx(TooltipWrapper, {
    direction: "right",
    alignment: "center",
    anchorElRef,
    tooltip: jsx(TooltipContent, {
      children: jsx(LocalizedText, {
        hash: `Notifications.DESCRIPTION.${notification.key}`,
      }),
    }),
    children: jsxs(FocusableButton, {
      className: `${notificationStyles.notification} ${notificationStyles.fullWidth}`,
      children: [
        jsx("img", { src: notification.iconPath, className: notificationStyles.icon, alt: "" }),
        jsxs("div", {
          className: notificationStyles.label,
          children: [
            jsx(LocalizedText, { hash: `Notifications.TITLE.${notification.key}` }),
            notification.count && notification.count > 1 &&
              jsx(NotificationBadge, {
                className: notificationStyles.badge,
                children: jsx(CounterDisplay, { value: notification.count }),
              }),
          ],
        }),
      ],
    }),
  });
};