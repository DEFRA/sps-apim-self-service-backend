using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Sps.Apim.SelfServe.Application.Authentication;

namespace Sps.Apim.SelfServe.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthenticationService auth,
    IAntiforgery xsrf)
    : ControllerBase
{
    [HttpGet("csrf")]
    [AllowAnonymous]
    public IActionResult Csrf()
    {
        var tokens =
            xsrf.GetAndStoreTokens(HttpContext);

        return Ok(new
        {
            token = tokens.RequestToken
        });
    }

    [HttpPost("sign-in")]
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignIn(
        SignInRequest request,
        CancellationToken cancellationToken)
    {
        var succeeded =
            await auth.SignInAsync(
                request,
                HttpContext.TraceIdentifier,
                cancellationToken);

        if (!succeeded)
        {
            return Unauthorized(
                new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Invalid sign-in attempt."
                });
        }

        return NoContent();
    }

    [HttpGet("session")]
    [Authorize]
    public async Task<IActionResult> Session(
        CancellationToken cancellationToken)
    {
        var session =
            await auth.GetSessionAsync(
                User,
                cancellationToken);

        if (session is null)
        {
            return Unauthorized();
        }

        return Ok(session);
    }

    [HttpPost("sign-out")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignOut(
        CancellationToken cancellationToken)
    {
        Guid? userId = null;

        var userIdClaim =
            User.FindFirstValue(
                ClaimTypes.NameIdentifier);

        if (Guid.TryParse(
                userIdClaim,
                out var parsedUserId))
        {
            userId = parsedUserId;
        }

        await auth.SignOutAsync(
            userId,
            HttpContext.TraceIdentifier,
            cancellationToken);

        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var succeeded =
            await auth.ChangePasswordAsync(
                User,
                request,
                HttpContext.TraceIdentifier,
                cancellationToken);

        if (!succeeded)
        {
            return BadRequest(
                new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Password could not be changed."
                });
        }

        return NoContent();
    }
}