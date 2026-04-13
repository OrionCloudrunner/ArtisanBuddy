namespace ArtisanBuddy;

internal sealed record PluginStateSnapshot(
    bool GatherBuddyAvailable,
    bool GatherBuddyEnabled,
    bool GatherBuddyWaiting,
    string GatherBuddyStatus,
    bool ArtisanAvailable,
    bool ArtisanBusy,
    bool ArtisanListRunning,
    bool ArtisanStopRequested,
    bool InOwnApartment,
    uint TerritoryType,
    bool BetweenAreas,
    bool ShouldAllowCrafting,
    string DecisionReason
);
