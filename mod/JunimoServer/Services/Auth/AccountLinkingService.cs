using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using JunimoServer.Services.Redis;
using JunimoServer.Services.Roles;
using StardewModdingAPI;
using StardewValley;

namespace JunimoServer.Services.Auth;

public class WebUserOtp
{
    public long FarmerId { get; set; }
    public string FarmerName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public class WebUserProfile
{
    public long FarmerId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string Role { get; set; } = "PLAYER";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? LastLoginUtc { get; set; }
}

public class WebSessionInfo
{
    public string Token { get; set; } = string.Empty;
    public long FarmerId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = "PLAYER";
    public DateTime ExpiresAtUtc { get; set; }
}

public class AccountLinkingService : ModService
{
    private readonly IRedisService _redis;
    private readonly RoleService _roleService;
    private readonly IMonitor _monitor;

    public AccountLinkingService(
        IRedisService redis,
        RoleService roleService,
        IMonitor monitor
    ) : base(monitor)
    {
        _redis = redis;
        _roleService = roleService;
        _monitor = monitor;
    }

    /// <summary>
    /// Generates a 6-digit OTP for the given in-game farmer and caches it with a 10-minute TTL.
    /// </summary>
    public async Task<string> GenerateOtpAsync(long farmerId, string farmerName)
    {
        // Generate secure 6-digit numeric OTP (100000 - 999999)
        int codeNum = RandomNumberGenerator.GetInt32(100000, 1000000);
        string code = codeNum.ToString();

        var otpData = new WebUserOtp
        {
            FarmerId = farmerId,
            FarmerName = farmerName,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _redis.SetAsync($"auth:otp:{code}", otpData, TimeSpan.FromMinutes(10));
        await _redis.SetStringAsync($"auth:otp:farmer:{farmerId}", code, TimeSpan.FromMinutes(10));

        _monitor.Log($"[WebAuth] Generated OTP for farmer '{farmerName}' ({farmerId})", LogLevel.Info);
        return code;
    }

    /// <summary>
    /// Registers or links a web user account using an active in-game OTP code.
    /// </summary>
    public async Task<(bool Success, string Message, WebSessionInfo? Session)> RegisterWithOtpAsync(string code, string password)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(password))
        {
            return (false, "OTP code and password are required.", null);
        }

        if (password.Length < 6)
        {
            return (false, "Password must be at least 6 characters long.", null);
        }

        var cleanCode = code.Trim();
        var otpData = await _redis.GetAsync<WebUserOtp>($"auth:otp:{cleanCode}");
        if (otpData == null)
        {
            return (false, "Invalid or expired registration code. Run !webpass in-game to generate a new code.", null);
        }

        var (hash, salt) = HashPassword(password);
        var role = _roleService.IsPlayerAdmin(otpData.FarmerId) ? "ADMIN" : "PLAYER";

        var profile = new WebUserProfile
        {
            FarmerId = otpData.FarmerId,
            Username = otpData.FarmerName,
            PasswordHash = hash,
            PasswordSalt = salt,
            Role = role,
            CreatedAtUtc = DateTime.UtcNow,
            LastLoginUtc = DateTime.UtcNow
        };

        await _redis.SetAsync($"auth:user:{otpData.FarmerId}", profile);
        await _redis.SetStringAsync($"auth:farmer_name:{otpData.FarmerName.ToLowerInvariant()}", otpData.FarmerId.ToString());

        // Remove used OTP
        await _redis.DeleteAsync($"auth:otp:{cleanCode}");
        await _redis.DeleteAsync($"auth:otp:farmer:{otpData.FarmerId}");

        var session = await CreateSessionAsync(profile.FarmerId, profile.Username, profile.Role);
        _monitor.Log($"[WebAuth] Account registered successfully for farmer '{profile.Username}' ({profile.FarmerId})", LogLevel.Info);

        return (true, "Registration successful.", session);
    }

    /// <summary>
    /// Authenticates a web user (Superadmin via env credentials or Farmer via registered credentials).
    /// </summary>
    public async Task<(bool Success, string Message, WebSessionInfo? Session)> LoginAsync(string usernameOrId, string password)
    {
        if (string.IsNullOrWhiteSpace(usernameOrId) || string.IsNullOrWhiteSpace(password))
        {
            return (false, "Username and password are required.", null);
        }

        // 1. Superadmin authentication check
        if (usernameOrId.Equals(Env.AdminUsername, StringComparison.OrdinalIgnoreCase))
        {
            bool isAdminPassValid = false;
            if (!string.IsNullOrEmpty(Env.AdminPassword) && password == Env.AdminPassword)
            {
                isAdminPassValid = true;
            }
            else if (string.IsNullOrEmpty(Env.AdminPassword))
            {
                if (!string.IsNullOrEmpty(Env.ApiKey) && password == Env.ApiKey)
                {
                    isAdminPassValid = true;
                }
                else if (!string.IsNullOrEmpty(Env.ServerPassword) && password == Env.ServerPassword)
                {
                    isAdminPassValid = true;
                }
            }

            if (isAdminPassValid)
            {
                var adminSession = await CreateSessionAsync(0, Env.AdminUsername, "SUPERADMIN");
                _monitor.Log("[WebAuth] Superadmin authenticated successfully.", LogLevel.Info);
                return (true, "Login successful as Superadmin.", adminSession);
            }
        }

        // 2. Farmer authentication
        long farmerId = 0;
        if (long.TryParse(usernameOrId, out long parsedId))
        {
            farmerId = parsedId;
        }
        else
        {
            var idStr = await _redis.GetStringAsync($"auth:farmer_name:{usernameOrId.ToLowerInvariant()}");
            if (idStr != null && long.TryParse(idStr, out long mappedId))
            {
                farmerId = mappedId;
            }
            else
            {
                // Fallback: check in-game farmers currently loaded
                var found = Game1.getAllFarmers()
                    .FirstOrDefault(f => f.Name.Equals(usernameOrId, StringComparison.OrdinalIgnoreCase));
                if (found != null)
                {
                    farmerId = found.UniqueMultiplayerID;
                }
            }
        }

        if (farmerId == 0)
        {
            return (false, "Player account not found. Please join the farm and run !webpass in-game.", null);
        }

        var profile = await _redis.GetAsync<WebUserProfile>($"auth:user:{farmerId}");
        if (profile == null)
        {
            return (false, "Account not registered for web access. Run !webpass in-game to set up your password.", null);
        }

        if (!VerifyPassword(password, profile.PasswordHash, profile.PasswordSalt))
        {
            return (false, "Invalid credentials.", null);
        }

        // Update login timestamp & role
        profile.LastLoginUtc = DateTime.UtcNow;
        profile.Role = _roleService.IsPlayerAdmin(farmerId) ? "ADMIN" : "PLAYER";
        await _redis.SetAsync($"auth:user:{farmerId}", profile);

        var session = await CreateSessionAsync(farmerId, profile.Username, profile.Role);
        _monitor.Log($"[WebAuth] Farmer '{profile.Username}' ({farmerId}) logged in successfully.", LogLevel.Info);

        return (true, "Login successful.", session);
    }

    /// <summary>
    /// Validates an active web session token against Redis.
    /// </summary>
    public async Task<WebSessionInfo?> ValidateSessionAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var session = await _redis.GetAsync<WebSessionInfo>($"auth:session:{token}");
        if (session == null)
        {
            return null;
        }

        if (DateTime.UtcNow > session.ExpiresAtUtc)
        {
            await _redis.DeleteAsync($"auth:session:{token}");
            return null;
        }

        return session;
    }

    private async Task<WebSessionInfo> CreateSessionAsync(long farmerId, string username, string role)
    {
        byte[] tokenBytes = RandomNumberGenerator.GetBytes(32);
        string token = Convert.ToHexString(tokenBytes).ToLowerInvariant();

        var session = new WebSessionInfo
        {
            Token = token,
            FarmerId = farmerId,
            Username = username,
            Role = role,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(7)
        };

        await _redis.SetAsync($"auth:session:{token}", session, TimeSpan.FromDays(7));
        return session;
    }

    public static (string Hash, string Salt) HashPassword(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations: 10000,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 32
        );
        return (Convert.ToBase64String(hash), Convert.ToBase64String(salt));
    }

    public static bool VerifyPassword(string password, string storedHash, string storedSalt)
    {
        byte[] salt = Convert.FromBase64String(storedSalt);
        byte[] expectedHash = Convert.FromBase64String(storedHash);
        byte[] actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            iterations: 10000,
            hashAlgorithm: HashAlgorithmName.SHA256,
            outputLength: 32
        );
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
