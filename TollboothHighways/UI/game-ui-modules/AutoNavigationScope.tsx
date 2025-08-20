import React, { useState, useCallback } from 'react';
import { jsx } from 'react/jsx-runtime';

// A placeholder for the game's actual focus management component (deobfuscated from Zg)
const FocusManager = (props: any) => <div {...props} />;

interface AutoNavigationScopeProps {
  onRefocus?: () => void;
  onChange?: (focusedItem: any) => void;
  allowFocusExit?: boolean;
  allowLooping?: boolean;
  debugName?: string;
  focusKey?: string;
  forceFocus?: any;
  initialFocused?: any;
  children: React.ReactNode;
}

/**
 * Manages focus navigation for a list of items, allowing for keyboard and controller support.
 * (Deobfuscated from np)
 */
export const AutoNavigationScope: React.FC<AutoNavigationScopeProps> = ({
  onRefocus = () => {},
  onChange,
  allowFocusExit = true,
  allowLooping = false,
  debugName = "AutoNavigationScope",
  focusKey = "auto-nav-scope",
  forceFocus,
  initialFocused,
  ...rest
}) => {
  const [focused, setFocused] = useState(initialFocused ?? null);

  const handleFocusChange = useCallback((newlyFocused: any) => {
    setFocused(newlyFocused);
    if (onChange) {
      onChange(newlyFocused);
    }
  }, [onChange]);

  return jsx(FocusManager, {
    focusKey,
    focused: forceFocus ?? focused,
    debugName,
    onChange: handleFocusChange,
    onRefocus,
    allowFocusExit,
    allowLooping,
    ...rest,
  });
};