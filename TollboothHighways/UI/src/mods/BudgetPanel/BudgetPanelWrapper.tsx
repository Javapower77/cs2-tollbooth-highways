import React from 'react';
import { TollIncomeSection } from '../TollEconomyPanel/TollIncomeSection';

interface BudgetPageWrapperProps {
    originalPage: React.ComponentType<any>;
    [key: string]: any; // Allow any additional props
}

/**
 * Wrapper component that injects the TollIncomeSection into the Budget Page
 */
export const BudgetPageWrapper: React.FC<BudgetPageWrapperProps> = ({ originalPage: OriginalPage, ...props }) => {
    return (
        <>
            {/* Add toll income section at the top of the budget page */}
            <TollIncomeSection />
            
            {/* Render the original budget page below */}
            <OriginalPage {...props} />
        </>
    );
};

/**
 * Higher-order component that wraps the budget page
 */
export const createBudgetPageWrapper = (BudgetPage: React.ComponentType<any>) => {
    return (props: any) => {
        return <BudgetPageWrapper originalPage={BudgetPage} {...props} />;
    };
};