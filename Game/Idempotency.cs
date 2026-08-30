using System.Collections.Generic;

namespace ThronefallControl.Game;

public sealed class IdempotencyCache
{
    public const int Capacity = 256;

    public static IdempotencyCache? Current { get; set; }

    readonly object _gate = new();
    readonly Dictionary<string, Entry> _map = new();
    readonly Queue<string> _order = new();

    public bool TryGet(string? clientRequestId, out int status, out string body)
    {
        status = 0;
        body = "";
        if (string.IsNullOrEmpty(clientRequestId))
            return false;

        lock (_gate)
        {
            if (!_map.TryGetValue(clientRequestId, out var entry))
                return false;
            status = entry.Status;
            body = entry.Body;
            return true;
        }
    }

    public void Put(string? clientRequestId, int status, string body)
    {
        if (string.IsNullOrEmpty(clientRequestId))
            return;

        lock (_gate)
        {
            if (_map.ContainsKey(clientRequestId))
            {
                _map[clientRequestId] = new Entry(status, body);
                return;
            }

            while (_order.Count >= Capacity)
            {
                var oldest = _order.Dequeue();
                _map.Remove(oldest);
            }

            _order.Enqueue(clientRequestId);
            _map[clientRequestId] = new Entry(status, body);
        }
    }

    readonly struct Entry
    {
        public Entry(int status, string body)
        {
            Status = status;
            Body = body ?? "";
        }

        public int Status { get; }
        public string Body { get; }
    }
}
