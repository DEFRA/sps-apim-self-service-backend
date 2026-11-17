using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Sps.Apim.SelfServe.Api.Controllers;
using Sps.Apim.SelfServe.Application.AccountManagement;
using Xunit;

namespace Sps.Apim.SelfServe.UnitTests;

public sealed class AccountControllerTests
{
    [Fact]
    public async Task AdminAccessRequests_records_decision_and_returns_updated_request()
    {
        var service = new FakeAccessRequestService();
        var controller = new AdminAccessRequestsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = ControllerTestContext.Create().HttpContext
            }
        };
        var requestId = Guid.NewGuid();
        var decision = new AccessRequestDecisionRequest
        {
            Outcome = "Approved",
            Role = "API Developer",
            ProjectId = Guid.NewGuid(),
            DecisionNote = "Approved by the ICC."
        };

        var result = await controller.RecordDecision(
            requestId,
            decision,
            CancellationToken.None);

        var response = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Approved", Assert.IsType<AccessRequestResponse>(response.Value).Status);
        Assert.Equal(requestId, service.DecidedRequestId);
        Assert.Same(decision, service.RecordedDecision);
    }

    [Fact]
    public async Task AdminAccessRequests_can_retry_decision_email()
    {
        var service = new FakeAccessRequestService();
        var controller = new AdminAccessRequestsController(service);
        var requestId = Guid.NewGuid();

        var result = await controller.SendDecisionEmail(
            requestId,
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(requestId, service.EmailedRequestId);
    }

    [Theory]
    [InlineData(true, 204)]
    [InlineData(false, 400)]
    public async Task Activate_returns_result_from_account_service(bool succeeded, int expectedStatusCode)
    {
        var service = new FakeAccountRecoveryService { ActivateResult = succeeded };
        var controller = CreateController(service);
        var request = new ActivateAccountRequest
        {
            UserId = Guid.NewGuid(),
            Token = "activation-token",
            Password = "TestPassword123!"
        };

        var result = await controller.Activate(request, CancellationToken.None);

        Assert.Equal(expectedStatusCode, Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode);
        Assert.Same(request, service.ActivateRequest);
        Assert.Equal("test-trace", service.ActivateCorrelationId);
    }

    [Theory]
    [InlineData("Development", 200)]
    [InlineData("Production", 204)]
    public async Task RequestPasswordReset_exposes_result_only_in_development(
        string environmentName,
        int expectedStatusCode)
    {
        var response = new RequestPasswordResetResponse
        {
            ResetUrl = "https://example.test/reset",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1)
        };
        var service = new FakeAccountRecoveryService { PasswordResetRequestResponse = response };
        var controller = CreateController(service, environmentName);
        var request = new RequestPasswordResetRequest { Email = "user@example.com" };

        var result = await controller.RequestPasswordReset(request, CancellationToken.None);

        Assert.Equal(expectedStatusCode, Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode);
        if (environmentName == "Development")
        {
            Assert.Same(response, Assert.IsType<OkObjectResult>(result).Value);
        }
        else
        {
            Assert.IsType<NoContentResult>(result);
        }

        Assert.Same(request, service.PasswordResetRequest);
        Assert.Equal("test-trace", service.PasswordResetCorrelationId);
    }

    [Fact]
    public async Task SubmitAccessRequest_forwards_request_to_service_and_returns_no_content()
    {
        var service = new FakeAccountRecoveryService();
        var accessRequestService = new FakeAccessRequestService();
        var controller = CreateController(service, accessRequestService);
        var request = new AccessRequestRequest
        {
            FirstName = "Ada",
            LastName = "Lovelace",
            Email = "ada@example.com",
            RequestType = "Onboard a new API",
            Reason = "I need to onboard the service."
        };

        var result = await controller.SubmitAccessRequest(
            request,
            CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Same(request, accessRequestService.SubmittedRequest);
    }

    [Theory]
    [InlineData(true, 204)]
    [InlineData(false, 400)]
    public async Task CompletePasswordReset_returns_result_from_account_service(
        bool succeeded,
        int expectedStatusCode)
    {
        var service = new FakeAccountRecoveryService { ResetPasswordResult = succeeded };
        var controller = CreateController(service);
        var request = new CompletePasswordResetRequest
        {
            UserId = Guid.NewGuid(),
            Token = "dummy-test-token",
            NewPassword = "TestPassword123!"
        };

        var result = await controller.CompletePasswordReset(request, CancellationToken.None);

        Assert.Equal(expectedStatusCode, Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode);
        Assert.Same(request, service.CompletePasswordResetRequest);
        Assert.Equal("test-trace", service.ResetPasswordCorrelationId);
    }

    private static AccountController CreateController(
        FakeAccountRecoveryService service,
        string environmentName = "Production") =>
        CreateController(service, new FakeAccessRequestService(), environmentName);

    private static AccountController CreateController(
        FakeAccountRecoveryService service,
        FakeAccessRequestService accessRequestService,
        string environmentName = "Production")
    {
        var httpContext = ControllerTestContext.Create().HttpContext;
        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(
            new TestWebHostEnvironment(environmentName));
        httpContext.RequestServices = services.BuildServiceProvider();

        return new AccountController(service, accessRequestService)
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext }
        };
    }

    private sealed class FakeAccountRecoveryService : IAccountRecoveryService
    {
        public bool ActivateResult { get; init; }
        public bool ResetPasswordResult { get; init; }
        public RequestPasswordResetResponse PasswordResetRequestResponse { get; init; } = new();
        public ActivateAccountRequest? ActivateRequest { get; private set; }
        public string? ActivateCorrelationId { get; private set; }
        public RequestPasswordResetRequest? PasswordResetRequest { get; private set; }
        public string? PasswordResetCorrelationId { get; private set; }
        public CompletePasswordResetRequest? CompletePasswordResetRequest { get; private set; }
        public string? ResetPasswordCorrelationId { get; private set; }

        public Task<bool> ActivateAsync(ActivateAccountRequest request, string? correlationId, CancellationToken cancellationToken)
        {
            ActivateRequest = request;
            ActivateCorrelationId = correlationId;
            return Task.FromResult(ActivateResult);
        }

        public Task<RequestPasswordResetResponse> RequestPasswordResetAsync(
            RequestPasswordResetRequest request,
            string? correlationId,
            CancellationToken cancellationToken)
        {
            PasswordResetRequest = request;
            PasswordResetCorrelationId = correlationId;
            return Task.FromResult(PasswordResetRequestResponse);
        }

        public Task<bool> ResetPasswordAsync(
            CompletePasswordResetRequest request,
            string? correlationId,
            CancellationToken cancellationToken)
        {
            CompletePasswordResetRequest = request;
            ResetPasswordCorrelationId = correlationId;
            return Task.FromResult(ResetPasswordResult);
        }
    }

    private sealed class FakeAccessRequestService : IAccessRequestService
    {
        public AccessRequestRequest? SubmittedRequest { get; private set; }
        public Guid? DecidedRequestId { get; private set; }
        public AccessRequestDecisionRequest? RecordedDecision { get; private set; }
        public Guid? EmailedRequestId { get; private set; }

        public Task SubmitAsync(
            AccessRequestRequest request,
            CancellationToken cancellationToken)
        {
            SubmittedRequest = request;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<AccessRequestResponse>> GetAllAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<AccessRequestResponse>>([]);

        public Task<AccessRequestResponse?> GetAsync(
            Guid requestId,
            CancellationToken cancellationToken) =>
            Task.FromResult<AccessRequestResponse?>(null);

        public Task<int> GetCountAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult(0);

        public Task<AccessRequestResponse> RecordDecisionAsync(
            System.Security.Claims.ClaimsPrincipal administrator,
            Guid requestId,
            AccessRequestDecisionRequest decision,
            string? correlationId,
            CancellationToken cancellationToken)
        {
            DecidedRequestId = requestId;
            RecordedDecision = decision;
            return Task.FromResult(new AccessRequestResponse
            {
                Id = requestId,
                Status = decision.Outcome
            });
        }

        public Task SendDecisionEmailAsync(
            Guid requestId,
            CancellationToken cancellationToken)
        {
            EmailedRequestId = requestId;
            return Task.CompletedTask;
        }
    }
}
