using ArtisanBuddy.Ipc;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using System;

namespace ArtisanBuddy;

internal sealed class ArbitrationController
{
    private const string NoItemsStatus = "No available items to gather";
    private const string PlayerBusyPrefix = "Player is busy";
    private const string TeleportingPrefix = "Teleporting";

    private readonly Configuration _configuration;
    private readonly GatherBuddyIpc _gatherBuddy;
    private readonly ArtisanIpc _artisan;
    private readonly IClientState _clientState;
    private readonly ICondition _condition;
    private readonly IPluginLog _log;

    private DateTime _nextPollUtc;
    private DateTime? _resumeEligibleSinceUtc;
    private DateTime? _teleportingSinceUtc;
    private DateTime? _pendingGatherBuddyEnableUtc;
    private uint _teleportingTerritory;
    private bool _wePausedArtisan;
    private bool _weDisabledGatherBuddyForRecovery;
    private bool? _lastIssuedStopRequest;
    private string _lastDecisionReason = string.Empty;
    private string _lastObservedStatus = string.Empty;

    public ArbitrationController(
        Configuration configuration,
        GatherBuddyIpc gatherBuddy,
        ArtisanIpc artisan,
        IClientState clientState,
        ICondition condition,
        IPluginLog log)
    {
        _configuration = configuration;
        _gatherBuddy = gatherBuddy;
        _artisan = artisan;
        _clientState = clientState;
        _condition = condition;
        _log = log;
    }

    public PluginStateSnapshot LastSnapshot { get; private set; } = new(
        false, false, false, "Unavailable", false, false, false, false, false, 0, false, false, "Starting up");

    public void Update()
    {
        if (!_configuration.Enabled)
        {
            if (_wePausedArtisan)
            {
                _artisan.TryGetStopRequest(out var stopRequested);
                _artisan.TryIsListRunning(out var listRunning);
                ResumeArtisanIfWePausedIt(stopRequested, listRunning, reason: "Plugin disabled");
            }

            RestoreGatherBuddyIfWeDisabledIt(reason: "Plugin disabled");

            ResetTransientState();
            LastSnapshot = LastSnapshot with { ShouldAllowCrafting = false, DecisionReason = "Plugin disabled" };
            return;
        }

        var now = DateTime.UtcNow;
        if (now < _nextPollUtc)
            return;

        _nextPollUtc = now.AddMilliseconds(Math.Max(100, _configuration.PollIntervalMilliseconds));

        if (_pendingGatherBuddyEnableUtc is DateTime pendingAt && now >= pendingAt)
        {
            if (_gatherBuddy.TrySetAutoGatherEnabled(true))
            {
                _weDisabledGatherBuddyForRecovery = false;
                LogVerbose("Re-enabled GatherBuddy after teleport-stall recovery.");
                _pendingGatherBuddyEnableUtc = null;
            }
            else
            {
                _pendingGatherBuddyEnableUtc = now.AddMilliseconds(Math.Max(500, _configuration.GatherBuddyReenableDelayMilliseconds));
                LogVerbose("Failed to re-enable GatherBuddy after teleport-stall recovery; will retry.");
            }
        }

        var gatherBuddyEnabled = false;
        var gatherBuddyWaiting = false;
        string gatherBuddyStatus = string.Empty;
        var gatherBuddyAvailable = _gatherBuddy.TryIsAutoGatherEnabled(out gatherBuddyEnabled)
            && _gatherBuddy.TryIsAutoGatherWaiting(out gatherBuddyWaiting)
            && _gatherBuddy.TryGetAutoGatherStatus(out gatherBuddyStatus);

        gatherBuddyStatus ??= string.Empty;

        var artisanBusy = false;
        var artisanListRunning = false;
        var artisanStopRequested = false;
        var artisanAvailable = _artisan.TryIsBusy(out artisanBusy)
            && _artisan.TryIsListRunning(out artisanListRunning)
            && _artisan.TryGetStopRequest(out artisanStopRequested);

        var inOwnApartment = HousingHelper.IsInsideOwnApartment();
        var territoryType = _clientState.TerritoryType;
        var betweenAreas = _condition[ConditionFlag.BetweenAreas];

        var shouldAllowCrafting = gatherBuddyAvailable
            && gatherBuddyEnabled
            && gatherBuddyWaiting
            && string.Equals(gatherBuddyStatus, NoItemsStatus, StringComparison.OrdinalIgnoreCase)
            && (!_configuration.OnlyResumeCraftingInOwnApartment || inOwnApartment);

        var shouldIgnorePlayerBusy = ShouldIgnorePlayerBusyNoise(gatherBuddyStatus, artisanBusy, artisanListRunning);

        var decisionReason = BuildDecisionReason(
            gatherBuddyAvailable,
            gatherBuddyEnabled,
            gatherBuddyWaiting,
            gatherBuddyStatus,
            inOwnApartment,
            shouldAllowCrafting,
            shouldIgnorePlayerBusy);

        LastSnapshot = new PluginStateSnapshot(
            gatherBuddyAvailable,
            gatherBuddyEnabled,
            gatherBuddyWaiting,
            gatherBuddyStatus,
            artisanAvailable,
            artisanBusy,
            artisanListRunning,
            artisanStopRequested,
            inOwnApartment,
            territoryType,
            betweenAreas,
            shouldAllowCrafting,
            decisionReason);

        if (!string.Equals(_lastDecisionReason, decisionReason, StringComparison.Ordinal))
        {
            LogVerbose($"Decision: {decisionReason}");
            _lastDecisionReason = decisionReason;
        }

        if (!string.Equals(_lastObservedStatus, gatherBuddyStatus, StringComparison.Ordinal))
        {
            LogVerbose($"GatherBuddy status: {gatherBuddyStatus}");
            _lastObservedStatus = gatherBuddyStatus;
        }

        if (!gatherBuddyAvailable || !artisanAvailable)
        {
            ResetTransientState();
            return;
        }

        if (!gatherBuddyEnabled)
        {
            _resumeEligibleSinceUtc = null;

            if (_pendingGatherBuddyEnableUtc != null)
                return;

            ResetTeleportWatchdog();
            ResumeArtisanIfWePausedIt(artisanStopRequested, artisanListRunning, reason: "GatherBuddy disabled");
            return;
        }

        if (shouldAllowCrafting)
        {
            ResetTeleportWatchdog();
            _resumeEligibleSinceUtc ??= now;

            if (_wePausedArtisan && now - _resumeEligibleSinceUtc >= TimeSpan.FromSeconds(Math.Max(0, _configuration.ResumeDebounceSeconds)))
                ResumeArtisanIfWePausedIt(artisanStopRequested, artisanListRunning, reason: "GatherBuddy idle window reopened");

            return;
        }

        if (shouldIgnorePlayerBusy)
        {
            ResetTeleportWatchdog();
            _resumeEligibleSinceUtc = null;
            return;
        }

        _resumeEligibleSinceUtc = null;

        if ((artisanListRunning || artisanBusy) && !artisanStopRequested)
            PauseArtisan(reason: $"GatherBuddy active: {gatherBuddyStatus}");

        HandleTeleportWatchdog(now, gatherBuddyEnabled, gatherBuddyStatus, territoryType, betweenAreas, artisanBusy);
    }

    public void Dispose()
    {
        if (_wePausedArtisan)
            ResumeArtisanIfWePausedIt(LastSnapshot.ArtisanStopRequested, LastSnapshot.ArtisanListRunning, reason: "Plugin disposed");

        RestoreGatherBuddyIfWeDisabledIt(reason: "Plugin disposed");
    }

    private void PauseArtisan(string reason)
    {
        if (_lastIssuedStopRequest == true)
            return;

        if (_artisan.TrySetStopRequest(true))
        {
            _wePausedArtisan = true;
            _lastIssuedStopRequest = true;
            _log.Information($"Pausing Artisan: {reason}");
        }
    }

    private void ResumeArtisanIfWePausedIt(bool artisanStopRequested, bool artisanListRunning, string reason)
    {
        if (!_wePausedArtisan)
            return;

        if (!artisanStopRequested && !artisanListRunning)
        {
            _wePausedArtisan = false;
            _lastIssuedStopRequest = null;
            return;
        }

        if (_lastIssuedStopRequest == false)
            return;

        if (_artisan.TrySetStopRequest(false))
        {
            _wePausedArtisan = false;
            _lastIssuedStopRequest = false;
            _log.Information($"Resuming Artisan: {reason}");
        }
    }

    private void HandleTeleportWatchdog(DateTime now, bool gatherBuddyEnabled, string gatherBuddyStatus, uint territoryType, bool betweenAreas, bool artisanBusy)
    {
        if (!_configuration.EnableTeleportRecovery || !gatherBuddyEnabled)
        {
            ResetTeleportWatchdog();
            return;
        }

        if (_pendingGatherBuddyEnableUtc != null)
            return;

        if (!gatherBuddyStatus.StartsWith(TeleportingPrefix, StringComparison.OrdinalIgnoreCase) || betweenAreas)
        {
            ResetTeleportWatchdog();
            return;
        }

        if (_teleportingSinceUtc == null || _teleportingTerritory != territoryType)
        {
            _teleportingSinceUtc = now;
            _teleportingTerritory = territoryType;
            return;
        }

        if (artisanBusy || !IsSafeToRetryTeleport())
            return;

        var stallThreshold = TimeSpan.FromSeconds(Math.Max(1, _configuration.TeleportStallSeconds));
        if (now - _teleportingSinceUtc < stallThreshold)
            return;

        if (_gatherBuddy.TrySetAutoGatherEnabled(false))
        {
            _weDisabledGatherBuddyForRecovery = true;
            _pendingGatherBuddyEnableUtc = now.AddMilliseconds(Math.Max(500, _configuration.GatherBuddyReenableDelayMilliseconds));
            _teleportingSinceUtc = now;
            _log.Warning($"GatherBuddy appears stuck in '{gatherBuddyStatus}'. Bouncing auto-gather to recover.");
        }
    }

    private void RestoreGatherBuddyIfWeDisabledIt(string reason)
    {
        if (!_weDisabledGatherBuddyForRecovery)
            return;

        if (_gatherBuddy.TrySetAutoGatherEnabled(true))
        {
            _log.Information($"Re-enabled GatherBuddy: {reason}");
            _weDisabledGatherBuddyForRecovery = false;
        }
    }

    private string BuildDecisionReason(
        bool gatherBuddyAvailable,
        bool gatherBuddyEnabled,
        bool gatherBuddyWaiting,
        string gatherBuddyStatus,
        bool inOwnApartment,
        bool shouldAllowCrafting,
        bool shouldIgnorePlayerBusy)
    {
        if (!gatherBuddyAvailable)
            return "GatherBuddy Reborn IPC unavailable";

        if (!gatherBuddyEnabled)
            return "GatherBuddy auto-gather disabled";

        if (shouldAllowCrafting)
            return _configuration.OnlyResumeCraftingInOwnApartment
                ? "Crafting allowed: GBR idle and character is inside own apartment"
                : "Crafting allowed: GBR idle";

        if (_configuration.OnlyResumeCraftingInOwnApartment && !inOwnApartment && string.Equals(gatherBuddyStatus, NoItemsStatus, StringComparison.OrdinalIgnoreCase))
            return "GBR idle, but apartment-only resume blocked crafting";

        if (shouldIgnorePlayerBusy)
            return "Ignoring GBR 'Player is busy' noise while Artisan currently owns crafting";

        if (!gatherBuddyWaiting)
            return $"GatherBuddy active: {gatherBuddyStatus}";

        return $"Crafting blocked by GatherBuddy state: {gatherBuddyStatus}";
    }

    private bool ShouldIgnorePlayerBusyNoise(string gatherBuddyStatus, bool artisanBusy, bool artisanListRunning)
    {
        if (!_configuration.IgnorePlayerBusyWhileArtisanCrafts)
            return false;

        if (!artisanBusy && !artisanListRunning)
            return false;

        return gatherBuddyStatus.StartsWith(PlayerBusyPrefix, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsSafeToRetryTeleport()
    {
        return !_condition[ConditionFlag.Crafting]
            && !_condition[ConditionFlag.PreparingToCraft]
            && !_condition[ConditionFlag.ExecutingCraftingAction]
            && !_condition[ConditionFlag.Occupied]
            && !_condition[ConditionFlag.OccupiedInQuestEvent]
            && !_condition[ConditionFlag.Mounting]
            && !_condition[ConditionFlag.Mounting71]
            && !_condition[ConditionFlag.Casting]
            && !_condition[ConditionFlag.Gathering]
            && !_condition[ConditionFlag.ExecutingGatheringAction];
    }

    private void ResetTransientState()
    {
        _resumeEligibleSinceUtc = null;
        ResetTeleportWatchdog();
    }

    private void ResetTeleportWatchdog()
    {
        _teleportingSinceUtc = null;
        _teleportingTerritory = 0;
        _pendingGatherBuddyEnableUtc = null;
    }

    private void LogVerbose(string message)
    {
        if (_configuration.VerboseLogging)
            _log.Debug(message);
    }
}
