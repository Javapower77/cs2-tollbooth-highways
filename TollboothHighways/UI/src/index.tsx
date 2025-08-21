import { ModRegistrar } from "cs2/modding";
import { MousePositionPanel } from "mods/mouse-position-panel";
import { CS2VanillaUIResolver } from "mods/CS2VanillaUIResolver";

import { TollboothSelectedInfoPanelComponent } from "mods/TollboothSelectedInfoPanel/TollBoothFields";

const register: ModRegistrar = (moduleRegistry) => {

    CS2VanillaUIResolver.setRegistry(moduleRegistry);

   // moduleRegistry.append('Game', MousePositionPanel);  
    
    moduleRegistry.extend("game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx", 'selectedInfoSectionComponents', TollboothSelectedInfoPanelComponent);

}

export default register;