using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sps.Apim.SelfServe.Application.AccountManagement;
using Sps.Apim.SelfServe.Domain.Constants;
using Microsoft.EntityFrameworkCore;

namespace Sps.Apim.SelfServe.Api.Controllers;

[ApiController]
[Route("api/admin/access-requests")]
[Authorize(Roles = RoleNames.Administrator + "," + RoleNames.IccAdministrator)]
public sealed class AdminAccessRequestsController(
    IAccessRequestService accessRequests)
    : ControllerBase
{
    [HttpGet]
    public async Task<IReadOnlyCollection<AccessRequestResponse>> GetAll(
        CancellationToken cancellationToken)
    {
        return await accessRequests.GetAllAsync(cancellationToken);
    }

    [HttpGet("{requestId:guid}")]
    public async Task<IActionResult> Get(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var request = await accessRequests.GetAsync(requestId, cancellationToken);
        return request is null ? NotFound() : Ok(request);
    }

    [HttpPost("{requestId:guid}/decision")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordDecision(
        Guid requestId,
        AccessRequestDecisionRequest decision,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await accessRequests.RecordDecisionAsync(
                User,
                requestId,
                decision,
                HttpContext.TraceIdentifier,
                cancellationToken);

            return Ok(result);
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Access request was not found.",
                Detail = exception.Message
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Access request has already been updated.",
                Detail = "Reload the request to see its current status."
            });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Access request decision could not be recorded.",
                Detail = exception.Message
            });
        }
    }

    [HttpPost("{requestId:guid}/decision-email")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendDecisionEmail(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        try
        {
            await accessRequests.SendDecisionEmailAsync(
                requestId,
                cancellationToken);

            return NoContent();
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Access request was not found.",
                Detail = exception.Message
            });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Decision email could not be sent.",
                Detail = exception.Message
            });
        }
    }
}
