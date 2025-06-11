using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public class LruCache<TKey, TValue> : IDisposable where TKey : notnull
{
    private class Node
    {
        public TKey Key;
        public TValue Value;
        public DateTime ExpireAt;
        public Node Prev, Next;
    }

    private readonly Dictionary<TKey, Node> map;
    private readonly int capacity;
    private Node head, tail;

    //TTL
    private readonly TimeSpan defaultTtl;

    // Thread-Safe Access
    private readonly ReaderWriterLockSlim lockSlim = new();
    private readonly CancellationTokenSource cleanupCts = new();
    private readonly Task cleanupTask;

    //For Statistics
    private long hits, misses, requests, evicts, expired;

    public LruCache(int capacity, TimeSpan defaultTtl, TimeSpan cleanupInterval)
    {
        if (capacity <= 0) throw new ArgumentException("Capacity must be > 0", nameof(capacity));

        this.capacity = capacity;
        this.defaultTtl = defaultTtl;
        map = new Dictionary<TKey, Node>(capacity);
        cleanupTask = Task.Run(() => CleanupLoop(cleanupInterval, cleanupCts.Token));
    }

   public void Put(TKey key, TValue value, int ttl = -1)
{
    if (key == null) throw new ArgumentNullException(nameof(key));

    var expire = DateTime.UtcNow + (ttl >= 0 ? TimeSpan.FromSeconds(ttl) : defaultTtl);

    lockSlim.EnterWriteLock();
    try
    {
        if (map.TryGetValue(key, out var node))
        {
            node.Value = value;
            node.ExpireAt = expire;
            MoveToHead(node);
        }
        else
        {
            node = new Node { Key = key, Value = value, ExpireAt = expire };
            AddToHead(node);
            map[key] = node;
            if (map.Count > capacity)
                EvictTail();
        }
    }
    finally { lockSlim.ExitWriteLock(); }
}

    public TValue Get(TKey key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        Interlocked.Increment(ref requests);

        lockSlim.EnterUpgradeableReadLock();
        try
        {
            if (!map.TryGetValue(key, out var node))
            {
                Interlocked.Increment(ref misses);
                throw new KeyNotFoundException($"Key '{key}' not in cache.");
            }

            if (DateTime.UtcNow >= node.ExpireAt)
            {
                Interlocked.Increment(ref misses);
                lockSlim.EnterWriteLock();
                try { RemoveNode(node); }
                finally { lockSlim.ExitWriteLock(); }
                throw new InvalidOperationException($"Cache entry for '{key}' expired.");
            }

            Interlocked.Increment(ref hits);
            lockSlim.EnterWriteLock();
            try { MoveToHead(node); }
            finally { lockSlim.ExitWriteLock(); }

            return node.Value;
        }
        finally { lockSlim.ExitUpgradeableReadLock(); }
    }

    public bool Delete(TKey key)
    {
        if (key == null) throw new ArgumentNullException(nameof(key));
        lockSlim.EnterWriteLock();
        try
        {
            if (map.TryGetValue(key, out var node))
            {
                RemoveNode(node);
                return true;
            }
            return false;
        }
        finally { lockSlim.ExitWriteLock(); }
    }

    public void Clear()
    {
        lockSlim.EnterWriteLock();
        try
        {
            map.Clear();
            head = tail = null;
        }
        finally { lockSlim.ExitWriteLock(); }
    }

    public CacheStats get_stats()
    {
        return new CacheStats
        {
            Hits = hits,
            Misses = misses,
            TotalRequests = requests,
            CurrentSize = map.Count,
            Evictions = evicts,
            ExpiredRemovals = expired
        };
    }

    private void AddToHead(Node node)
    {
        node.Prev = null;
        node.Next = head;
        if (head != null)
            head.Prev = node;
        head = node;
        if (tail == null)
            tail = node;
    }

    private void MoveToHead(Node node)
    {
        if (node == head) return;

        if (node.Prev != null)
            node.Prev.Next = node.Next;
        if (node.Next != null)
            node.Next.Prev = node.Prev;

        if (node == tail)
            tail = node.Prev;

        node.Prev = null;
        node.Next = head;
        if (head != null)
            head.Prev = node;
        head = node;
    }

    private void EvictTail()
    {
        if (tail == null) return;
        var old = tail;
        RemoveNode(old);
        Interlocked.Increment(ref evicts);
    }

    private void RemoveNode(Node node)
    {
        if (node.Prev != null)
            node.Prev.Next = node.Next;
        if (node.Next != null)
            node.Next.Prev = node.Prev;
        if (node == head)
            head = node.Next;
        if (node == tail)
            tail = node.Prev;

        if (map.Remove(node.Key))
            Interlocked.Increment(ref expired);
    }

    private async Task CleanupLoop(TimeSpan interval, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(interval, token);
                List<Node> toRemove = new();

                lockSlim.EnterReadLock();
                try
                {
                    foreach (var n in map.Values)
                        if (DateTime.UtcNow >= n.ExpireAt)
                            toRemove.Add(n);
                }
                finally { lockSlim.ExitReadLock(); }

                if (toRemove.Count > 0)
                {
                    lockSlim.EnterWriteLock();
                    try { toRemove.ForEach(RemoveNode); }
                    finally { lockSlim.ExitWriteLock(); }
                }
            }
        }
        catch (TaskCanceledException) { }
    }

    public void Dispose()
    {
        cleanupCts.Cancel();
        try
        {
            cleanupTask?.Wait();
        }
        catch { }
        lockSlim.Dispose();
        cleanupCts.Dispose();
    }
}
