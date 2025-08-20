import { TollboothMainPanelComponent } from "mods/TollboothSelectedInfoPanel/TollboothMainPanelComponent";

interface InfoSectionComponent {
	group: string;
	tooltipKeys: Array<string>;
	tooltipTags: Array<string>;
}


export const TollboothSelectedInfoPanelComponent = (componentList: any): any => {
	// I believe you should not put anything here.
	componentList["TollboothHighways.Systems.TollBoothInfoUISystem"] = (e: InfoSectionComponent) => {
		return TollboothMainPanelComponent();
	}
    console.log("TollboothSelectedInfoPanelComponent called", componentList);
	return componentList as any;
}