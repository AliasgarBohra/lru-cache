public struct CacheStats
{
    public long Hits;
    public long Misses;
    public long TotalRequests;
    public int CurrentSize;
    public long Evictions;
    public long ExpiredRemovals;
}