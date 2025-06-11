# 🧠 LRU Cache with TTL, Eviction & Stats (C#)

This is a thread-safe, generic **Least Recently Used (LRU) cache** in C#.
It supports:

* Per-entry TTL (time-to-live)
* Automatic cleanup of expired items
* Access statistics (hits, misses, evictions)
* Safe concurrent access using `ReaderWriterLockSlim`

---

## 📦 Features

* LRU eviction on capacity overflow
* TTL expiration per entry (optional)
* Background cleanup task
* Thread-safe for concurrent access
* Built-in access stats tracking

---

## ▶️ How to Run

### 1. Clone the Repo or Copy Files

```bash
Just copy these files into a `.NET Console App`:

* `LruCache.cs` (main implementation)
* `TestRunner.cs` (tests with `Main()` method)

### 2. Compile and Run (with .NET CLI)

```bash
dotnet build
dotnet run
```

---

## 📚 Dependencies

* [.NET 6.0 SDK or later](https://dotnet.microsoft.com/en-us/download)

---

## 🧹 Design Decisions

* **Generic Support**: Cache works for any non-nullable key type and any value type.
* **TTL Handling**: TTL is applied per item, either using a default or explicitly passed value.
* **Eviction**: Uses a doubly linked list to track LRU order. On overflow, the tail is evicted.
* **Stats**: Tracks hits, misses, evictions, expired entries, and total requests.
* **Cleanup**: Background task periodically removes expired entries to keep memory clean.

---

## 🔁 Concurrency Model

* Uses `ReaderWriterLockSlim` to handle concurrent reads/writes:

  * Read access is fast and doesn't block other readers.
  * Write access (add/delete/expire) is exclusive.
* All cache operations (`Put`, `Get`, `Delete`, etc.) are thread-safe.
* A background task performs cleanup on an interval (configurable).

---

## ♻️ Eviction Logic

When `capacity` is exceeded:

* The **least recently used** (tail of the linked list) node is evicted.
* Eviction is done during `Put()` if needed.

Additionally, expired items are cleaned up:

* On `Get()`, if an item is expired, it’s removed immediately.
* Periodic background cleanup scans for and removes expired items.

---

## 📊 Sample Output

```
=== LRU Cache Test Suite ===

[TEST] Basic Operations
  db_host: localhost:5432
  api_key: abc123

[TEST] Eviction
  db_host was evicted: Key 'config:db_host' not in cache.

[TEST] Expiration
  Sleeping for 3 seconds...
  temp_data expired: Cache entry for 'temp_data' expired.

[TEST] Concurrent Access
  All threads completed.

[TEST] Final Stats:
  Hits:           112
  Misses:         16
  Total Requests: 128
  Evictions:      201
  Expired:        14
  Current Size:   989

[TEST] Done.
```

---

## 🚀 Performance Considerations

* Suitable for high-read scenarios due to `ReaderWriterLockSlim` optimizations.
* LRU list maintains O(1) access for add/remove/move operations.
* TTL cleanup overhead is minimal due to batching in the background loop.
* Fine-tune `cleanupInterval` based on expected cache load and TTL variance.

---

## 📌 Notes

* TTL is in seconds (`int`), with `-1` meaning "use default TTL".
* Supports immediate disposal via `.Dispose()` to clean up background tasks.

---