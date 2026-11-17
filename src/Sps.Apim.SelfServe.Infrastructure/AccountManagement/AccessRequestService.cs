using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Sps.Apim.SelfServe.Application.AccountManagement;
using Sps.Apim.SelfServe.Application.Notifications;
using Sps.Apim.SelfServe.Domain.Constants;
using Sps.Apim.SelfServe.Domain.Entities;
using Sps.Apim.SelfServe.Infrastructure.Notifications;
using Sps.Apim.SelfServe.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace Sps.Apim.SelfServe.Infrastructure.AccountManagement;

public sealed class AccessRequestService(
    ApplicationDbContext dbContext,
    INotificationService notifications,
    IAccountAdministrationService accountAdministration,
    IOptions<GovNotifyOptions> notifyOptions)
    : IAccessRequestService
{
    public async Task SubmitAsync(
        AccessRequestRequest request,
        CancellationToken cancellationToken)
    {
        var accessRequest = new AccessRequest
        {
            Id = Guid.NewGuid(),
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email.Trim(),
            RequestType = request.RequestType,
            Reason = request.Reason.Trim(),
            SubmittedAtUtc = DateTimeOffset.UtcNow
        };

        dbContext.AccessRequests.Add(accessRequest);
        await dbContext.SaveChangesAsync(cancellationToken);

        var normalizedRequest = new AccessRequestRequest
        {
            FirstName = accessRequest.FirstName,
            LastName = accessRequest.LastName,
            Email = accessRequest.Email,
            RequestType = accessRequest.RequestType,
            Reason = accessRequest.Reason
        };

        await notifications.SendAccessRequestEmailAsync(
            normalizedRequest,
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<AccessRequestResponse>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        return await RequestResponses()
            .OrderByDescending(request => request.SubmittedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<AccessRequestResponse?> GetAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        return await RequestResponses()
            .Where(request => request.Id == requestId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<int> GetCountAsync(
        CancellationToken cancellationToken)
    {
        return dbContext.AccessRequests.CountAsync(
            request => request.Status == AccessRequestStatuses.AwaitingReview,
            cancellationToken);
    }

    public async Task<AccessRequestResponse> RecordDecisionAsync(
        ClaimsPrincipal administrator,
        Guid requestId,
        AccessRequestDecisionRequest decision,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(decision.DecisionNote))
        {
            throw new InvalidOperationException("Enter a decision note.");
        }

        if (!new[]
            {
                AccessRequestStatuses.Approved,
                AccessRequestStatuses.Rejected,
                AccessRequestStatuses.MoreInformationRequested
            }.Contains(decision.Outcome, StringComparer.Ordinal))
        {
            throw new InvalidOperationException("Select a valid request outcome.");
        }

        if (string.IsNullOrWhiteSpace(notifyOptions.Value.AccessDecisionTemplateId))
        {
            throw new InvalidOperationException(
                "The GOV.UK Notify access decision template is not configured.");
        }

        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var accessRequest = await dbContext.AccessRequests
            .SingleOrDefaultAsync(request => request.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("Access request was not found.");

        if (accessRequest.Status != AccessRequestStatuses.AwaitingReview)
        {
            throw new InvalidOperationException(
                "This access request has already been decided.");
        }

        Guid? accountUserId = null;
        if (decision.Outcome == AccessRequestStatuses.Approved)
        {
            if (string.IsNullOrWhiteSpace(decision.Role))
            {
                throw new InvalidOperationException(
                    "Select a portal role before approving this request.");
            }

            var user = await accountAdministration.CreateUserAsync(
                administrator,
                new CreateUserRequest
                {
                    Email = accessRequest.Email,
                    DisplayName = $"{accessRequest.FirstName} {accessRequest.LastName}",
                    Role = decision.Role,
                    ProjectIds = decision.ProjectId is Guid projectId
                        ? [projectId]
                        : [],
                    Reason = decision.DecisionNote.Trim(),
                    ApprovalReference = $"AR-{accessRequest.Id:N}"[..11]
                },
                correlationId,
                cancellationToken);

            accountUserId = user.Id;
        }

        accessRequest.Status = decision.Outcome;
        accessRequest.DecisionNote = decision.DecisionNote.Trim();
        accessRequest.AssignedRole =
            decision.Outcome == AccessRequestStatuses.Approved
                ? decision.Role
                : null;
        accessRequest.ProjectId =
            decision.Outcome == AccessRequestStatuses.Approved
                ? decision.ProjectId
                : null;
        accessRequest.AccountUserId = accountUserId;
        accessRequest.DecidedByUserId = GetAdministratorId(administrator);
        accessRequest.DecidedAtUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await GetAsync(requestId, cancellationToken)
            ?? throw new InvalidOperationException(
                "The access request was recorded but could not be reloaded.");
    }

    public async Task SendDecisionEmailAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var accessRequest = await dbContext.AccessRequests
            .SingleOrDefaultAsync(request => request.Id == requestId, cancellationToken)
            ?? throw new KeyNotFoundException("Access request was not found.");

        if (accessRequest.Status == AccessRequestStatuses.AwaitingReview)
        {
            throw new InvalidOperationException(
                "Record a decision before sending a decision email.");
        }

        if (accessRequest.DecisionEmailSentAtUtc is not null)
        {
            return;
        }

        var response = await RequestResponses()
            .SingleAsync(request => request.Id == requestId, cancellationToken);

        await notifications.SendAccessDecisionEmailAsync(
            response,
            cancellationToken);

        accessRequest.DecisionEmailSentAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<AccessRequestResponse> RequestResponses()
    {
        return dbContext.AccessRequests.Select(request => new AccessRequestResponse
        {
            Id = request.Id,
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            RequestType = request.RequestType,
            Reason = request.Reason,
            SubmittedAtUtc = request.SubmittedAtUtc,
            Status = request.Status,
            DecisionNote = request.DecisionNote,
            AssignedRole = request.AssignedRole,
            ProjectName = dbContext.Projects
                .Where(project => project.Id == request.ProjectId)
                .Select(project => project.Name)
                .FirstOrDefault(),
            AccountUserId = request.AccountUserId,
            DecidedAtUtc = request.DecidedAtUtc,
            DecisionEmailSentAtUtc = request.DecisionEmailSentAtUtc
        });
    }

    private static Guid? GetAdministratorId(ClaimsPrincipal administrator)
    {
        var userId = administrator.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userId, out var parsedUserId)
            ? parsedUserId
            : null;
    }
}
