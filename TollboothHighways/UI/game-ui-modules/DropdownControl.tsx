import React, { createContext, useContext, useState, useRef, useEffect, useMemo, useCallback, ReactNode } from 'react';

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

// Default CSS classes theme (equivalent to dS/mS in obfuscated code)
const defaultDropdownTheme = {
  "dropdown-toggle": "dropdown-toggle_bN9",
  dropdownToggle: "dropdown-toggle_bN9",
  label: "label_REA",
  indicator: "indicator_lzV",
  "dropdown-popup": "dropdown-popup_duR",
  dropdownPopup: "dropdown-popup_duR",
  "dropdown-menu": "dropdown-menu_rL4",
  dropdownMenu: "dropdown-menu_rL4",
  scrollable: "scrollable_MVT",
  "dropdown-item": "dropdown-item_t3P",
  dropdownItem: "dropdown-item_t3P",
  hiddenIcon: "Media/Glyphs/StrokeArrowDown.svg",
  visibleIcon: "Media/Glyphs/StrokeArrowUp.svg",
};

// Sound constants (equivalent to lp in obfuscated code)
const dropdownSounds = {
  selectDropdown: 'selectDropdown',
  hover: null,
};

// Focus key constants (equivalent to gS, pS in obfuscated code)
const DROPDOWN_TOGGLE_KEY = "DROPDOWN_TOGGLE";
const DROPDOWN_MENU_KEY = "DROPDOWN_MENU";

// Dropdown Context Type (equivalent to hS in obfuscated code)
interface DropdownContextType {
  visible: boolean;
  theme: any;
  show: () => void;
  hide: () => void;
  toggle: () => void;
}

// Create Dropdown Context (equivalent to hS in obfuscated code)
const DropdownContext = createContext<DropdownContextType>({
  visible: false,
  theme: defaultDropdownTheme,
  show: () => {},
  hide: () => {},
  toggle: () => {},
});

// Mock components for CS2 integration
const Button: React.FC<any> = ({ 
  disableHint,
  hintAction,
  forceHint,
  focusKey,
  className,
  theme,
  sounds,
  selectSound,
  onSelect,
  tooltipLabel,
  children,
  ...props
}) => (
  <button
    className={className}
    onClick={onSelect}
    title={tooltipLabel}
    data-focus-key={focusKey}
    data-hint={hintAction}
    {...props}
  >
    {children}
  </button>
);

const Image: React.FC<{ src: string; className?: string }> = ({ src, className }) => (
  <img src={src} className={className} alt="" />
);

// Hook for focus detection (equivalent to Dm() in obfuscated code)
const useFocused = (): boolean => {
  return false; // Simplified implementation
};

// Mock sound function (equivalent to cp() in obfuscated code)
const playSound = (sound: string) => {
  console.log(`Playing sound: ${sound}`);
};

// Interfaces
interface DropdownProps {
  focusKey?: string;
  initialFocused?: string;
  theme?: any;
  content?: ReactNode;
  alignment?: string;
  children: ReactNode;
  onToggle?: (visible: boolean) => void;
}

interface DropdownToggleProps {
  theme?: any;
  openIconComponent?: ReactNode;
  closeIconComponent?: ReactNode;
  children: ReactNode;
  [key: string]: any;
}

interface DropdownToggleBaseProps {
  theme?: any;
  buttonTheme?: any;
  sounds?: any;
  showHint?: boolean;
  selectSound?: string;
  className?: string;
  children: ReactNode;
  tooltipLabel?: string;
  [key: string]: any;
}

interface DropdownItemProps {
  focusKey?: string;
  value?: any;
  selected?: boolean;
  theme?: any;
  sounds?: any;
  className?: string;
  onChange?: (value: any) => void;
  onToggleSelected?: (value: any) => void;
  closeOnSelect?: boolean;
  children: ReactNode;
}

// Main Dropdown Component (equivalent to fS in obfuscated code)
export const Dropdown: React.FC<DropdownProps> = ({
  focusKey,
  initialFocused,
  theme = defaultDropdownTheme,
  content,
  alignment,
  children,
  onToggle,
}) => {
  const resolvedTheme = useMemo(() => ({ ...defaultDropdownTheme, ...theme }), [theme]);
  const [visible, setVisible] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const toggle = useCallback(() => {
    playSound(dropdownSounds.selectDropdown);
    setVisible((prev) => !prev);
    onToggle && onToggle(!visible);
  }, [onToggle, visible]);

  const show = useCallback(() => {
    setVisible(true);
    onToggle && onToggle(true);
  }, [onToggle]);

  const hide = useCallback(() => {
    setVisible(false);
    onToggle && onToggle(false);
  }, [onToggle]);

  const contextValue = useMemo(
    () => ({ visible, theme: resolvedTheme, show, hide, toggle }),
    [visible, resolvedTheme, show, hide, toggle]
  );

  // Handle clicks outside dropdown to close it
  useEffect(() => {
    if (visible) {
      const handleClickOutside = (event: MouseEvent) => {
        if (!dropdownRef.current) return;
        const rect = dropdownRef.current.getBoundingClientRect();
        if (
          event.clientX < rect.left ||
          event.clientX > rect.right ||
          event.clientY < rect.top ||
          event.clientY > rect.bottom
        ) {
          setVisible(false);
        }
      };

      document.addEventListener("mousedown", handleClickOutside);
      return () => document.removeEventListener("mousedown", handleClickOutside);
    }
  }, [visible]);

  const dropdownMenu = (
    <div className={resolvedTheme?.dropdownMenu} ref={dropdownRef}>
      <div className={resolvedTheme.scrollable}>
        <div data-focus-key={DROPDOWN_MENU_KEY}>
          {content}
        </div>
      </div>
    </div>
  );

  return (
    <>
      {visible && <div className="dropdown-overlay" onClick={hide} />}
      <div data-focus-key={focusKey}>
        <DropdownContext.Provider value={contextValue}>
          <div className={classNames(resolvedTheme.dropdownPopup, visible && 'visible')}>
            <div data-focus-key={DROPDOWN_TOGGLE_KEY}>
              {children}
            </div>
            {visible && dropdownMenu}
          </div>
        </DropdownContext.Provider>
      </div>
    </>
  );
};

// Dropdown Toggle Component (equivalent to xS in obfuscated code)
export const DropdownToggle: React.FC<DropdownToggleProps> = ({
  theme,
  openIconComponent,
  closeIconComponent,
  children,
  ...props
}) => {
  const dropdownContext = useContext(DropdownContext);
  const resolvedTheme = theme ?? dropdownContext.theme;

  return (
    <DropdownToggleBase {...props} theme={theme}>
      <div className={resolvedTheme.label}>
        {children}
      </div>
      {resolvedTheme.indicator && (
        <>
          {dropdownContext.visible && 
           resolvedTheme.visibleIcon && 
           (closeIconComponent || (
             <Image
               src={resolvedTheme.visibleIcon}
               className={resolvedTheme.indicator}
             />
           ))}
          {!dropdownContext.visible && 
           resolvedTheme.hiddenIcon && 
           (openIconComponent || (
             <Image
               src={resolvedTheme.hiddenIcon}
               className={resolvedTheme.indicator}
             />
           ))}
        </>
      )}
    </DropdownToggleBase>
  );
};

// Dropdown Toggle Base Component (equivalent to vS in obfuscated code)
export const DropdownToggleBase: React.FC<DropdownToggleBaseProps> = ({
  theme,
  buttonTheme,
  sounds,
  showHint,
  selectSound = dropdownSounds.selectDropdown,
  className,
  children,
  tooltipLabel,
  ...props
}) => {
  const dropdownContext = useContext(DropdownContext);
  const resolvedTheme = theme ?? dropdownContext.theme;
  const isFocused = useFocused();

  return (
    <Button
      disableHint={!showHint}
      hintAction={dropdownContext.visible ? "Back" : "Select"}
      forceHint={showHint && dropdownContext.visible}
      {...props}
      focusKey={DROPDOWN_TOGGLE_KEY}
      className={classNames(
        resolvedTheme?.dropdownToggle,
        className,
        dropdownContext.visible && "active",
        dropdownContext.visible && isFocused && "focused"
      )}
      theme={buttonTheme}
      sounds={sounds}
      selectSound={selectSound}
      onSelect={dropdownContext.toggle}
      tooltipLabel={dropdownContext.visible ? undefined : tooltipLabel}
    >
      {children}
    </Button>
  );
};

// Dropdown Item Component (equivalent to bS in obfuscated code)
export const DropdownItem: React.FC<DropdownItemProps> = ({
  focusKey,
  value,
  selected = false,
  theme,
  sounds = { ...dropdownSounds, hover: null },
  className,
  onChange,
  onToggleSelected,
  closeOnSelect = true,
  children,
}) => {
  const dropdownContext = useContext(DropdownContext);
  const resolvedTheme = theme ?? dropdownContext.theme;

  const handleSelect = useCallback(() => {
    if (selected) {
      onToggleSelected && onToggleSelected(value);
    } else {
      onChange && onChange(value);
    }
    if (closeOnSelect) {
      dropdownContext.hide();
    }
  }, [selected, closeOnSelect, dropdownContext, onChange, value, onToggleSelected]);

  const itemFocusKey = focusKey != null 
    ? focusKey 
    : (typeof value === 'string' || typeof value === 'number') 
      ? value 
      : undefined;

  return (
    <Button
      disableHint={true}
      focusKey={itemFocusKey}
      sounds={sounds}
      className={classNames(
        resolvedTheme.dropdownItem, 
        selected && "selected", 
        className
      )}
      onSelect={handleSelect}
    >
      {children || " "}
    </Button>
  );
};

// Export all components and utilities
export { 
  DropdownContext, 
  defaultDropdownTheme, 
  dropdownSounds,
  DROPDOWN_TOGGLE_KEY,
  DROPDOWN_MENU_KEY,
  classNames 
};

// Default export
export default Dropdown;