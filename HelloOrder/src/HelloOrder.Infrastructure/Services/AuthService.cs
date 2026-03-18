using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using HelloOrder.Application.Services;
using Microsoft.Extensions.Configuration;
using HelloOrder.Core.Entities;
using HelloOrder.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace HelloOrder.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext db, IConfiguration config)
    {
        _db = db;
        _config = config;
    }

    public async Task<LoginResult?> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var user = await _db.SysUsers
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.Status == 1, ct);
        if (user == null) return null;

        // 简单 BCrypt 或 SHA256 校验；实际建议用 BCrypt.Net
        var hash = HashPassword(request.Password);
        if (user.PasswordHash != hash) return null;

        var permissions = await GetUserPermissionsAsync(user.RoleId, ct);
        var token = GenerateJwt(user, permissions);
        return new LoginResult
        {
            Token = token,
            UserId = user.Id,
            Username = user.Username,
            RealName = user.RealName,
            RoleCode = user.Role?.Code,
            Permissions = permissions
        };
    }

    public async Task<ProfileResult?> GetProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.SysUsers.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId && u.Status == 1, ct);
        if (user == null) return null;
        return new ProfileResult
        {
            UserId = user.Id,
            Username = user.Username,
            RealName = user.RealName,
            RoleCode = user.Role?.Code,
            RoleName = user.Role?.Name
        };
    }

    private async Task<List<string>> GetUserPermissionsAsync(Guid? roleId, CancellationToken ct)
    {
        if (roleId == null) return new List<string>();
        var list = await _db.SysRolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Join(_db.SysPermissions, rp => rp.PermissionId, p => p.Id, (rp, p) => p.Code)
            .ToListAsync(ct);
        return list;
    }

    private string GenerateJwt(SysUser user, List<string> permissions)
    {
        var key = _config["Jwt:Key"] ?? "HelloOrder-SecretKey-32Chars!!";
        var issuer = _config["Jwt:Issuer"] ?? "HelloOrder";
        var audience = _config["Jwt:Audience"] ?? "HelloOrder";
        var expiryMinutes = int.TryParse(_config["Jwt:ExpiryMinutes"], out var m) ? m : 120;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("real_name", user.RealName),
            new("role_id", user.RoleId?.ToString() ?? ""),
            new("role_code", user.Role?.Code ?? "")
        };
        foreach (var p in permissions)
            claims.Add(new Claim("permission", p));

        var keyBytes = Encoding.UTF8.GetBytes(key.PadRight(32).Substring(0, 32));
        var creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            DateTime.UtcNow,
            DateTime.UtcNow.AddMinutes(expiryMinutes),
            creds
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashPassword(string password)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(password);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    public static string HashPasswordForCreate(string password) => HashPassword(password);
}
