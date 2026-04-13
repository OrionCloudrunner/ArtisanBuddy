using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using System;
using System.Collections.Generic;

namespace ArtisanBuddy.Ipc;

internal sealed class ArtisanIpc
{
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IPluginLog _log;
    private readonly HashSet<string> _loggedFailures = new(StringComparer.Ordinal);

    public ArtisanIpc(IDalamudPluginInterface pluginInterface, IPluginLog log)
    {
        _pluginInterface = pluginInterface;
        _log = log;
    }

    public bool TryIsListRunning(out bool value)
        => TryInvokeFunc("Artisan.IsListRunning", out value);

    public bool TryIsBusy(out bool value)
        => TryInvokeFunc("Artisan.IsBusy", out value);

    public bool TryGetStopRequest(out bool value)
        => TryInvokeFunc("Artisan.GetStopRequest", out value);

    public bool TrySetStopRequest(bool value)
    {
        try
        {
            _pluginInterface.GetIpcSubscriber<bool, object>("Artisan.SetStopRequest").InvokeAction(value);
            return true;
        }
        catch (Exception ex)
        {
            LogFailureOnce("Artisan.SetStopRequest", ex);
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
            _log.Warning($"Artisan IPC call failed: {name} ({ex.GetType().Name}: {ex.Message})");
    }
}
