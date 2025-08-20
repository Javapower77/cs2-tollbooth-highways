import React, { ReactNode } from 'react';

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

// Default CSS classes theme (equivalent to VS in obfuscated code)
const defaultInfoRowTheme = {
  "info-row": "info-row_QQ9 item-focused_FuT",
  infoRow: "info-row_QQ9 item-focused_FuT",
  "disable-focus-highlight": "disable-focus-highlight_I85",
  disableFocusHighlight: "disable-focus-highlight_I85",
  link: "link_ICj",
  tooltipRow: "tooltipRow_uIh",
  left: "left_RyE",
  hasIcon: "hasIcon_iZ3",
  right: "right_ZUb",
  icon: "icon_ugE",
  uppercase: "uppercase_f0y",
  subRow: "subRow_NJI",
  "no-shrink": "no-shrink_oxj",
  noShrink: "no-shrink_oxj",
};

// Mock components for CS2 integration
const TooltipWrapper: React.FC<{ 
  tooltip?: ReactNode; 
  direction?: string; 
  alignment?: string; 
  children: ReactNode 
}> = ({ tooltip, children }) => (
  <div title={tooltip?.toString()}>
    {children}
  </div>
);

const FocusableContainer: React.FC<{ 
  activation?: string; 
  className?: string; 
  focusKey?: string;
  children: ReactNode 
}> = ({ className, children, ...props }) => (
  <div className={className} {...props}>
    {children}
  </div>
);

const SimpleContainer: React.FC<{ 
  className?: string; 
  children: ReactNode 
}> = ({ className, children }) => (
  <div className={className}>
    {children}
  </div>
);

// Focus key constants (mock implementations)
const FOCUS_DISABLED = "FOCUS_DISABLED";
const FOCUS_AUTO = "FOCUS_AUTO";

// Interfaces
interface InfoRowProps {
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

interface TooltipRowProps {
  icon?: string;
  left?: ReactNode;
  right?: ReactNode;
  uppercase?: boolean;
  subRow?: boolean;
}

// Main InfoRow Component (equivalent to KS in obfuscated code)
export const InfoRow: React.FC<InfoRowProps> = ({
  icon,
  left,
  right,
  tooltip,
  link,
  uppercase = false,
  subRow = false,
  disableFocus = false,
  className,
  noShrinkRight = false,
}) => {
  const rightClassName = right
    ? classNames(defaultInfoRowTheme.right, {
        [defaultInfoRowTheme.noShrink]: noShrinkRight,
        [defaultInfoRowTheme.link]: !!link,
      })
    : undefined;

  return (
    <TooltipWrapper
      tooltip={tooltip}
      direction="right"
      alignment="center"
    >
      {link ? (
        // When there's a link, create a focusable container with special layout
        <FocusableContainer
          activation="FocusedChild"
          className={classNames(
            defaultInfoRowTheme.infoRow,
            subRow && defaultInfoRowTheme.subRow,
            !!link && defaultInfoRowTheme.link,
            className
          )}
        >
          {icon && (
            <img
              src={icon}
              className={classNames(
                defaultInfoRowTheme.icon,
                !!link && defaultInfoRowTheme.link
              )}
              alt=""
            />
          )}
          {left && (
            <div
              className={classNames(
                defaultInfoRowTheme.left,
                uppercase && defaultInfoRowTheme.uppercase,
                !!link && defaultInfoRowTheme.link,
                icon && defaultInfoRowTheme.hasIcon
              )}
            >
              {left}
            </div>
          )}
          {link && (
            <FocusableContainer
              className={classNames(
                defaultInfoRowTheme.right,
                !!link && defaultInfoRowTheme.link
              )}
            >
              {link}
            </FocusableContainer>
          )}
          {right && (
            <div className={rightClassName}>
              {right}
            </div>
          )}
        </FocusableContainer>
      ) : disableFocus ? (
        // When focus is disabled, use a simple container without focus capabilities
        <SimpleContainer
          className={classNames(
            defaultInfoRowTheme.infoRow,
            subRow && defaultInfoRowTheme.subRow,
            defaultInfoRowTheme.disableFocusHighlight,
            className
          )}
        >
          {icon && (
            <img 
              src={icon} 
              className={defaultInfoRowTheme.icon} 
              alt=""
            />
          )}
          {left && (
            <div
              className={classNames(
                defaultInfoRowTheme.left,
                uppercase && defaultInfoRowTheme.uppercase
              )}
            >
              {left}
            </div>
          )}
          {right && (
            <div className={rightClassName}>
              {right}
            </div>
          )}
        </SimpleContainer>
      ) : (
        // Default focusable container
        <FocusableContainer
          focusKey={disableFocus ? FOCUS_DISABLED : FOCUS_AUTO}
          className={classNames(
            defaultInfoRowTheme.infoRow,
            subRow && defaultInfoRowTheme.subRow,
            className
          )}
        >
          {icon && (
            <img 
              src={icon} 
              className={defaultInfoRowTheme.icon} 
              alt=""
            />
          )}
          {left && (
            <div
              className={classNames(
                defaultInfoRowTheme.left,
                uppercase && defaultInfoRowTheme.uppercase
              )}
            >
              {left}
            </div>
          )}
          {right && (
            <div className={rightClassName}>
              {right}
            </div>
          )}
        </FocusableContainer>
      )}
    </TooltipWrapper>
  );
};

// TooltipRow Component (equivalent to WS in obfuscated code)
export const TooltipRow: React.FC<TooltipRowProps> = ({
  icon,
  left,
  right,
  uppercase = false,
  subRow = false,
}) => {
  return (
    <div
      className={classNames(
        defaultInfoRowTheme.infoRow,
        defaultInfoRowTheme.tooltipRow,
        subRow && defaultInfoRowTheme.subRow
      )}
    >
      {icon && (
        <img
          src={icon}
          className={classNames(defaultInfoRowTheme.icon)}
          alt=""
        />
      )}
      {left && (
        <div
          className={classNames(
            defaultInfoRowTheme.left,
            uppercase && defaultInfoRowTheme.uppercase,
            icon && defaultInfoRowTheme.hasIcon
          )}
        >
          {left}
        </div>
      )}
      {right && (
        <div className={classNames(defaultInfoRowTheme.right)}>
          {right}
        </div>
      )}
    </div>
  );
};

// Export theme and utilities
export { 
  defaultInfoRowTheme, 
  classNames 
};

// Default export
export default { InfoRow, TooltipRow };