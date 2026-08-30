using BepInEx;
using ThronefallControl.Config;
using ThronefallControl.Game;

namespace ThronefallControl;

[BepInPlugin(PluginInfo.Id, PluginInfo.Name, PluginInfo.Version)]
public sealed class Plugin : BaseUnityPlugin
{
    internal static Plugin? Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        PluginConfig.Bind(Config);
        ReflectionCache.TryInit(Logger);
        MainThread.Current ??= new MainThread();
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
