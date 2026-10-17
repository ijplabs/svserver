using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using StackExchange.Redis;
using StardewModdingAPI;

namespace JunimoServer.Services.Redis;

public class RedisService : ModService, IRedisService
{
    private readonly IMonitor _monitor;
    private ConnectionMultiplexer? _redis;
    private IDatabase? _db;
    private ISubscriber? _sub;
    private readonly bool _enabled;

    // Resilient fallback storage when Redis is unreachable
    private readonly ConcurrentDictionary<string, (string Value, DateTime? Expiry)> _memoryFallback = new();
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, string>> _hashFallback = new();

    public bool IsConnected => _redis != null && _redis.IsConnected;

    public RedisService(IMonitor monitor)
        : base(monitor)
    {
        _monitor = monitor;
        _enabled = !string.IsNullOrWhiteSpace(Env.RedisHost);

        if (!_enabled)
        {
            _monitor.Log("[Redis] Redis host not configured. Running in memory-fallback mode.", LogLevel.Info);
            return;
        }

        InitializeRedis();
    }

    private void InitializeRedis()
    {
        try
        {
            var config = new ConfigurationOptions
            {
                EndPoints = { $"{Env.RedisHost}:{Env.RedisPort}" },
                Password = string.IsNullOrEmpty(Env.RedisPassword) ? null : Env.RedisPassword,
                AbortOnConnectFail = false,
                ConnectRetry = 5,
                ConnectTimeout = 4000,
                SyncTimeout = 3000,
                KeepAlive = 60,
                AsyncTimeout = 3000
            };

            _redis = ConnectionMultiplexer.Connect(config);
            _redis.ConnectionFailed += (sender, args) =>
            {
                _monitor.Log($"[Redis] Connection failed: {args.FailureType}. Operating with memory fallback.", LogLevel.Debug);
            };
            _redis.ConnectionRestored += (sender, args) =>
            {
                _monitor.Log("[Redis] Connection restored successfully.", LogLevel.Info);
            };

            _db = _redis.GetDatabase();
            _sub = _redis.GetSubscriber();

            _monitor.Log($"[Redis] Connecting to Redis at {Env.RedisHost}:{Env.RedisPort}...", LogLevel.Info);
        }
        catch (Exception ex)
        {
            _monitor.Log($"[Redis] Initialization warning: {ex.Message}. Falling back to in-memory state.", LogLevel.Warn);
        }
    }

    public async Task<bool> SetStringAsync(string key, string value, TimeSpan? expiry = null)
    {
        try
        {
            if (IsConnected && _db != null)
            {
                return await _db.StringSetAsync(key, value, expiry);
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"[Redis] Error setting string for key '{key}': {ex.Message}", LogLevel.Debug);
        }

        // Memory fallback
        var expireAt = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : (DateTime?)null;
        _memoryFallback[key] = (value, expireAt);
        return true;
    }

    public async Task<string?> GetStringAsync(string key)
    {
        try
        {
            if (IsConnected && _db != null)
            {
                var val = await _db.StringGetAsync(key);
                if (val.HasValue)
                {
                    return val.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"[Redis] Error getting string for key '{key}': {ex.Message}", LogLevel.Debug);
        }

        // Memory fallback
        if (_memoryFallback.TryGetValue(key, out var fallback))
        {
            if (fallback.Expiry.HasValue && DateTime.UtcNow > fallback.Expiry.Value)
            {
                _memoryFallback.TryRemove(key, out _);
                return null;
            }
            return fallback.Value;
        }

        return null;
    }

    public async Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var json = JsonConvert.SerializeObject(value);
        return await SetStringAsync(key, json, expiry);
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var json = await GetStringAsync(key);
        if (string.IsNullOrEmpty(json))
        {
            return default;
        }

        try
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
        catch (Exception ex)
        {
            _monitor.Log($"[Redis] Failed to deserialize '{key}': {ex.Message}", LogLevel.Warn);
            return default;
        }
    }

    public async Task<bool> DeleteAsync(string key)
    {
        var removed = false;
        try
        {
            if (IsConnected && _db != null)
            {
                removed = await _db.KeyDeleteAsync(key);
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"[Redis] Error deleting key '{key}': {ex.Message}", LogLevel.Debug);
        }

        _memoryFallback.TryRemove(key, out _);
        _hashFallback.TryRemove(key, out _);
        return removed;
    }

    public async Task<bool> ExistsAsync(string key)
    {
        try
        {
            if (IsConnected && _db != null)
            {
                return await _db.KeyExistsAsync(key);
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"[Redis] Error checking exists for key '{key}': {ex.Message}", LogLevel.Debug);
        }

        if (_memoryFallback.TryGetValue(key, out var fallback))
        {
            if (fallback.Expiry.HasValue && DateTime.UtcNow > fallback.Expiry.Value)
            {
                _memoryFallback.TryRemove(key, out _);
                return false;
            }
            return true;
        }

        return _hashFallback.ContainsKey(key);
    }

    public async Task<bool> HashSetAsync(string key, string field, string value)
    {
        try
        {
            if (IsConnected && _db != null)
            {
                return await _db.HashSetAsync(key, field, value);
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"[Redis] Error setting hash for key '{key}', field '{field}': {ex.Message}", LogLevel.Debug);
        }

        var dict = _hashFallback.GetOrAdd(key, _ => new ConcurrentDictionary<string, string>());
        dict[field] = value;
        return true;
    }

    public async Task<string?> HashGetAsync(string key, string field)
    {
        try
        {
            if (IsConnected && _db != null)
            {
                var val = await _db.HashGetAsync(key, field);
                if (val.HasValue)
                {
                    return val.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"[Redis] Error getting hash for key '{key}', field '{field}': {ex.Message}", LogLevel.Debug);
        }

        if (_hashFallback.TryGetValue(key, out var dict) && dict.TryGetValue(field, out var valFall))
        {
            return valFall;
        }

        return null;
    }

    public async Task<Dictionary<string, string>> HashGetAllAsync(string key)
    {
        var result = new Dictionary<string, string>();
        try
        {
            if (IsConnected && _db != null)
            {
                var entries = await _db.HashGetAllAsync(key);
                foreach (var entry in entries)
                {
                    if (entry.Name.HasValue && entry.Value.HasValue)
                    {
                        result[entry.Name.ToString()] = entry.Value.ToString();
                    }
                }
                return result;
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"[Redis] Error getting all hashes for key '{key}': {ex.Message}", LogLevel.Debug);
        }

        if (_hashFallback.TryGetValue(key, out var dict))
        {
            return dict.ToDictionary(k => k.Key, v => v.Value);
        }

        return result;
    }

    public async Task<bool> HashDeleteAsync(string key, string field)
    {
        try
        {
            if (IsConnected && _db != null)
            {
                return await _db.HashDeleteAsync(key, field);
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"[Redis] Error deleting hash field '{field}' for key '{key}': {ex.Message}", LogLevel.Debug);
        }

        if (_hashFallback.TryGetValue(key, out var dict))
        {
            return dict.TryRemove(field, out _);
        }

        return false;
    }

    public async Task<long> PublishAsync(string channel, string message)
    {
        try
        {
            if (IsConnected && _sub != null)
            {
                return await _sub.PublishAsync(RedisChannel.Literal(channel), message);
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"[Redis] Publish to '{channel}' failed: {ex.Message}", LogLevel.Debug);
        }

        return 0;
    }

    public void Subscribe(string channel, Action<string, string> handler)
    {
        try
        {
            if (_sub != null)
            {
                _sub.Subscribe(RedisChannel.Literal(channel), (redisChannel, message) =>
                {
                    handler(redisChannel.ToString(), message.ToString());
                });
            }
        }
        catch (Exception ex)
        {
            _monitor.Log($"[Redis] Subscribe to '{channel}' failed: {ex.Message}", LogLevel.Debug);
        }
    }
}
