import React from 'react';
import { jsx } from 'react/jsx-runtime';
import { AutoNavigationScope } from './AutoNavigationScope';
import { NotificationControl } from './NotificationControl';

// Deobfuscated from Dhe/Fhe
const sectionStyles = {
  notificationsSection: "notifications-section_cKq",
  factors: "factors_Y7i",
};

// A placeholder for the game's focusable container component (deobfuscated from Sp)
const FocusableContainer = (props: any) => <div {...props} />;

interface Notification {
  key: string;
  iconPath: string;
  count?: number;
}

interface NotificationsSectionProps {
  focusKey: string;
  notifications: Notification[];
}

/**
 * Renders a list of notifications within a focusable, navigable container.
 * (Deobfuscated from Bhe)
 */
export const NotificationsSection: React.FC<NotificationsSectionProps> = ({ focusKey, notifications }) => {
  return jsx(FocusableContainer, {
    focusKey,
    className: sectionStyles.notificationsSection,
    children:
      notifications.length > 0 &&
      jsx(AutoNavigationScope, {
        children: notifications.map((notification) =>
          jsx(NotificationControl, { notification }, notification.key)
        ),
      }),
  });
};