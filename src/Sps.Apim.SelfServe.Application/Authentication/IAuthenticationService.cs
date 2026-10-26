using System.Security.Claims;
namespace Sps.Apim.SelfServe.Application.Authentication;
public interface IAuthenticationService { Task<bool> SignInAsync(SignInRequest request,string? correlationId,CancellationToken ct); Task SignOutAsync(Guid? userId,string? correlationId,CancellationToken ct); Task<SessionResponse?> GetSessionAsync(ClaimsPrincipal principal,CancellationToken ct); Task<bool> ChangePasswordAsync(ClaimsPrincipal principal,ChangePasswordRequest request,string? correlationId,CancellationToken ct); }
