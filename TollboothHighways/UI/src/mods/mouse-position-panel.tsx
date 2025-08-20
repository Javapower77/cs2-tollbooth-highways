import { bindValue, bindTrigger, useValue } from "cs2/api";
import { Button } from "cs2/ui";
import { FC } from "react";
import styles from "./mouse-position-panel.module.scss";

// Create bindings to connect with C# system
const showPanel$ = bindValue<boolean>("mousePosition", "showPanel");
const screenPosition$ = bindValue<{ x: number; y: number }>("mousePosition", "screenPosition");
const worldPosition$ = bindValue<{ x: number; y: number; z: number }>("mousePosition", "worldPosition");
const formattedPosition$ = bindValue<string>("mousePosition", "formattedPosition");
const togglePanel$ = bindTrigger("mousePosition", "togglePanel");

export const MousePositionPanel: FC = () => {
    // Subscribe to the bindings from the C# system
    const showPanel = useValue(showPanel$);
    const screenPos = useValue(screenPosition$);
    const worldPos = useValue(worldPosition$);
    const formattedPos = useValue(formattedPosition$);

    return (
        <>
            {/* Toggle Button */}
            <Button
                className={styles.toggleButton}
                onClick={togglePanel$}
                variant="flat"
            >
                📍
            </Button>

            {/* Main Panel */}
            {showPanel && (
                <div className={styles.mousePanel}>
                    <div className={styles.panelHeader}>
                        🖱️ Mouse Position Tracker
                    </div>
                    
                    <div className={styles.positionInfo}>
                        <div className={`${styles.coordinates} ${styles.screenCoords}`}>
                            <div className={styles.positionRow}>
                                <span className={styles.positionLabel}>Screen X:</span>
                                <span className={styles.positionValue}>
                                    {screenPos?.x?.toFixed(0) ?? "0"}
                                </span>
                            </div>
                            <div className={styles.positionRow}>
                                <span className={styles.positionLabel}>Screen Y:</span>
                                <span className={styles.positionValue}>
                                    {screenPos?.y?.toFixed(0) ?? "0"}
                                </span>
                            </div>
                        </div>

                        <div className={`${styles.coordinates} ${styles.worldCoords}`}>
                            <div className={styles.positionRow}>
                                <span className={styles.positionLabel}>World X:</span>
                                <span className={styles.positionValue}>
                                    {worldPos?.x?.toFixed(2) ?? "N/A"}
                                </span>
                            </div>
                            <div className={styles.positionRow}>
                                <span className={styles.positionLabel}>World Y:</span>
                                <span className={styles.positionValue}>
                                    {worldPos?.y?.toFixed(2) ?? "N/A"}
                                </span>
                            </div>
                            <div className={styles.positionRow}>
                                <span className={styles.positionLabel}>World Z:</span>
                                <span className={styles.positionValue}>
                                    {worldPos?.z?.toFixed(2) ?? "N/A"}
                                </span>
                            </div>
                        </div>

                        <div className={styles.formattedPositionRow}>
                            <span>{formattedPos}</span>
                        </div>
                    </div>
                </div>
            )}
        </>
    );
};