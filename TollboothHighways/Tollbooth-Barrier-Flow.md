flowchart TD
    A[OnUpdate] --> B[CheckVehiclesApproachingBarriers]
    A --> C[ProcessBarrierVehicles]
    A --> D[CleanupFinishedVehicles]
    A --> E[Process Barrier Close Queue]
    
    B --> B1[Query All Toll Roads]
    B1 --> B2[CheckRoadForApproachingVehicles]
    B2 --> B3[CheckLaneForVehicles]
    B3 --> B4{Vehicle Close to Barrier?}
    B4 -->|Yes| B5[SetupVehiclePetitioning]
    B5 --> B6[Add to ProcessingVehicles]
    B6 --> B7[Set Lane Signal Petitioner]
    
    C --> C1{For Each Processing Vehicle}
    C1 --> C2{Is Processing?}
    C2 -->|No| C3[SetupBarrierStop]
    C3 --> C4[Add Blocker Component]
    C3 --> C5[Set Start Time]
    
    C2 -->|Yes| C6{Processing Time Elapsed?}
    C6 -->|No| C7[Continue Waiting]
    C6 -->|Yes| C8[ReleaseVehicleFromBarrier]
    
    C8 --> C9[Remove Blocker Component]
    C8 --> C10[OpenBarrier]
    C8 --> C11[CloseBarrierAfterDelay]
    
    C10 --> C12[Set Lane Signals to GO]
    C10 --> C13[Set Traffic Lights to GREEN]
    
    C11 --> C14[Add to Close Queue]
    
    E --> E1{Close Time Reached?}
    E1 -->|Yes| E2[CloseBarrier]
    E2 --> E3[Set Lane Signals to STOP]
    E2 --> E4[Set Traffic Lights to RED]
    
    D --> D1[CleanupVehiclePetitioning]
    D1 --> D2[Clear Lane Signal Petitioner]
    
    style A fill:#e1f5fe
    style B fill:#f3e5f5
    style C fill:#e8f5e8
    style C8 fill:#fff3e0
    style C10 fill:#e8f5e8
    style E2 fill:#ffebee