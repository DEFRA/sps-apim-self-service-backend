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

    private static AccountController CreateController(FakeAccountRecoveryService service, string environmentName = "Production")
    {
        var httpContext = ControllerTestContext.Create().HttpContext;
        var services = new ServiceCollection();
        services.AddSingleton<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>(
            new TestWebHostEnvironment(environmentName));
        httpContext.RequestServices = services.BuildServiceProvider();

        return new AccountController(service)
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
}
