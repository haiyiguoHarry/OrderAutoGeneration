namespace HelloOrder.Application.Services;

public class LoginRequest
{
    public string Username { get; set; } = null!;
    public string Password { get; set; } = null!;
}

public class LoginResult
{
    public string Token { get; set; } = null!;
    public Guid UserId { get; set; }
    public string Username { get; set; } = null!;
    public string RealName { get; set; } = null!;
    public string? RoleCode { get; set; }
    public List<string> Permissions { get; set; } = new();
}

public class ProfileResult
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = null!;
    public string RealName { get; set; } = null!;
    public string? RoleCode { get; set; }
    public string? RoleName { get; set; }
}

public interface IAuthService
{
    Task<LoginResult?> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<ProfileResult?> GetProfileAsync(Guid userId, CancellationToken ct = default);
}
