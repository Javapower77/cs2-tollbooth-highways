import React from 'react';
import { TollIncomeSection } from '../TollEconomyPanel/TollIncomeSection';

interface BudgetPanelWrapperProps {
    originalPanel: React.ComponentType<any>;
    [key: string]: any; // Allow any additional props
}

/**
 * Wrapper component that injects the TollIncomeSection into the Budget Panel
 */
export const BudgetPanelWrapper: React.FC<BudgetPanelWrapperProps> = ({ originalPanel: OriginalPanel, ...props }) => {
    return (
        <>
            {/* Add toll income section at the top of the budget panel */}
            <TollIncomeSection />
            
            {/* Render the original budget panel below */}
            <OriginalPanel {...props} />
        </>
    );
};

/**
 * Higher-order component that wraps the budget panel
 */
export const createBudgetPanelWrapper = (BudgetPanel: React.ComponentType<any>) => {
    return (props: any) => {
        return <BudgetPanelWrapper originalPanel={BudgetPanel} {...props} />;
    };
};