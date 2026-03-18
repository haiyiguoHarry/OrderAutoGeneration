using System.Security.Claims;
using HelloOrder.Api.Common;
using HelloOrder.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HelloOrder.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResult<LoginResult>>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        if (result == null)
            return Ok(ApiResult<LoginResult>.Fail("用户名或密码错误"));
        return Ok(ApiResult<LoginResult>.Ok(result));
    }

    [HttpGet("profile")]
    [Authorize]
    public async Task<ActionResult<ApiResult<ProfileResult>>> Profile(CancellationToken ct)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized();
        var result = await _authService.GetProfileAsync(userId, ct);
        if (result == null)
            return Ok(ApiResult<ProfileResult>.Fail("用户不存在"));
        return Ok(ApiResult<ProfileResult>.Ok(result));
    }
}

