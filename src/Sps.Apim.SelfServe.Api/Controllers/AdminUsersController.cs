using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sps.Apim.SelfServe.Application.AccountManagement;
using Sps.Apim.SelfServe.Domain.Constants;

namespace Sps.Apim.SelfServe.Api.Controllers;

[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.IccAdministrator)]
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

    [HttpGet("projects")]
    public async Task<IReadOnlyCollection<ProjectResponse>> GetProjects(
        CancellationToken cancellationToken)
    {
        return await accounts.GetProjectsAsync(cancellationToken);
    }

    [HttpGet("{userId:guid}/access")]
    public async Task<IActionResult> GetUserAccess(
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await accounts.GetUserAccessAsync(userId, cancellationToken));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "User account was not found.",
                Detail = exception.Message
            });
        }
    }

    [HttpPut("{userId:guid}/access")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateUserAccess(
        Guid userId,
        UpdateUserAccessRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await accounts.UpdateUserAccessAsync(
                User,
                userId,
                request,
                HttpContext.TraceIdentifier,
                cancellationToken);

            return NoContent();
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "User account was not found.",
                Detail = exception.Message
            });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "User access could not be updated.",
                Detail = exception.Message
            });
        }
    }

    [HttpPost("{userId:guid}/unlock")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlock(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await accounts.UnlockAsync(
            User,
            userId,
            HttpContext.TraceIdentifier,
            cancellationToken);

        return NoContent();
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

    [HttpPost("{userId:guid}/disable")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(
        Guid userId,
        AccountStatusChangeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await accounts.DisableAsync(
                User,
                userId,
                request.Reason,
                HttpContext.TraceIdentifier,
                cancellationToken);

            return NoContent();
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(
                new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "User account was not found.",
                    Detail = exception.Message
                });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(
                new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Account could not be disabled.",
                    Detail = exception.Message
                });
        }
    }

    [HttpPost("{userId:guid}/enable")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enable(
        Guid userId,
        AccountStatusChangeRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            await accounts.EnableAsync(
                User,
                userId,
                request.Reason,
                HttpContext.TraceIdentifier,
                cancellationToken);

            return NoContent();
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(
                new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "User account was not found.",
                    Detail = exception.Message
                });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(
                new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Account could not be enabled.",
                    Detail = exception.Message
                });
        }
    }
}