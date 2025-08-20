import { TransportRouteMainPanel } from "./TransportRouteMainPanel";

interface InfoSectionComponent {
	group: string;
	tooltipKeys: Array<string>;
	tooltipTags: Array<string>;
}


export const TransportSelectedInfoPanelComponent = (componentList: any): any => {
	// I believe you should not put anything here.
	componentList["TollboothHighways.Systems.TransportRouteUISystem"] = (e: InfoSectionComponent) => {
		return TransportRouteMainPanel();
	}
	console.log("TransportRouteMainPanel called", componentList);
	return componentList as any;
}