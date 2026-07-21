using Common.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebPos.Core.Abstractions;
using WebPos.Core.Security;
using LoginRequest = Common.Models.LoginRequest;
using UserLoginRequest = WebPos.Core.Abstractions.LoginRequest;

namespace WebPos.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    LoginService loginService,
    ISecurityService securityService) : ControllerBase
{
    private readonly LoginService _loginService =
        loginService ?? throw new ArgumentNullException(nameof(loginService));

    private readonly ISecurityService _securityService =
        securityService ?? throw new ArgumentNullException(nameof(securityService));

    [HttpPost("login")]
    [EnableRateLimiting("pin-login")]
    [ProducesResponseType(typeof(CashierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<CashierDto>> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        CashierDto? cashier = await _loginService.LoginAsync(request.Pin, cancellationToken);
        if (cashier is null)
        {
            return Problem(
                detail: "Invalid cashier PIN.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return Ok(cashier);
    }

    [HttpPost("token")]
    [EnableRateLimiting("pin-login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<AuthResponse>> Token(
        [FromBody] UserLoginRequest request,
        CancellationToken cancellationToken)
    {
        AuthResponse? response =
            await _securityService.AuthenticateAsync(request, cancellationToken);
        if (response is null)
        {
            return Problem(
                detail: "Invalid username or password.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return Ok(response);
    }
}
