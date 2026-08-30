using System;
using UnityEngine;

namespace BepInEx
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class BepInPlugin : Attribute
    {
        public BepInPlugin(string GUID, string Name, string Version)
        {
            this.GUID = GUID;
            this.Name = Name;
            this.Version = Version;
        }

        public string GUID { get; }
        public string Name { get; }
        public string Version { get; }
    }

    public abstract class BaseUnityPlugin : MonoBehaviour
    {
        protected Logging.ManualLogSource Logger { get; } = new();
        protected Configuration.ConfigFile Config { get; } = new();
    }
}

namespace BepInEx.Logging
{
    public sealed class ManualLogSource
    {
        public void LogInfo(object data) { }
        public void LogWarning(object data) { }
        public void LogError(object data) { }
        public void LogDebug(object data) { }
        public void LogFatal(object data) { }
        public void LogMessage(object data) { }
    }
}

namespace BepInEx.Configuration
{
    public sealed class ConfigFile
    {
        public ConfigEntry<T> Bind<T>(string section, string key, T defaultValue, string description)
            => new(defaultValue);
    }

    public sealed class ConfigEntry<T>
    {
        public ConfigEntry(T value) => Value = value;

        public T Value { get; set; }
    }
}
