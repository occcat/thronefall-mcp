using BepInEx;
using ThronefallControl.Config;
using ThronefallControl.Game;
using ThronefallControl.Http;

namespace ThronefallControl;

[BepInPlugin(PluginInfo.Id, PluginInfo.Name, PluginInfo.Version)]
public sealed class Plugin : BaseUnityPlugin
{
    internal static Plugin? Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        PluginConfig.Bind(Config);
        MainThread.Current ??= new MainThread();
        RuntimeState.Reset();
        King.Reset();
        Loadout.Reset();
        King.Actions = King.ReflectionActions.Instance;
        Loadout.Runtime = Loadout.ReflectionRuntime.Instance;
        King.CurrentPolicy = PluginConfig.DefaultNightPolicy;
    }

    private void Update()
    {
        MainThread.Current?.Pump();
    }

    private void OnDestroy()
    {
        if (ReferenceEquals(Instance, this))
            Instance = null;
    }
}
