using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;

namespace ArtisanBuddy.Ipc;

internal sealed class GatherBuddyIpc
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IPluginLog _log;
    private readonly HashSet<string> _loggedFailures = new(StringComparer.Ordinal);

    public GatherBuddyIpc(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        _pluginInterface = pluginInterface;
        _log = log;
    }

    public bool TryIsAutoGatherEnabled(out bool value)
        => TryInvokeFunc("GatherBuddyReborn.IsAutoGatherEnabled", out value);

    public bool TryIsAutoGatherWaiting(out bool value)
        => TryInvokeFunc("GatherBuddyReborn.IsAutoGatherWaiting", out value);

    public bool TryGetAutoGatherStatus(out string value)
        => TryInvokeFunc("GatherBuddyReborn.GetAutoGatherStatusText", out value);

    public bool TrySetAutoGatherEnabled(bool value)
    {
        try
        {
            _pluginInterface.GetIpcSubscriber<bool, object>("GatherBuddyReborn.SetAutoGatherEnabled").InvokeAction(value);
            return true;
        }
        catch (Exception ex)
        {
            LogFailureOnce("GatherBuddyReborn.SetAutoGatherEnabled", ex);
            return false;
        }
    }

    private bool TryInvokeFunc<T>(string name, out T value)
    {
        try
        {
            value = _pluginInterface.GetIpcSubscriber<T>(name).InvokeFunc();
            return true;
        }
        catch (Exception ex)
        {
            LogFailureOnce(name, ex);
            value = default!;
            return false;
        }
    }

    private void LogFailureOnce(string name, Exception ex)
    {
        if (_loggedFailures.Add(name))
            _log.Warning($"GatherBuddy Reborn IPC call failed: {name} ({ex.GetType().Name}: {ex.Message})");
    }
}
