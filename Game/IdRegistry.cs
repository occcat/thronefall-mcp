using System.Collections.Generic;
using ThronefallControl.Dto;

namespace ThronefallControl.Game;

public sealed class IdRegistry
{
    readonly Dictionary<int, Entry> _entries = new();

    public int SceneGeneration { get; private set; }

    public void BeginScene()
    {
        SceneGeneration++;
        _entries.Clear();
    }

    public EntityId Register(int instanceId, string kind, string name, object? target)
    {
        var id = new EntityId
        {
            InstanceId = instanceId,
            Generation = SceneGeneration,
            Kind = kind,
            Name = name
        };
        _entries[instanceId] = new Entry(id, target);
        return id;
    }

    public bool TryResolve(EntityId id, out object? target, out string? error) =>
        TryResolve(id.InstanceId, id.Generation, out target, out error);

    public bool TryResolve(int instanceId, int generation, out object? target, out string? error)
    {
        target = null;
        error = null;
        if (generation != SceneGeneration)
        {
            error = ErrorCodes.StaleId;
            return false;
        }

        if (!_entries.TryGetValue(instanceId, out var entry))
        {
            error = ErrorCodes.NotFound;
            return false;
        }

        target = entry.Target;
        return true;
    }

    public bool TryGet(int instanceId, out EntityId id)
    {
        if (_entries.TryGetValue(instanceId, out var entry))
        {
            id = entry.Id;
            return true;
        }

        id = default!;
        return false;
    }

    public bool TryResolve<T>(int instanceId, int generation, out T? target, out string? error)
        where T : class
    {
        if (!TryResolve(instanceId, generation, out var obj, out error))
        {
            target = null;
            return false;
        }

        target = obj as T;
        if (target == null && obj != null)
        {
            error = ErrorCodes.NotFound;
            return false;
        }

        return true;
    }

    readonly struct Entry
    {
        public Entry(EntityId id, object? target)
        {
            Id = id;
            Target = target;
        }

        public EntityId Id { get; }
        public object? Target { get; }
    }
}
