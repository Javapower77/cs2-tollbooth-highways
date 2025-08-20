import React from 'react';

// Default CSS classes theme (equivalent to Vue object)
const DescriptionRowCSS = {
  "description-row": "description-row_Xb1 info-row_QQ9 item-focused_FuT",
  descriptionRow: "description-row_Xb1 info-row_QQ9 item-focused_FuT",
};

// Component props interface
interface DescriptionRowProps {
  children: React.ReactNode;
  className?: string;
}

// Simple Description Row component (equivalent to Kue function)
const DescriptionRowComponent: React.FC<DescriptionRowProps> = ({ children, className }) => {
  const finalClassName = className 
    ? `${DescriptionRowCSS.descriptionRow} ${className}` 
    : DescriptionRowCSS.descriptionRow;

  return React.createElement("div", {
    className: finalClassName,
  }, children);
};

// Export the component and theme
export const DescriptionRow = DescriptionRowComponent;
export const DescriptionRowTheme = DescriptionRowCSS;
export default DescriptionRow;