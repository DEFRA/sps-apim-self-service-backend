using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Sps.Apim.SelfServe.Api.Controllers;
using Sps.Apim.SelfServe.Application.AccountManagement;
using Xunit;

namespace Sps.Apim.SelfServe.UnitTests;

public sealed class AdminUsersControllerTests
{
    [Fact]
    public async Task Create_returns_created_location_and_response()
    {
        var expected = new CreateUserResponse
        {
            Id = Guid.NewGuid(),
            Email = "user@example.com",
            DisplayName = "Test User",
            Status = "PendingActivation"
        };
        var service = new FakeAccountAdministrationService { CreateUserResponse = expected };
        var controller = CreateController(service);
        var request = new CreateUserRequest { Email = expected.Email };

        var result = Assert.IsType<CreatedResult>(
            await controller.Create(request, CancellationToken.None));

        Assert.Equal($"/api/admin/users/{expected.Id}", result.Location);
        Assert.Same(expected, result.Value);
        Assert.Same(request, service.CreateUserRequest);
        Assert.Equal("test-trace", service.CreateUserCorrelationId);
    }

    [Fact]
    public async Task Create_returns_bad_request_when_service_rejects_request()
    {
        var service = new FakeAccountAdministrationService
        {
            CreateUserException = new InvalidOperationException("duplicate email")
        };
        var controller = CreateController(service);

        var result = Assert.IsType<BadRequestObjectResult>(
            await controller.Create(new CreateUserRequest(), CancellationToken.None));
        var problem = Assert.IsType<ProblemDetails>(result.Value);

        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("User account could not be created.", problem.Title);
        Assert.Equal("duplicate email", problem.Detail);
    }

    [Fact]
    public async Task GetUsers_returns_users_from_service()
    {
        IReadOnlyCollection<UserSummaryResponse> expected =
        [
            new UserSummaryResponse { Email = "user@example.com" }
        ];
        var service = new FakeAccountAdministrationService { Users = expected };
        var controller = CreateController(service);

        var result = await controller.GetUsers(CancellationToken.None);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetProjects_returns_projects_from_service()
    {
        IReadOnlyCollection<ProjectResponse> expected =
        [
            new ProjectResponse { Name = "Water" }
        ];
        var service = new FakeAccountAdministrationService { Projects = expected };
        var controller = CreateController(service);

        var result = await controller.GetProjects(CancellationToken.None);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetUserAccess_returns_access_from_service()
    {
        var expected = new UserAccessResponse { Email = "user@example.com" };
        var service = new FakeAccountAdministrationService { UserAccess = expected };
        var controller = CreateController(service);

        var result = Assert.IsType<OkObjectResult>(
            await controller.GetUserAccess(Guid.NewGuid(), CancellationToken.None));

        Assert.Same(expected, result.Value);
    }

    [Fact]
    public async Task UpdateUserAccess_passes_request_and_correlation_id()
    {
        var service = new FakeAccountAdministrationService();
        var controller = CreateController(service);
        var userId = Guid.NewGuid();
        var request = new UpdateUserAccessRequest
        {
            Role = "API Developer",
            Status = "Active",
            Reason = "Approved access"
        };

        var result = Assert.IsType<NoContentResult>(
            await controller.UpdateUserAccess(userId, request, CancellationToken.None));

        Assert.Equal(userId, service.UpdatedAccessUserId);
        Assert.Same(request, service.UpdatedAccessRequest);
        Assert.Equal("test-trace", service.UpdatedAccessCorrelationId);
    }

    [Fact]
    public async Task ReissueActivation_returns_response_from_service()
    {
        var expected = new ReissueActivationResponse
        {
            UserId = Guid.NewGuid(),
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
        };
        var service = new FakeAccountAdministrationService { ReissueActivationResponse = expected };
        var controller = CreateController(service);
        var userId = Guid.NewGuid();
        var request = new ReissueActivationRequest { Reason = "Requested by user" };

        var result = Assert.IsType<OkObjectResult>(
            await controller.ReissueActivation(userId, request, CancellationToken.None));

        Assert.Same(expected, result.Value);
        Assert.DoesNotContain(
            "activationUrl",
            JsonSerializer.Serialize(result.Value),
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(userId, service.ReissuedUserId);
        Assert.Same(request, service.ReissueActivationRequest);
        Assert.Equal("test-trace", service.ReissueCorrelationId);
    }

    [Fact]
    public async Task ReissueActivation_returns_bad_request_when_service_rejects_request()
    {
        var service = new FakeAccountAdministrationService
        {
            ReissueActivationException = new InvalidOperationException("account is disabled")
        };
        var controller = CreateController(service);

        var result = Assert.IsType<BadRequestObjectResult>(
            await controller.ReissueActivation(Guid.NewGuid(), new ReissueActivationRequest(), CancellationToken.None));
        var problem = Assert.IsType<ProblemDetails>(result.Value);

        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal("Activation could not be reissued.", problem.Title);
        Assert.Equal("account is disabled", problem.Detail);
    }

    [Fact]
    public async Task Disable_returns_no_content_and_passes_reason_and_correlation_id()
    {
        var service = new FakeAccountAdministrationService();
        var controller = CreateController(service);
        var userId = Guid.NewGuid();
        var request = new AccountStatusChangeRequest
        {
            Reason = "Access removed"
        };

        var result = Assert.IsType<NoContentResult>(
            await controller.Disable(userId, request, CancellationToken.None));

        Assert.Equal(userId, service.DisabledUserId);
        Assert.Equal(request.Reason, service.DisableReason);
        Assert.Equal("test-trace", service.DisableCorrelationId);
    }

    [Fact]
    public async Task Enable_returns_no_content_and_passes_reason_and_correlation_id()
    {
        var service = new FakeAccountAdministrationService();
        var controller = CreateController(service);
        var userId = Guid.NewGuid();
        var request = new AccountStatusChangeRequest
        {
            Reason = "Access restored"
        };

        var result = Assert.IsType<NoContentResult>(
            await controller.Enable(userId, request, CancellationToken.None));

        Assert.Equal(userId, service.EnabledUserId);
        Assert.Equal(request.Reason, service.EnableReason);
        Assert.Equal("test-trace", service.EnableCorrelationId);
    }

    [Fact]
    public async Task Enable_returns_not_found_when_account_does_not_exist()
    {
        var service = new FakeAccountAdministrationService
        {
            EnableException = new KeyNotFoundException("User account was not found.")
        };
        var controller = CreateController(service);

        var result = Assert.IsType<NotFoundObjectResult>(
            await controller.Enable(
                Guid.NewGuid(),
                new AccountStatusChangeRequest { Reason = "Access restored" },
                CancellationToken.None));

        Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ProblemDetails>(result.Value).Status);
    }

    private static AdminUsersController CreateController(FakeAccountAdministrationService service)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())],
            "test"));
        return new AdminUsersController(service)
        {
            ControllerContext = ControllerTestContext.Create(user: principal)
        };
    }

    private sealed class FakeAccountAdministrationService : IAccountAdministrationService
    {
        public CreateUserResponse CreateUserResponse { get; init; } = new();
        public ReissueActivationResponse ReissueActivationResponse { get; init; } = new();
        public IReadOnlyCollection<UserSummaryResponse> Users { get; init; } = [];
        public IReadOnlyCollection<ProjectResponse> Projects { get; init; } = [];
        public UserAccessResponse UserAccess { get; init; } = new();
        public InvalidOperationException? CreateUserException { get; init; }
        public InvalidOperationException? ReissueActivationException { get; init; }
        public KeyNotFoundException? EnableException { get; init; }
        public CreateUserRequest? CreateUserRequest { get; private set; }
        public string? CreateUserCorrelationId { get; private set; }
        public Guid? ReissuedUserId { get; private set; }
        public ReissueActivationRequest? ReissueActivationRequest { get; private set; }
        public string? ReissueCorrelationId { get; private set; }
        public Guid? DisabledUserId { get; private set; }
        public string? DisableReason { get; private set; }
        public string? DisableCorrelationId { get; private set; }
        public Guid? EnabledUserId { get; private set; }
        public string? EnableReason { get; private set; }
        public string? EnableCorrelationId { get; private set; }
        public Guid? UpdatedAccessUserId { get; private set; }
        public UpdateUserAccessRequest? UpdatedAccessRequest { get; private set; }
        public string? UpdatedAccessCorrelationId { get; private set; }

        public Task<CreateUserResponse> CreateUserAsync(
            ClaimsPrincipal administrator,
            CreateUserRequest request,
            string? correlationId,
            CancellationToken cancellationToken)
        {
            CreateUserRequest = request;
            CreateUserCorrelationId = correlationId;
            if (CreateUserException is not null)
            {
                throw CreateUserException;
            }

            return Task.FromResult(CreateUserResponse);
        }

        public Task<ReissueActivationResponse> ReissueActivationAsync(
            ClaimsPrincipal administrator,
            Guid userId,
            ReissueActivationRequest request,
            string? correlationId,
            CancellationToken cancellationToken)
        {
            ReissuedUserId = userId;
            ReissueActivationRequest = request;
            ReissueCorrelationId = correlationId;
            if (ReissueActivationException is not null)
            {
                throw ReissueActivationException;
            }

            return Task.FromResult(ReissueActivationResponse);
        }

        public Task<IReadOnlyCollection<UserSummaryResponse>> GetUsersAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Users);

        public Task<int> GetUserCountAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Users.Count);

        public Task<IReadOnlyCollection<ProjectResponse>> GetProjectsAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Projects);

        public Task<UserAccessResponse> GetUserAccessAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(UserAccess);

        public Task UpdateUserAccessAsync(
            ClaimsPrincipal administrator,
            Guid userId,
            UpdateUserAccessRequest request,
            string? correlationId,
            CancellationToken cancellationToken)
        {
            UpdatedAccessUserId = userId;
            UpdatedAccessRequest = request;
            UpdatedAccessCorrelationId = correlationId;
            return Task.CompletedTask;
        }

        public Task UnlockAsync(ClaimsPrincipal administrator, Guid userId, string? correlationId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DisableAsync(ClaimsPrincipal administrator, Guid userId, string reason, string? correlationId, CancellationToken cancellationToken)
        {
            DisabledUserId = userId;
            DisableReason = reason;
            DisableCorrelationId = correlationId;
            return Task.CompletedTask;
        }

        public Task EnableAsync(ClaimsPrincipal administrator, Guid userId, string reason, string? correlationId, CancellationToken cancellationToken)
        {
            EnabledUserId = userId;
            EnableReason = reason;
            EnableCorrelationId = correlationId;

            if (EnableException is not null)
            {
                throw EnableException;
            }

            return Task.CompletedTask;
        }
    }
}
