using Dalamud.Configuration;
using System;

namespace ArtisanBuddy;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;
    public bool OnlyResumeCraftingInOwnApartment { get; set; } = true;
    public int ResumeDebounceSeconds { get; set; } = 3;
    public int PollIntervalMilliseconds { get; set; } = 250;
    public int TeleportStallSeconds { get; set; } = 4;
    public int GatherBuddyReenableDelayMilliseconds { get; set; } = 1500;
    public bool EnableTeleportRecovery { get; set; } = true;
    public bool IgnorePlayerBusyWhileArtisanCrafts { get; set; } = true;
    public bool VerboseLogging { get; set; } = false;

    public void Save()
        => Plugin.PluginInterface.SavePluginConfig(this);
}
