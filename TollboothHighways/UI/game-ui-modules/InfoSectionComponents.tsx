import React, { ReactNode } from 'react';

// CSS class utility function (equivalent to cs() in the obfuscated code)
const cs = (...classes: (string | undefined | null | boolean | { [key: string]: boolean })[]): string => {
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

// Default CSS theme classes (equivalent to IS object)
export const InfoSectionTheme = {
  infoSection: 'info-section_I7V',
  content: 'content_Cdk item-focused_FuT',
  column: 'column_aPB',
  divider: 'divider_rfM',
  noMargin: 'no-margin_K7I',
  disableFocusHighlight: 'disable-focus-highlight_ik3',
  infoWrapBox: 'info-wrap-box_Rt4',
};

// Component interfaces
export interface InfoSectionProps {
  focusKey?: string;
  tooltip?: ReactNode;
  disableFocus?: boolean;
  className?: string;
  children: ReactNode;
}

export interface DividerProps {
  noMargin?: boolean;
  className?: string;
}

export interface TooltipProps {
  tooltip: ReactNode;
  forceVisible?: boolean;
  disabled?: boolean;
  theme?: typeof TooltipTheme;
  direction?: 'up' | 'down' | 'left' | 'right';
  alignment?: 'start' | 'center' | 'end';
  className?: string;
  children: ReactNode;
  anchorElRef?: React.RefObject<HTMLDivElement>;
}

// Tooltip theme classes (equivalent to Tg object from obfuscated code)
export const TooltipTheme = {
  balloon: 'balloon_qJY balloon_H23',
  enter: 'enter_ZTk',
  enterActive: 'enter-active_AJs',
  exitActive: 'exit-active_RfZ',
  container: 'container_zgM container_jfe',
  arrow: 'arrow_SVb arrow_Xfn',
  content: 'content_A82 content_JQV',
};

// Focus components interfaces
export interface FocusableProps {
  focusKey?: string;
  className?: string;
  children: ReactNode;
  onFocus?: () => void;
  onBlur?: () => void;
}

export interface FocusDisabledProps {
  className?: string;
  children: ReactNode;
}

// Mock focus components (these would be imported from the game's focus system)
const FocusableComponent: React.FC<FocusableProps> = ({ 
  focusKey, 
  className, 
  children, 
  onFocus, 
  onBlur 
}) => (
  <div 
    className={className}
    data-focus-key={focusKey}
    onFocus={onFocus}
    onBlur={onBlur}
    tabIndex={0}
  >
    {children}
  </div>
);

const FocusDisabledComponent: React.FC<FocusDisabledProps> = ({ 
  className, 
  children 
}) => (
  <div className={className}>
    {children}
  </div>
);

const ColumnComponent: React.FC<{ children: ReactNode }> = ({ children }) => (
  <div className={InfoSectionTheme.column}>
    {children}
  </div>
);

// Tooltip component (equivalent to Eg function)
export const Tooltip: React.FC<TooltipProps> = ({
  tooltip,
  forceVisible = false,
  disabled = false,
  theme = TooltipTheme,
  direction = 'right',
  alignment = 'center',
  className,
  children,
  anchorElRef,
}) => {
  const [isVisible, setIsVisible] = React.useState(false);
  const [isFocused, setIsFocused] = React.useState(false);

  const handleMouseEnter = React.useCallback(() => {
    if (!disabled) setIsVisible(true);
  }, [disabled]);

  const handleMouseLeave = React.useCallback(() => {
    setIsVisible(false);
  }, []);

  const handleFocus = React.useCallback(() => {
    if (!disabled) setIsFocused(true);
  }, [disabled]);

  const handleBlur = React.useCallback(() => {
    setIsFocused(false);
  }, []);

  const shouldShowTooltip = (isVisible || isFocused || forceVisible) && tooltip && !disabled;

  return (
    <div
      className={cs('tooltip-container', className)}
      onMouseEnter={handleMouseEnter}
      onMouseLeave={handleMouseLeave}
      onFocus={handleFocus}
      onBlur={handleBlur}
      ref={anchorElRef}
    >
      {children}
      {shouldShowTooltip && (
        <div 
          className={cs(
            theme.balloon,
            `direction-${direction}`,
            `alignment-${alignment}`
          )}
        >
          <div className={theme.container}>
            <div className={theme.arrow} />
            <div className={theme.content}>
              {tooltip}
            </div>
          </div>
        </div>
      )}
    </div>
  );
};

// Info Section component (equivalent to yS function)
export const InfoSection: React.FC<InfoSectionProps> = ({
  focusKey,
  tooltip,
  disableFocus = false,
  className,
  children,
}) => {
  const content = disableFocus ? (
    <FocusDisabledComponent
      className={cs(InfoSectionTheme.content, InfoSectionTheme.disableFocusHighlight)}
    >
      <ColumnComponent>
        {children}
      </ColumnComponent>
    </FocusDisabledComponent>
  ) : (
    <FocusableComponent
      focusKey={focusKey}
      className={InfoSectionTheme.content}
    >
      <ColumnComponent>
        {children}
      </ColumnComponent>
    </FocusableComponent>
  );

  return (
    <div className={cs(InfoSectionTheme.infoSection, className)}>
      <Tooltip
        tooltip={tooltip}
        direction="right"
        alignment="center"
      >
        {content}
      </Tooltip>
    </div>
  );
};

// Info Section Divider component (equivalent to jS function)
export const InfoSectionDivider: React.FC<DividerProps> = ({
  noMargin = false,
  className,
}) => (
  <div 
    className={cs(
      InfoSectionTheme.divider,
      { [InfoSectionTheme.noMargin]: noMargin },
      className
    )} 
  />
);

// Export utility functions and components
export { cs as classNames };

// Higher-order component for creating info sections with default styling
export const createInfoSection = (defaultProps: Partial<InfoSectionProps> = {}) => {
  return (props: InfoSectionProps) => (
    <InfoSection {...defaultProps} {...props} />
  );
};

// Utility function for creating tooltips
export const createTooltip = (tooltipContent: ReactNode, options: Partial<TooltipProps> = {}) => {
  return (props: { children: ReactNode }) => (
    <Tooltip tooltip={tooltipContent} {...options}>
      {props.children}
    </Tooltip>
  );
};