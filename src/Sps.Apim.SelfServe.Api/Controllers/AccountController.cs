using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sps.Apim.SelfServe.Application.AccountManagement;

namespace Sps.Apim.SelfServe.Api.Controllers;

[ApiController]
[Route("api/account")]
public sealed class AccountController(
    IAccountRecoveryService accounts)
    : ControllerBase
{
    [HttpPost("activate")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(
        ActivateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var succeeded =
            await accounts.ActivateAsync(
                request,
                HttpContext.TraceIdentifier,
                cancellationToken);

        if (!succeeded)
        {
            return BadRequest(
                new ProblemDetails
                {
                    Status =
                        StatusCodes.Status400BadRequest,

                    Title =
                        "Account could not be activated."
                });
        }

        return NoContent();
    }

    [HttpPost("password-reset/request")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult>
    RequestPasswordReset(
        RequestPasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        var result =
            await accounts
                .RequestPasswordResetAsync(
                    request,
                    HttpContext.TraceIdentifier,
                    cancellationToken);

        // IMPORTANT:
        // In production return the same generic response
        // regardless of whether the account exists.

        if (HttpContext.RequestServices
            .GetRequiredService<IWebHostEnvironment>()
            .IsDevelopment())
        {
            return Ok(result);
        }

        return NoContent();
    }


    [HttpPost("password-reset/complete")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult>
    CompletePasswordReset(
        CompletePasswordResetRequest request,
        CancellationToken cancellationToken)
    {
        var succeeded =
            await accounts.ResetPasswordAsync(
                request,
                HttpContext.TraceIdentifier,
                cancellationToken);

        if (!succeeded)
        {
            return BadRequest(
                new ProblemDetails
                {
                    Status =
                        StatusCodes.Status400BadRequest,

                    Title =
                        "Password could not be reset."
                });
        }

        return NoContent();
    }
}
