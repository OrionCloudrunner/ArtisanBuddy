using ArtisanBuddy.Ipc;
using ArtisanBuddy.Windows;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace ArtisanBuddy;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/artisanbuddy";

    private readonly ConfigWindow _configWindow;
    private readonly MainWindow _mainWindow;
    private readonly ArbitrationController _controller;

    public Configuration Configuration { get; }
    public WindowSystem WindowSystem { get; } = new("ArtisanBuddy");

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        var artisanIpc = new ArtisanIpc(PluginInterface, Log);
        var gatherBuddyIpc = new GatherBuddyIpc(PluginInterface, Log);
        _controller = new ArbitrationController(Configuration, gatherBuddyIpc, artisanIpc, ClientState, Condition, Log);

        _configWindow = new ConfigWindow(this);
        _mainWindow = new MainWindow(this, _controller);

        WindowSystem.AddWindow(_configWindow);
        WindowSystem.AddWindow(_mainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open ArtisanBuddy status and settings."
        });

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        Framework.Update += OnFrameworkUpdate;

        Log.Information("ArtisanBuddy loaded.");
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        _controller.Dispose();
        _configWindow.Dispose();
        _mainWindow.Dispose();
        WindowSystem.RemoveAllWindows();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnFrameworkUpdate(IFramework framework)
        => _controller.Update();

    private void OnCommand(string command, string arguments)
        => ToggleMainUi();

    public void ToggleConfigUi()
        => _configWindow.Toggle();

    public void ToggleMainUi()
        => _mainWindow.Toggle();
}
