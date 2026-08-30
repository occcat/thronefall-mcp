using System;
using BepInEx;
using ThronefallControl.Config;
using ThronefallControl.Game;
using ThronefallControl.Http;
using ThronefallControl.Http.Modules;
using UnityEngine;

namespace ThronefallControl;

[BepInPlugin(PluginInfo.Id, PluginInfo.Name, PluginInfo.Version)]
public sealed class Plugin : BaseUnityPlugin
{
    internal static Plugin? Instance { get; private set; }

    Server? _server;
    MainThread? _mainThread;

    private void Awake()
    {
        Instance = this;
        try
        {
            PluginConfig.Bind(Config);
            ReflectionCache.TryInit(Logger);
            var facade = GameFacade.Current ??= new GameFacade();
            Units.Current = facade.Units;
            _mainThread = MainThread.Current ?? new MainThread();
            MainThread.Current = _mainThread;
            HealthModule.FrameCountReader = ReadFrameCount;
            IdempotencyCache.Current ??= new IdempotencyCache();
            Slots.Backend ??= new LiveSlotBackend();

            _server = new Server(
                logInfo: msg => Logger.LogInfo(msg),
                logError: msg => Logger.LogError(msg));
            _server.Start();
            if (!_server.IsListening)
                Logger.LogError("HTTP API disabled after bind failure; game continues.");
        }
        catch (Exception ex)
        {
            Logger.LogError($"plugin Awake failed; game continues: {ex}");
        }
    }

    private void Update()
    {
        try
        {
            GameFacade.Current?.Tick();
            _mainThread?.Pump();
        }
        catch (Exception ex)
        {
            Logger.LogError($"MainThread.Pump: {ex}");
        }
    }

    private void OnDestroy()
    {
        try
        {
            _server?.Stop();
        }
        catch (Exception ex)
        {
            Logger.LogError($"HTTP stop failed: {ex}");
        }

        _server = null;
        HealthModule.FrameCountReader = null;
        if (ReferenceEquals(MainThread.Current, _mainThread))
            MainThread.Current = null;
        if (ReferenceEquals(Instance, this))
            Instance = null;
    }

    static int ReadFrameCount() => Time.frameCount;
}
