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
        MainThread.Current ??= new MainThread();
        ReflectionCache.TryInit(Logger);
        IdempotencyCache.Current ??= new IdempotencyCache();
        Slots.Backend ??= new LiveSlotBackend();
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
