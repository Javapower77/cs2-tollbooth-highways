import React, { createContext, useContext, useState, useCallback, useMemo, ReactNode, Children } from 'react';

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

// Default CSS classes theme (equivalent to LS in obfuscated code)
const defaultFoldoutTheme = {
  "foldout-item": "foldout-item_wOF",
  foldoutItem: "foldout-item_wOF",
  header: "header_8H_ item-mouse-states_Fmi item-focused_FuT",
  "header-content": "header-content_wUX",
  headerContent: "header-content_wUX",
  "disable-active-state": "disable-active-state_w8a",
  disableActiveState: "disable-active-state_w8a",
  "disable-mouse-states": "disable-mouse-states_js5",
  disableMouseStates: "disable-mouse-states_js5",
  toggle: "toggle_yQv",
  content: "content_mJm",
  enter: "enter_sm5",
  exit: "exit_ScE",
  "exit-active": "exit-active_LGX",
  exitActive: "exit-active_LGX",
  "enter-active": "enter-active_jNk",
  enterActive: "enter-active_jNk",
  expandable: "expandable",
  expanded: "expanded",
  item: "item",
  group: "group",
  category: "category",
};

// Info Section Foldout theme (equivalent to US in obfuscated code)
const infoSectionFoldoutTheme = {
  "foldout-item": "foldout-item_Ha3 foldout-item_wOF",
  foldoutItem: "foldout-item_Ha3 foldout-item_wOF",
  header: "header_o7z header_8H_ item-mouse-states_Fmi item-focused_FuT",
  "disable-active-state": "disable-active-state_UBt disable-active-state_w8a",
  disableActiveState: "disable-active-state_UBt disable-active-state_w8a",
  "disable-mouse-states": "disable-mouse-states_ivb disable-mouse-states_js5",
  disableMouseStates: "disable-mouse-states_ivb disable-mouse-states_js5",
  "header-content": "header-content_t6v header-content_wUX",
  headerContent: "header-content_t6v header-content_wUX",
  icon: "icon_p5g undefined",
  toggle: "toggle_r8A toggle_yQv",
  infoSection: "info-section",
  content: "content",
  disableFocusHighlight: "disable-focus-highlight",
};

// Mock components for CS2 integration
const Button: React.FC<any> = ({ 
  className,
  onClick,
  onMouseEnter,
  onFocusChange,
  disabled,
  children,
  ...props
}) => (
  <button
    className={className}
    onClick={onClick}
    onMouseEnter={onMouseEnter}
    onFocus={() => onFocusChange?.(true)}
    onBlur={() => onFocusChange?.(false)}
    disabled={disabled}
    {...props}
  >
    {children}
  </button>
);

const FocusableContainer: React.FC<{ 
  className?: string;
  focusKey?: string;
  children: ReactNode;
  onAction?: () => void;
  disabled?: boolean;
}> = ({ className, children, onAction, disabled, ...props }) => (
  <div
    className={className}
    onClick={!disabled ? onAction : undefined}
    tabIndex={!disabled ? 0 : -1}
    {...props}
  >
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

const Image: React.FC<{ src: string; className?: string; alt?: string }> = ({ src, className, alt = "" }) => (
  <img src={src} className={className} alt={alt} />
);

// Collapse/Expand transition component
const CollapseTransition: React.FC<{ 
  in: boolean; 
  children: ReactNode;
  styles?: any;
}> = ({ in: isIn, children, styles }) => (
  <div 
    className={classNames(
      styles?.content,
      isIn ? styles?.enterActive : styles?.exitActive
    )}
    style={{ 
      display: isIn ? 'block' : 'none',
      overflow: 'hidden',
      transition: 'all 0.3s ease'
    }}
  >
    {children}
  </div>
);

// Sound function mock (equivalent to cp() in obfuscated code)
const playSound = (sound: string) => {
  console.log(`Playing sound: ${sound}`);
};

// Sound constants
const foldoutSounds = {
  expandPanel: 'expandPanel',
  selectItem: 'selectItem',
  hoverItem: 'hoverItem',
};

// Interfaces
interface FoldoutContextType {
  theme: any;
  expandable?: boolean;
  expanded?: boolean;
  onToggleExpanded?: () => void;
  onSelect?: () => void;
}

interface FoldoutItemProps {
  header: ReactNode;
  theme?: any;
  type?: 'Item' | 'Group' | 'Category';
  nesting?: number;
  initialExpanded?: boolean;
  expandFromContent?: boolean;
  onSelect?: () => void;
  onToggleExpanded?: (expanded: boolean) => void;
  className?: string;
  children: ReactNode;
}

interface FoldoutItemHeaderProps {
  onClick?: () => void;
  onFocusChange?: (focused: boolean) => void;
  children: ReactNode;
}

interface InfoSectionFoldoutProps {
  header: ReactNode;
  initialExpanded?: boolean;
  expandFromContent?: boolean;
  focusKey?: string;
  tooltip?: ReactNode;
  disableFocus?: boolean;
  className?: string;
  onToggleExpanded?: (expanded: boolean) => void;
  children: ReactNode;
}

// Foldout Context (equivalent to MS in obfuscated code)
const FoldoutContext = createContext<FoldoutContextType>({ 
  theme: defaultFoldoutTheme 
});

// Foldout theme hook (equivalent to RS in obfuscated code)
export const useFoldoutTheme = (customTheme?: any) => {
  return useMemo(() => ({ ...defaultFoldoutTheme, ...customTheme }), [customTheme]);
};

// Helper function for nesting styles (equivalent to FS in obfuscated code)
const getNestingStyle = (nesting: number): React.CSSProperties => ({
  '--nesting': nesting
} as React.CSSProperties);

// Foldout Item Header Component (equivalent to DS in obfuscated code)
export const FoldoutItemHeader: React.FC<FoldoutItemHeaderProps> = ({
  onClick,
  onFocusChange,
  children
}) => {
  const [focused, setFocused] = useState(false);
  const context = useContext(FoldoutContext);

  const handleMouseEnter = useCallback(() => {
    playSound(foldoutSounds.hoverItem);
  }, []);

  const handleClick = useCallback(() => {
    if (context.expandable) {
      context.onToggleExpanded?.();
    } else {
      context.onSelect?.();
    }
    onClick?.();
  }, [context, onClick]);

  const handleFocusChange = useCallback((isFocused: boolean) => {
    setFocused(isFocused);
    onFocusChange?.(isFocused);
  }, [onFocusChange]);

  return (
    <FocusableContainer
      className={classNames(context.theme.header)}
      //onMouseEnter={handleMouseEnter}
      onAction={handleClick}
    >
      <div className={context.theme.headerContent}>
        {children}
      </div>
      {context.expandable && (
        <>
          <Image
            className={context.theme.toggle}
            src={context.expanded
              ? "Media/Glyphs/ThickStrokeArrowDown.svg"
              : "Media/Glyphs/ThickStrokeArrowRight.svg"
            }
          />
        </>
      )}
    </FocusableContainer>
  );
};

// Foldout Item Component (equivalent to kS in obfuscated code)
export const FoldoutItem: React.FC<FoldoutItemProps> = ({
  header,
  theme = defaultFoldoutTheme,
  type = 'Item',
  nesting = 0,
  initialExpanded = false,
  expandFromContent = false,
  onSelect,
  onToggleExpanded,
  className,
  children
}) => {
  const resolvedTheme = useFoldoutTheme(theme);
  const hasChildren = Children.count(children) > 0;
  const [expanded, setExpanded] = useState(initialExpanded);

  const handleToggleExpanded = useCallback(() => {
    playSound(foldoutSounds.expandPanel);
    setExpanded(prev => !prev);
    onToggleExpanded?.(!expanded);
  }, [expanded, onToggleExpanded]);

  const handleSelect = useCallback(() => {
    if (onSelect) {
      playSound(foldoutSounds.selectItem);
      onSelect();
    }
  }, [onSelect]);

  const isDisabled = !hasChildren && !onSelect;

  const contextValue = useMemo(() => ({
    theme: resolvedTheme,
    expandable: hasChildren,
    expanded,
    onToggleExpanded: handleToggleExpanded,
    onSelect: handleSelect,
  }), [hasChildren, expanded, handleSelect, handleToggleExpanded, resolvedTheme]);

  return (
    <FoldoutContext.Provider value={contextValue}>
      <div
        style={getNestingStyle(nesting)}
        className={classNames(
          resolvedTheme.foldoutItem,
          hasChildren && resolvedTheme.expandable,
          expanded && resolvedTheme.expanded,
          type === 'Item' && resolvedTheme.item,
          type === 'Group' && resolvedTheme.group,
          type === 'Category' && resolvedTheme.category,
          isDisabled && resolvedTheme.disableMouseStates,
          className
        )}
      >
        <FocusableContainer
          onAction={handleSelect}
          disabled={hasChildren}
        >
          <FocusableContainer
        //    expandable={hasChildren}
            disabled={!hasChildren}
       //     expanded={expanded}
            onAction={handleToggleExpanded}
          >
            {header}
          </FocusableContainer>
        </FocusableContainer>

        <FocusableContainer
        //  expandable={hasChildren}
          disabled={!hasChildren || !expandFromContent}
        //  expanded={expanded}
          onAction={handleToggleExpanded}
        >
          <CollapseTransition
            in={hasChildren && expanded}
            styles={defaultFoldoutTheme}
          >
            <div className={classNames(resolvedTheme.content, "foldout-expanded")}>
              <div>
                {children}
              </div>
            </div>
          </CollapseTransition>
        </FocusableContainer>
      </div>
    </FoldoutContext.Provider>
  );
};

// Info Section Foldout Component (equivalent to GS in obfuscated code)
export const InfoSectionFoldout: React.FC<InfoSectionFoldoutProps> = ({
  header,
  initialExpanded,
  expandFromContent,
  focusKey,
  tooltip,
  disableFocus = false,
  className,
  onToggleExpanded,
  children
}) => {
  const childrenCount = Children.count(children);

  return (
    <FoldoutItem
      header={
        <TooltipWrapper
          tooltip={tooltip}
          direction="right"
          alignment="center"
        >
          <FoldoutItemHeader>
            {header}
          </FoldoutItemHeader>
        </TooltipWrapper>
      }
      theme={infoSectionFoldoutTheme}
      initialExpanded={initialExpanded}
      expandFromContent={expandFromContent}
      onToggleExpanded={onToggleExpanded}
      className={classNames(infoSectionFoldoutTheme.infoSection, className)}
    >
      {childrenCount > 0 ? (
        disableFocus ? (
          <SimpleContainer
            className={classNames(
              infoSectionFoldoutTheme.content,
              infoSectionFoldoutTheme.disableFocusHighlight
            )}
          >
            <div>
              {children}
            </div>
          </SimpleContainer>
        ) : (
          <FocusableContainer
            focusKey={focusKey}
            className={infoSectionFoldoutTheme.content}
          >
            <div>
              {children}
            </div>
          </FocusableContainer>
        )
      ) : null}
    </FoldoutItem>
  );
};

// Export all components and utilities
export { 
  FoldoutContext, 
  defaultFoldoutTheme, 
  infoSectionFoldoutTheme,
  foldoutSounds,
  classNames,
  playSound
};

// Default export
export default InfoSectionFoldout;