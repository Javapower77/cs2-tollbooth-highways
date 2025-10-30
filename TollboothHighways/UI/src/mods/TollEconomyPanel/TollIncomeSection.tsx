import React from 'react';
import { bindValue, useValue } from "cs2/api";
import { LocalizedNumber, Unit } from "cs2/l10n";
import mod from "../../../mod.json";

// Bind to C# data
const tollIncomeDaily$ = bindValue<number>(mod.id, "tollIncomeDaily");
const tollIncomeWeekly$ = bindValue<number>(mod.id, "tollIncomeWeekly");
const tollIncomeMonthly$ = bindValue<number>(mod.id, "tollIncomeMonthly");
const tollIncomeTotal$ = bindValue<number>(mod.id, "tollIncomeTotal");
const tollVehiclesDaily$ = bindValue<number>(mod.id, "tollVehiclesDaily");
const tollVehiclesTotal$ = bindValue<number>(mod.id, "tollVehiclesTotal");
const hasTollIncome$ = bindValue<boolean>(mod.id, "hasTollIncome");

export const TollIncomeSection: React.FC = () => {
    const dailyIncome = useValue(tollIncomeDaily$);
    const weeklyIncome = useValue(tollIncomeWeekly$);
    const monthlyIncome = useValue(tollIncomeMonthly$);
    const totalIncome = useValue(tollIncomeTotal$);
    const dailyVehicles = useValue(tollVehiclesDaily$);
    const totalVehicles = useValue(tollVehiclesTotal$);
    const hasTollIncome = useValue(hasTollIncome$);

    // Only show if there's toll income
    if (!hasTollIncome) {
        //return null;
    }

    return (
        <div className="economy-toll-income-section" style={{
            padding: '10px',
            backgroundColor: 'rgba(0, 100, 0, 0.1)',
            borderLeft: '3px solid #00ff00',
            marginBottom: '10px'
        }}>
            <h3 style={{ 
                fontSize: '14px', 
                fontWeight: 'bold',
                color: '#ffffff',
                marginBottom: '8px'
            }}>
                Toll Revenue
            </h3>
            
            <div style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span>Daily Income:</span>
                    <LocalizedNumber unit={Unit.Money} value={dailyIncome} />
                </div>
                
                <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span>Weekly Income:</span>
                    <LocalizedNumber unit={Unit.Money} value={weeklyIncome} />
                </div>
                
                <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <span>Monthly Income:</span>
                    <LocalizedNumber unit={Unit.Money} value={monthlyIncome} />
                </div>
                
                <div style={{ 
                    display: 'flex', 
                    justifyContent: 'space-between',
                    paddingTop: '4px',
                    borderTop: '1px solid rgba(255, 255, 255, 0.2)',
                    fontWeight: 'bold'
                }}>
                    <span>Total Revenue:</span>
                    <LocalizedNumber unit={Unit.Money} value={totalIncome} />
                </div>
                
                <div style={{ 
                    display: 'flex', 
                    justifyContent: 'space-between',
                    fontSize: '11px',
                    color: '#cccccc',
                    marginTop: '4px'
                }}>
                    <span>Vehicles Today:</span>
                    <LocalizedNumber unit={Unit.Integer} value={dailyVehicles} />
                </div>
            </div>
        </div>
    );
};