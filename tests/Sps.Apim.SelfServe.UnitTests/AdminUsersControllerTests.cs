using System.Security.Claims;
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
    public async Task ReissueActivation_returns_response_from_service()
    {
        var expected = new ReissueActivationResponse
        {
            UserId = Guid.NewGuid(),
            ActivationUrl = "https://example.test/activate"
        };
        var service = new FakeAccountAdministrationService { ReissueActivationResponse = expected };
        var controller = CreateController(service);
        var userId = Guid.NewGuid();
        var request = new ReissueActivationRequest { Reason = "Requested by user" };

        var result = Assert.IsType<OkObjectResult>(
            await controller.ReissueActivation(userId, request, CancellationToken.None));

        Assert.Same(expected, result.Value);
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
        public InvalidOperationException? CreateUserException { get; init; }
        public InvalidOperationException? ReissueActivationException { get; init; }
        public CreateUserRequest? CreateUserRequest { get; private set; }
        public string? CreateUserCorrelationId { get; private set; }
        public Guid? ReissuedUserId { get; private set; }
        public ReissueActivationRequest? ReissueActivationRequest { get; private set; }
        public string? ReissueCorrelationId { get; private set; }

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

        public Task ReissueActivationAsync(Guid userId, string reason, string? correlationId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task UnlockAsync(Guid userId, string? correlationId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DisableAsync(Guid userId, string reason, string? correlationId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task EnableAsync(Guid userId, string reason, string? correlationId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
