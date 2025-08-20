import React, { useState, useRef, useCallback, useMemo, useEffect, forwardRef } from 'react';

// CSS theme classes for Info Button (equivalent to Yue object)
export const InfoButtonTheme = {
  "info-button": "info-button_n9m",
  infoButton: "info-button_n9m",
  button: "button_hTp",
  label: "label_MAK",
  container: "container_Et5",
  hint: "hint_bps",
};

// Additional theme for the button component (equivalent to zue)
export const ButtonTheme = {
  button: "button_hTp",
  icon: "icon_cls",
  hint: "hint_bps",
};

// Component interfaces
export interface InfoButtonProps {
  label: string;
  icon?: string;
  selected?: boolean;
  onSelect?: () => void;
  className?: string;
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

// CSS class utility function
const classNames = (...classes: (string | undefined | null | boolean)[]): string => {
  return classes
    .filter((cls) => cls)
    .map((cls) => {
      if (typeof cls === 'string') return cls;
      return '';
    })
    .join(' ')
    .trim();
};

// Placeholder image fallback function (equivalent to q_)
const handleImageError = (e: React.SyntheticEvent<HTMLImageElement>) => {
  e.currentTarget.src = "Media/Placeholder.svg";
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

// Button component (equivalent to U_)
export const Button = forwardRef<HTMLButtonElement, ButtonProps>(({
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
}, ref) => {
  const [isOverflowing, setIsOverflowing] = useState(false);
  const buttonRef = useRef<HTMLButtonElement | null>(null);
  const [focused, setFocused] = useState(false);

  // Combine refs
  const combinedRef = useCallback((node: HTMLButtonElement) => {
    buttonRef.current = node;
    if (typeof ref === 'function') {
      ref(node);
    } else if (ref) {
      ref.current = node;
    }
  }, [ref]);

  // Check for text overflow
  useEffect(() => {
    if (buttonRef.current) {
      const element = buttonRef.current?.firstElementChild || buttonRef.current;
      const hasOverflow = element.scrollWidth > element.clientWidth;
      setIsOverflowing(hasOverflow);
    }
  }, [tooltipLabel]);

  // Handle focus change
  const handleFocusChange = useCallback((isFocused: boolean) => {
    setFocused(isFocused);
  }, []);

  // Handle click
  const handleClick = useCallback((e: React.MouseEvent<HTMLButtonElement>) => {
    if (!disabled) {
      onSelect?.();
      onClick?.(e);
      e.stopPropagation();
    }
  }, [disabled, onSelect, onClick]);

  // Handle mouse enter
  const handleMouseEnter = useCallback((e: React.MouseEvent<HTMLButtonElement>) => {
    if (!disabled) {
      onMouseEnter?.(e);
    }
  }, [disabled, onMouseEnter]);

  const ButtonElement = as as any;
  const shouldShowHint = !disabled && (forceHint || focused) && !disableHint;

  let buttonElement: React.ReactElement = React.createElement("div", {
    children: React.createElement(ButtonElement, {
      ...props,
      ref: combinedRef,
      disabled: disabled,
      className: classNames(
        theme?.button,
        selected && "selected",
        focused && "focused",
        className
      ),
      onClick: handleClick,
      onMouseEnter: handleMouseEnter,
    }, [
      React.createElement(Hint, {
        action: hintAction,
        active: shouldShowHint,
        className: theme?.hint || InfoButtonTheme.hint,
        key: "hint"
      }),
      children
    ])
  });

  // Wrap with focus boundary
  buttonElement = React.createElement(FocusBoundary, {
    onFocusChange: handleFocusChange,
    children: buttonElement
  });

  return buttonElement;
});

Button.displayName = "Button";

// Info Button component (equivalent to que function)
export const InfoButton: React.FC<InfoButtonProps> = ({
  label,
  icon,
  selected,
  onSelect,
  className
}) => {
  const [isFocused, setIsFocused] = useState(false);

  const handleFocusChange = useCallback((focused: boolean) => {
    setIsFocused(focused);
  }, []);

  const buttonContent = React.createElement("div", {
    className: InfoButtonTheme.container,
  }, [
    React.createElement(Hint, {
      action: "Select",
      active: isFocused,
      className: InfoButtonTheme.hint,
      key: "hint"
    }),
    icon ? React.createElement("div", {
      className: InfoButtonTheme.label,
      key: "label-with-icon"
    }, [
      React.createElement("img", {
        src: icon,
        className: ButtonTheme.icon,
        onError: handleImageError,
        key: "icon"
      }),
      label
    ]) : React.createElement("div", {
      className: InfoButtonTheme.label,
      children: label,
      key: "label-only"
    })
  ]);

    return React.createElement("div", {
        className: classNames(InfoButtonTheme.infoButton, className),
    }, React.createElement(FocusBoundary, {
        onFocusChange: handleFocusChange,
        children: React.createElement(Button, {
            disableHint: true,
            selected: selected,
            onSelect: onSelect,
            onClick: stopPropagation,
            className: InfoButtonTheme.button,
            theme: ButtonTheme,
            children: buttonContent
        })
    }));
};

// Export utility functions and themes
export { classNames, handleImageError, stopPropagation };

// Export for backward compatibility
export default InfoButton;