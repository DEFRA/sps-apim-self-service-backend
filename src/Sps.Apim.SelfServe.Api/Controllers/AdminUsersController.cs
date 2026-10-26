using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sps.Apim.SelfServe.Application.AccountManagement;

namespace Sps.Apim.SelfServe.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = "Administrator")]
public sealed class AdminUsersController(
    IAccountAdministrationService accounts)
    : ControllerBase
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await accounts.CreateUserAsync(
                    User,
                    request,
                    HttpContext.TraceIdentifier,
                    cancellationToken);

            return Created(
                $"/api/admin/users/{result.Id}",
                result);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(
                new ProblemDetails
                {
                    Status =
                        StatusCodes.Status400BadRequest,

                    Title =
                        "User account could not be created.",

                    Detail =
                        exception.Message
                });
        }
    }

    [HttpGet]
    public async Task<
    IReadOnlyCollection<UserSummaryResponse>>
    GetUsers(
        CancellationToken cancellationToken)
    {
        return await accounts
            .GetUsersAsync(
                cancellationToken);
    }

    [HttpPost("{userId:guid}/activation/reissue")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReissueActivation(
    Guid userId,
    ReissueActivationRequest request,
    CancellationToken cancellationToken)
    {
        try
        {
            var result =
                await accounts
                    .ReissueActivationAsync(
                        User,
                        userId,
                        request,
                        HttpContext.TraceIdentifier,
                        cancellationToken);

            return Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(
                new ProblemDetails
                {
                    Status =
                        StatusCodes.Status400BadRequest,

                    Title =
                        "Activation could not be reissued.",

                    Detail =
                        exception.Message
                });
        }
    }
}