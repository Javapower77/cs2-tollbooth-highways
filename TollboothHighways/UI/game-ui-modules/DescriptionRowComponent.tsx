import React from 'react';

// CSS theme classes for Description Row (equivalent to Vue object)
export const DescriptionRowTheme = {
  "description-row": "description-row_Xb1 info-row_QQ9 item-focused_FuT",
  descriptionRow: "description-row_Xb1 info-row_QQ9 item-focused_FuT",
};

// Component interface
export interface DescriptionRowProps {
  children: React.ReactNode;
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

// Description Row component (equivalent to Kue function)
export const DescriptionRow: React.FC<DescriptionRowProps> = ({ 
  children, 
  className 
}) => {
  return React.createElement("div", {
    className: classNames(DescriptionRowTheme.descriptionRow, className),
    children: children
  });
};

// Alternative implementation using a more functional approach
export const createDescriptionRow = (theme = DescriptionRowTheme) => {
  return ({ children, className }: DescriptionRowProps) => {
    return React.createElement("div", {
      className: classNames(theme.descriptionRow, className),
    }, children);
  };
};

// Export utility functions
export { classNames };

// Export for backward compatibility
export default DescriptionRow;