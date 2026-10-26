using System.ComponentModel.DataAnnotations;

namespace Sps.Apim.SelfServe.Application.Authentication;

// ---------------------------------------------------------
// Sign-in request
// ---------------------------------------------------------

public sealed class SignInRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;
}

// ---------------------------------------------------------
// Change password request
// ---------------------------------------------------------

public sealed class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; init; } = string.Empty;

    [Required]
    [MinLength(12)]
    public string NewPassword { get; init; } = string.Empty;
}

// ---------------------------------------------------------
// Project available to authenticated user
// ---------------------------------------------------------

public sealed record ProjectItem(
    Guid Id,
    string Name);

// ---------------------------------------------------------
// Authenticated user session
// ---------------------------------------------------------

public sealed record SessionResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string[] Roles,
    IReadOnlyCollection<ProjectItem> Projects);