using System;
using System.Threading;
using System.Threading.Tasks;

public static class TestLRU
{
    public static void Main()
    {
        Console.WriteLine("LRU Cache Test\n");

        var cache = new LruCache<string, string>(
            capacity: 1000,
            defaultTtl: TimeSpan.FromSeconds(120),
            cleanupInterval: TimeSpan.FromSeconds(1)
        );

        TestBasicOperations(cache);
        TestEviction(cache);
        TestExpiration(cache);
        TestConcurrentAccess(cache);

        Console.WriteLine("\n[TEST] Final Stats:");
        var stats = cache.get_stats();
        Console.WriteLine($"  Hits:           {stats.Hits}");
        Console.WriteLine($"  Misses:         {stats.Misses}");
        Console.WriteLine($"  Total Requests: {stats.TotalRequests}");
        Console.WriteLine($"  Evictions:      {stats.Evictions}");
        Console.WriteLine($"  Expired:        {stats.ExpiredRemovals}");
        Console.WriteLine($"  Current Size:   {stats.CurrentSize}");

        Console.WriteLine("\n[TEST] Done.");
        cache.Dispose();
    }

    private static void TestBasicOperations(LruCache<string, string> cache)
    {
        Console.WriteLine("[TEST] Basic Operations");

        cache.Put("config:db_host", "localhost:5432");
        cache.Put("config:api_key", "abc123", 60);

        Console.WriteLine("  db_host: " + cache.Get("config:db_host"));
        Console.WriteLine("  api_key: " + cache.Get("config:api_key"));
    }

    private static void TestEviction(LruCache<string, string> cache)
    {
        Console.WriteLine("\n[TEST] Eviction");

        for (int i = 0; i < 1200; i++)
        {
            cache.Put($"data:{i}", $"value_{i}");
        }

        try
        {
            var val = cache.Get("config:db_host");
            Console.WriteLine("  db_host still in cache: " + val);
        }
        catch (Exception ex)
        {
            Console.WriteLine("  db_host was evicted: " + ex.Message);
        }
    }

    private static void TestExpiration(LruCache<string, string> cache)
    {
        Console.WriteLine("\n[TEST] Expiration");

        cache.Put("temp_data", "expires_soon", 2);
        Thread.Sleep(3000);

        try
        {
            var val = cache.Get("temp_data");
            Console.WriteLine("  temp_data: " + val);
        }
        catch (Exception ex)
        {
            Console.WriteLine("  temp_data expired: " + ex.Message);
        }
    }

    private static void TestConcurrentAccess(LruCache<string, string> cache)
    {
        Console.WriteLine("\n[TEST] Concurrent Access");

        int threadCount = 4;
        Task[] tasks = new Task[threadCount];

        for (int t = 0; t < threadCount; t++)
        {
            int threadId = t;
            tasks[t] = Task.Run(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    string key = $"thread_{threadId}:item_{i}";
                    cache.Put(key, $"data_{i}");
                    try
                    {
                        cache.Get($"thread_{threadId}:item_{i / 2}");
                    }
                    catch { }
                }
            });
        }

        Task.WaitAll(tasks);
        Console.WriteLine("  All threads completed.");
    }
}
