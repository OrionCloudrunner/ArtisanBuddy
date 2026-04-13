using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;

namespace ArtisanBuddy.Windows;

internal sealed class MainWindow : Window, IDisposable
{
    private readonly Plugin _plugin;
    private readonly ArbitrationController _controller;

    public MainWindow(Plugin plugin, ArbitrationController controller)
        : base("ArtisanBuddy###ArtisanBuddyMain")
    {
        _plugin = plugin;
        _controller = controller;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new System.Numerics.Vector2(560, 420),
            MaximumSize = new System.Numerics.Vector2(1200, 900),
        };
    }

    public void Dispose()
    {
    }

    public override void Draw()
    {
        var snapshot = _controller.LastSnapshot;
        var configuration = _plugin.Configuration;
        var changed = false;

        ImGui.TextUnformatted("ArtisanBuddy arbitrates between GatherBuddy Reborn and Artisan crafting lists.");
        ImGui.TextWrapped("It only resumes Artisan during GatherBuddy Reborn's true dead time, then preempts crafting immediately when GBR has work again.");
        ImGui.Separator();

        if (ImGui.CollapsingHeader("Status", ImGuiTreeNodeFlags.DefaultOpen))
        {
            DrawBool("Plugin enabled", configuration.Enabled);
            DrawBool("GatherBuddy IPC available", snapshot.GatherBuddyAvailable);
            DrawBool("Artisan IPC available", snapshot.ArtisanAvailable);
            DrawBool("GatherBuddy enabled", snapshot.GatherBuddyEnabled);
            DrawBool("GatherBuddy waiting", snapshot.GatherBuddyWaiting);
            DrawBool("Artisan list running", snapshot.ArtisanListRunning);
            DrawBool("Artisan busy", snapshot.ArtisanBusy);
            DrawBool("Artisan stop requested", snapshot.ArtisanStopRequested);
            DrawBool("Inside own apartment", snapshot.InOwnApartment);
            DrawBool("Between areas", snapshot.BetweenAreas);
            DrawBool("Crafting currently allowed", snapshot.ShouldAllowCrafting);

            ImGui.Spacing();
            ImGui.TextUnformatted($"GatherBuddy status: {snapshot.GatherBuddyStatus}");
            ImGui.TextWrapped($"Decision: {snapshot.DecisionReason}");
            ImGui.TextUnformatted($"Territory: {snapshot.TerritoryType}");
        }

        if (ImGui.CollapsingHeader("Settings", ImGuiTreeNodeFlags.DefaultOpen))
        {
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
            ImGui.TextWrapped("When enabled, ArtisanBuddy will only let Artisan resume once GBR is idle and you are inside your own apartment.");

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
        }

        if (changed)
            configuration.Save();
    }

    private static void DrawBool(string label, bool value)
    {
        ImGui.TextUnformatted($"{label}: {(value ? "Yes" : "No")}");
    }
}
