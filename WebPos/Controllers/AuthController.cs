using Common.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebPos.Core.Abstractions;
using WebPos.Core.Security;
using LoginRequest = Common.Models.LoginRequest;
using UserLoginRequest = WebPos.Core.Abstractions.LoginRequest;

namespace WebPos.Controllers;

public sealed class ManagerPinRequest
{
    public required string Pin { get; init; }
}

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
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

    [HttpPost("verify-manager-pin")]
    [EnableRateLimiting("pin-login")]
    [ProducesResponseType(typeof(ManagerPinVerifiedDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<ManagerPinVerifiedDto>> VerifyManagerPin(
        [FromBody] ManagerPinRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        bool ok = await _loginService.VerifyManagerPinAsync(request.Pin, cancellationToken);
        if (!ok)
        {
            return Problem(
                detail: "Invalid manager PIN.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return Ok(new ManagerPinVerifiedDto { Verified = true });
    }
}
