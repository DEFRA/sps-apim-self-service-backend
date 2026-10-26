using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Sps.Apim.SelfServe.Api.Controllers;
using Sps.Apim.SelfServe.Application.Authentication;
using Xunit;

namespace Sps.Apim.SelfServe.UnitTests;

public sealed class AuthControllerTests
{
    [Fact]
    public void Csrf_returns_request_token()
    {
        var antiforgery = new FakeAntiforgery();
        var controller = CreateController(new FakeAuthenticationService(), antiforgery);

        var result = Assert.IsType<OkObjectResult>(controller.Csrf());

        Assert.Equal("request-token", result.Value!.GetType().GetProperty("token")!.GetValue(result.Value));
        Assert.True(antiforgery.TokensRequested);
    }

    [Theory]
    [InlineData(true, 204)]
    [InlineData(false, 401)]
    public async Task SignIn_returns_result_from_authentication_service(
        bool succeeded,
        int expectedStatusCode)
    {
        var service = new FakeAuthenticationService { SignInResult = succeeded };
        var controller = CreateController(service);
        var request = new SignInRequest { Email = "user@example.com", Password = "secret" };

        var result = await controller.SignIn(request, CancellationToken.None);

        Assert.Equal(expectedStatusCode, Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode);
        Assert.Same(request, service.SignInRequest);
        Assert.Equal("test-trace", service.SignInCorrelationId);
    }

    [Fact]
    public async Task Session_returns_session_when_user_is_authenticated()
    {
        var expected = new SessionResponse(Guid.NewGuid(), "user@example.com", "User", [], []);
        var service = new FakeAuthenticationService { Session = expected };
        var controller = CreateController(service);

        var result = await controller.Session(CancellationToken.None);

        Assert.Same(expected, Assert.IsType<OkObjectResult>(result).Value);
    }

    [Fact]
    public async Task Session_returns_unauthorized_when_service_has_no_session()
    {
        var controller = CreateController(new FakeAuthenticationService());

        var result = await controller.Session(CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task SignOut_passes_parsed_user_id_to_service()
    {
        var userId = Guid.NewGuid();
        var service = new FakeAuthenticationService();
        var controller = CreateController(
            service,
            user: new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString())],
                "test")));

        var result = await controller.SignOut(CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(userId, service.SignedOutUserId);
        Assert.Equal("test-trace", service.SignOutCorrelationId);
    }

    [Fact]
    public async Task SignOut_passes_null_when_user_id_claim_is_not_a_guid()
    {
        var service = new FakeAuthenticationService();
        var controller = CreateController(
            service,
            user: new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "invalid")],
                "test")));

        await controller.SignOut(CancellationToken.None);

        Assert.Null(service.SignedOutUserId);
    }

    [Theory]
    [InlineData(true, 204)]
    [InlineData(false, 400)]
    public async Task ChangePassword_returns_result_from_authentication_service(
        bool succeeded,
        int expectedStatusCode)
    {
        var service = new FakeAuthenticationService { ChangePasswordResult = succeeded };
        var controller = CreateController(service);
        var request = new ChangePasswordRequest
        {
            CurrentPassword = Guid.NewGuid().ToString(),
            NewPassword = Guid.NewGuid().ToString()
        };

        var result = await controller.ChangePassword(request, CancellationToken.None);

        Assert.Equal(expectedStatusCode, Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode);
        Assert.Same(request, service.ChangePasswordRequest);
        Assert.Equal("test-trace", service.ChangePasswordCorrelationId);
    }

    private static AuthController CreateController(
        FakeAuthenticationService service,
        FakeAntiforgery? antiforgery = null,
        ClaimsPrincipal? user = null)
    {
        return new AuthController(service, antiforgery ?? new FakeAntiforgery())
        {
            ControllerContext = ControllerTestContext.Create(user: user)
        };
    }

    private sealed class FakeAuthenticationService : IAuthenticationService
    {
        public bool SignInResult { get; init; }
        public SessionResponse? Session { get; init; }
        public bool ChangePasswordResult { get; init; }
        public SignInRequest? SignInRequest { get; private set; }
        public string? SignInCorrelationId { get; private set; }
        public Guid? SignedOutUserId { get; private set; }
        public string? SignOutCorrelationId { get; private set; }
        public ChangePasswordRequest? ChangePasswordRequest { get; private set; }
        public string? ChangePasswordCorrelationId { get; private set; }

        public Task<bool> SignInAsync(SignInRequest request, string? correlationId, CancellationToken ct)
        {
            SignInRequest = request;
            SignInCorrelationId = correlationId;
            return Task.FromResult(SignInResult);
        }

        public Task SignOutAsync(Guid? userId, string? correlationId, CancellationToken ct)
        {
            SignedOutUserId = userId;
            SignOutCorrelationId = correlationId;
            return Task.CompletedTask;
        }

        public Task<SessionResponse?> GetSessionAsync(ClaimsPrincipal principal, CancellationToken ct) =>
            Task.FromResult(Session);

        public Task<bool> ChangePasswordAsync(
            ClaimsPrincipal principal,
            ChangePasswordRequest request,
            string? correlationId,
            CancellationToken ct)
        {
            ChangePasswordRequest = request;
            ChangePasswordCorrelationId = correlationId;
            return Task.FromResult(ChangePasswordResult);
        }
    }

    private sealed class FakeAntiforgery : IAntiforgery
    {
        public bool TokensRequested { get; private set; }

        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext)
        {
            TokensRequested = true;
            return new AntiforgeryTokenSet("request-token", "cookie-token", "header", "form");
        }

        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) =>
            new("request-token", "cookie-token", "header", "form");

        public Task<bool> IsRequestValidAsync(HttpContext httpContext) =>
            Task.FromResult(true);

        public Task ValidateRequestAsync(HttpContext httpContext) =>
            Task.CompletedTask;

        public void SetCookieTokenAndHeader(HttpContext httpContext) { }
    }
}
