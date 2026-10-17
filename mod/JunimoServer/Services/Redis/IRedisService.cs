using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JunimoServer.Services.Redis;

public interface IRedisService : IModService
{
    bool IsConnected { get; }

    Task<bool> SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task<T?> GetAsync<T>(string key);
    Task<bool> DeleteAsync(string key);
    Task<bool> ExistsAsync(string key);

    Task<bool> SetStringAsync(string key, string value, TimeSpan? expiry = null);
    Task<string?> GetStringAsync(string key);

    Task<bool> HashSetAsync(string key, string field, string value);
    Task<string?> HashGetAsync(string key, string field);
    Task<Dictionary<string, string>> HashGetAllAsync(string key);
    Task<bool> HashDeleteAsync(string key, string field);

    Task<long> PublishAsync(string channel, string message);
    void Subscribe(string channel, Action<string, string> handler);
}
