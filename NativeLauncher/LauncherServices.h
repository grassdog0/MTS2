#ifndef MOD_THE_SPIRE_2_LAUNCHER_SERVICES_H
#define MOD_THE_SPIRE_2_LAUNCHER_SERVICES_H

typedef struct Mts2ModDiscoveryService {
    void (*discoverRoot)(const WCHAR* root, const WCHAR* source);
} Mts2ModDiscoveryService;

typedef struct Mts2LoadOrderService {
    void (*sortForCurrentSettings)(void);
    void (*sortForDiagnostics)(void);
    BOOL (*validateSelected)(WCHAR* message, int capacity);
} Mts2LoadOrderService;

typedef struct Mts2GroupingService {
    void (*applyGroups)(void);
} Mts2GroupingService;

typedef struct Mts2SettingsService {
    void (*write)(BOOL modded);
} Mts2SettingsService;

typedef struct Mts2LaunchService {
    BOOL (*startGame)(void);
} Mts2LaunchService;

typedef struct Mts2LauncherServices {
    Mts2ModDiscoveryService discovery;
    Mts2LoadOrderService loadOrder;
    Mts2GroupingService grouping;
    Mts2SettingsService settings;
    Mts2LaunchService launch;
} Mts2LauncherServices;

#endif
