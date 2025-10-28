import { ModRegistrar } from "cs2/modding";
import { MousePositionPanel } from "mods/mouse-position-panel";
import { CS2VanillaUIResolver } from "mods/CS2VanillaUIResolver";
import { TollboothSelectedInfoPanelComponent } from "mods/TollboothSelectedInfoPanel/TollBoothFields";
import { TollIncomeSection } from "mods/TollEconomyPanel/TollIncomeSection";

const register: ModRegistrar = (moduleRegistry) => {

    CS2VanillaUIResolver.setRegistry(moduleRegistry);

   // moduleRegistry.append('Game', MousePositionPanel);  
    
    // Register tollbooth info panel
    moduleRegistry.extend("game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx", 'selectedInfoSectionComponents', TollboothSelectedInfoPanelComponent);

    // Register toll income in economy panel - inject into the budget tab
    moduleRegistry.extend("game-ui/game/components/economy-panel/budget-panel/budget-panel.tsx", 'BudgetPanel', (BudgetPanel: any) => {
        return (props: any) => {
            const originalPanel = BudgetPanel(props);
            
            // Inject our toll income section into the revenue section
            return React.createElement(React.Fragment, null,
                React.createElement(TollIncomeSection),
                originalPanel
            );
        };
    });
}

export default register;