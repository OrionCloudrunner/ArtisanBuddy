using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;

namespace ArtisanBuddy.Windows;

internal sealed class ConfigWindow : Window, IDisposable
{
    private readonly Plugin _plugin;

    public ConfigWindow(Plugin plugin)
        : base("ArtisanBuddy Settings###ArtisanBuddyConfig")
    {
        _plugin = plugin;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new System.Numerics.Vector2(440, 240),
            MaximumSize = new System.Numerics.Vector2(900, 900),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        var configuration = _plugin.Configuration;
        var changed = false;

        var enabled = configuration.Enabled;
        if (ImGui.Checkbox("Enable arbitration", ref enabled))
        {
            configuration.Enabled = enabled;
            changed = true;
        }

        var apartmentOnly = configuration.OnlyResumeCraftingInOwnApartment;
        if (ImGui.Checkbox("Only resume crafting in own apartment", ref apartmentOnly))
        {
            configuration.OnlyResumeCraftingInOwnApartment = apartmentOnly;
            changed = true;
        }
        ImGui.TextWrapped("This defaults on. Crafting only resumes when GatherBuddy is idle and the character is inside their own apartment.");

        var teleportRecovery = configuration.EnableTeleportRecovery;
        if (ImGui.Checkbox("Recover GatherBuddy if teleport stalls", ref teleportRecovery))
        {
            configuration.EnableTeleportRecovery = teleportRecovery;
            changed = true;
        }

        var ignoreBusyNoise = configuration.IgnorePlayerBusyWhileArtisanCrafts;
        if (ImGui.Checkbox("Ignore GBR 'Player is busy' while Artisan is crafting", ref ignoreBusyNoise))
        {
            configuration.IgnorePlayerBusyWhileArtisanCrafts = ignoreBusyNoise;
            changed = true;
        }
        ImGui.TextWrapped("This suppresses stop/restart thrash caused by GatherBuddy reporting 'Player is busy...' just because Artisan currently owns the craft.");

        var verboseLogging = configuration.VerboseLogging;
        if (ImGui.Checkbox("Verbose logging", ref verboseLogging))
        {
            configuration.VerboseLogging = verboseLogging;
            changed = true;
        }

        var resumeDebounceSeconds = configuration.ResumeDebounceSeconds;
        if (ImGui.InputInt("Resume debounce (seconds)", ref resumeDebounceSeconds))
        {
            configuration.ResumeDebounceSeconds = Math.Clamp(resumeDebounceSeconds, 0, 60);
            changed = true;
        }

        var pollIntervalMs = configuration.PollIntervalMilliseconds;
        if (ImGui.InputInt("Poll interval (ms)", ref pollIntervalMs))
        {
            configuration.PollIntervalMilliseconds = Math.Clamp(pollIntervalMs, 100, 5000);
            changed = true;
        }

        var teleportStallSeconds = configuration.TeleportStallSeconds;
        if (ImGui.InputInt("Teleport stall threshold (seconds)", ref teleportStallSeconds))
        {
            configuration.TeleportStallSeconds = Math.Clamp(teleportStallSeconds, 1, 30);
            changed = true;
        }

        var reenableDelayMs = configuration.GatherBuddyReenableDelayMilliseconds;
        if (ImGui.InputInt("GatherBuddy re-enable delay (ms)", ref reenableDelayMs))
        {
            configuration.GatherBuddyReenableDelayMilliseconds = Math.Clamp(reenableDelayMs, 500, 10000);
            changed = true;
        }

        if (changed)
            configuration.Save();
    }
}
