import React, { useState, useCallback } from 'react';

// CSS theme classes for Info Link (equivalent to Jue object)
export const InfoLinkTheme = {
  "info-link": "info-link_Mpc item-focused_FuT",
  infoLink: "info-link_Mpc item-focused_FuT",
  row: "row_Q8R",
  ellipsis: "ellipsis_C0N",
  smallText: "smallText_DnB",
  uppercase: "uppercase_qZ8",
  icon: "icon_hE2",
  hint: "hint_IBf",
};

// Component interfaces
export interface InfoLinkProps {
  icon?: string;
  tooltip?: React.ReactNode;
  uppercase?: boolean;
  onSelect?: () => void;
  children: React.ReactNode;
  className?: string;
}

export interface TooltipProps {
  tooltip?: React.ReactNode;
  forceVisible?: boolean;
  disabled?: boolean;
  theme?: any;
  direction?: string;
  alignment?: string;
  className?: string;
  children: React.ReactNode;
  anchorElRef?: React.RefObject<HTMLElement>;
}

export interface FocusBoundaryProps {
  disabled?: boolean;
  children: React.ReactNode;
  onFocusChange?: (focused: boolean) => void;
}

export interface ButtonProps {
  focusKey?: string;
  debugName?: string;
  selected?: boolean;
  disabled?: boolean;
  theme?: any;
  sounds?: any;
  selectAction?: string;
  selectSound?: string;
  className?: string;
  tooltipLabel?: string;
  disableHint?: boolean;
  onClick?: (e: React.MouseEvent) => void;
  onMouseEnter?: (e: React.MouseEvent) => void;
  onSelect?: () => void;
  children: React.ReactNode;
  as?: string;
  hintAction?: string;
  forceHint?: boolean;
  shortcut?: string;
  allowFocusableChildren?: boolean;
}

export interface HintProps {
  action: string;
  active?: boolean;
  controlScheme?: string;
  className?: string;
}

// CSS class utility function (equivalent to Zu)
const classNames = (...classes: (string | undefined | null | boolean)[]): string => {
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

// Mock function to prevent event propagation (equivalent to Db)
const stopPropagation = (e: React.MouseEvent) => {
  e.stopPropagation();
};

// Focus Boundary component (equivalent to Ah)
export const FocusBoundary: React.FC<FocusBoundaryProps> = ({ 
  disabled = false, 
  children, 
  onFocusChange 
}) => {
  const [focused, setFocused] = useState(false);

  const handleFocusChange = useCallback((isFocused: boolean) => {
    if (!disabled) {
      setFocused(isFocused);
      onFocusChange?.(isFocused);
    }
  }, [disabled, onFocusChange]);

  const handleFocus = useCallback(() => {
    handleFocusChange(true);
  }, [handleFocusChange]);

  const handleBlur = useCallback(() => {
    handleFocusChange(false);
  }, [handleFocusChange]);

  return React.createElement("div", {
    onFocus: handleFocus,
    onBlur: handleBlur,
    tabIndex: disabled ? -1 : 0,
  }, children);
};

// Hint component (equivalent to UT)
export const Hint: React.FC<HintProps> = ({
  action,
  active = true,
  controlScheme = "gamepad",
  className,
  ...props
}) => {
  // Mock implementation - in real CS2 this would show gamepad/keyboard hints
  if (!active || !action) {
    return null;
  }

  return React.createElement("div", {
    className: classNames("hint-indicator", className),
    ...props
  }, action);
};

// Simple Button component (equivalent to U_)
export const Button: React.FC<ButtonProps> = ({
  focusKey,
  debugName = "Button",
  selected,
  disabled,
  theme,
  sounds,
  selectAction = "Select",
  selectSound,
  className,
  tooltipLabel,
  disableHint = false,
  onClick,
  onMouseEnter,
  onSelect,
  children,
  as = "button",
  hintAction = "Select",
  forceHint,
  shortcut,
  allowFocusableChildren,
  ...props
}) => {
  const [focused, setFocused] = useState(false);

  const handleClick = useCallback((e: React.MouseEvent<HTMLButtonElement>) => {
    if (!disabled) {
      onSelect?.();
      onClick?.(e);
      e.stopPropagation();
    }
  }, [disabled, onSelect, onClick]);

  const handleMouseEnter = useCallback((e: React.MouseEvent<HTMLButtonElement>) => {
    if (!disabled) {
      onMouseEnter?.(e);
    }
  }, [disabled, onMouseEnter]);

  const handleFocus = useCallback(() => {
    setFocused(true);
  }, []);

  const handleBlur = useCallback(() => {
    setFocused(false);
  }, []);

  const ButtonElement = as as any;

  return React.createElement(ButtonElement, {
    ...props,
    disabled: disabled,
    className: classNames(
      theme?.button,
      selected && "selected",
      focused && "focused",
      className
    ),
    onClick: handleClick,
    onMouseEnter: handleMouseEnter,
    onFocus: handleFocus,
    onBlur: handleBlur,
  }, children);
};

// Tooltip component (equivalent to Eg)
export const Tooltip: React.FC<TooltipProps> = ({
  tooltip,
  forceVisible = false,
  disabled = false,
  theme,
  direction,
  alignment,
  className,
  children,
  anchorElRef
}) => {
  const [isHovered, setIsHovered] = useState(false);
  const [isFocused, setIsFocused] = useState(false);

  const handleMouseEnter = useCallback(() => {
    setIsHovered(true);
  }, []);

  const handleMouseLeave = useCallback(() => {
    setIsHovered(false);
  }, []);

  const handleFocusChange = useCallback((focused: boolean) => {
    setIsFocused(focused);
  }, []);

  const shouldShowTooltip = !disabled && tooltip && (forceVisible || isHovered || isFocused);

  return React.createElement(React.Fragment, {}, [
    React.createElement(FocusBoundary, {
      onFocusChange: handleFocusChange,
      key: "focus-boundary",
      children: React.createElement("div", {
        onMouseEnter: handleMouseEnter,
        onMouseLeave: handleMouseLeave,
        style: { position: 'relative' }
      }, [
        children,
        shouldShowTooltip && React.createElement("div", {
          key: "tooltip",
          className: classNames("tooltip-container", className),
          style: {
            position: 'absolute',
            top: '100%',
            left: '50%',
            transform: 'translateX(-50%)',
            zIndex: 1000,
            backgroundColor: 'rgba(0, 0, 0, 0.9)',
            color: 'white',
            padding: '8px 12px',
            borderRadius: '4px',
            fontSize: '14px',
            whiteSpace: 'nowrap',
            marginTop: '4px',
            border: '1px solid rgba(255, 255, 255, 0.2)'
          }
        }, tooltip)
      ]))
    ]);
};

// Info Link component (equivalent to tde function)
export const InfoLink: React.FC<InfoLinkProps> = ({
  icon,
  tooltip,
  uppercase = false,
  onSelect,
  children,
  className
}) => {
  const [isFocused, setIsFocused] = useState(false);

  const handleFocusChange = useCallback((focused: boolean) => {
    setIsFocused(focused);
  }, []);

  const handleSelect = useCallback(() => {
    onSelect?.();
  }, [onSelect]);

  const buttonContent = React.createElement(React.Fragment, {}, [
    // Info icon (always present)
    React.createElement("img", {
      src: "Media/Glyphs/ViewInfo.svg",
      className: InfoLinkTheme.icon,
      key: "info-icon",
      alt: "Info"
    }),
    
    // Custom icon (if provided)
    icon && React.createElement("img", {
      src: icon,
      className: InfoLinkTheme.icon,
      key: "custom-icon",
      alt: ""
    }),
    
    // Content with ellipsis
    React.createElement("div", {
      className: InfoLinkTheme.ellipsis,
      key: "content"
    }, children),
    
    // Hint indicator
    React.createElement(Hint, {
      action: "Select",
      active: isFocused,
      className: InfoLinkTheme.hint,
      key: "hint"
    })
  ]);

  const buttonElement = React.createElement(Button, {
    disableHint: true,
    onSelect: handleSelect,
    onClick: stopPropagation,
    className: classNames(
      InfoLinkTheme.row,
      InfoLinkTheme.infoLink,
      uppercase && InfoLinkTheme.uppercase,
      className
    ),
    children: buttonContent
  });

  const focusBoundaryElement = React.createElement(FocusBoundary, {
    onFocusChange: handleFocusChange,
    children: buttonElement
  });

  return React.createElement(Tooltip, {
    tooltip: tooltip,
    children: focusBoundaryElement
  });
};

// Export utility functions and themes
export { classNames, stopPropagation };

// Export for backward compatibility
export default InfoLink;