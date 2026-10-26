using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sps.Apim.SelfServe.Domain.Constants;
using Sps.Apim.SelfServe.Domain.Enums;

namespace Sps.Apim.SelfServe.Infrastructure.Identity;

public sealed class DatabaseSeeder
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly BootstrapAdminOptions _options;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IOptions<BootstrapAdminOptions> options,
        ILogger<DatabaseSeeder> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _options = options.Value;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        await EnsureRoleExistsAsync(RoleNames.Administrator);
        await EnsureRoleExistsAsync(RoleNames.User);

        if (!_options.Enabled)
        {
            _logger.LogInformation(
                "Bootstrap administrator creation is disabled.");

            return;
        }

        ValidateBootstrapConfiguration();

        var admin = await _userManager.FindByEmailAsync(_options.Email);

        if (admin is null)
        {
            var now =
                DateTimeOffset.UtcNow;

            admin = new ApplicationUser
            {
                Id = Guid.NewGuid(),

                UserName =
                    _options.Email,

                Email =
                    _options.Email,

                DisplayName =
                    _options.DisplayName,

                EmailConfirmed = true,

                Status =
                    AccountStatus.Active,

                CreatedAtUtc = now,

                ActivatedAtUtc = now
            };

            var result = await _userManager.CreateAsync(
                admin,
                _options.Password);

            if (!result.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to create bootstrap administrator: " +
                    $"{FormatErrors(result.Errors)}");
            }

            _logger.LogInformation(
                "Bootstrap administrator account created.");
        }

        if (!admin.IsActive)
        {
            throw new InvalidOperationException(
                "The configured bootstrap administrator exists but is disabled.");
        }

        if (!await _userManager.IsInRoleAsync(
                admin,
                RoleNames.Administrator))
        {
            var roleResult = await _userManager.AddToRoleAsync(
                admin,
                RoleNames.Administrator);

            if (!roleResult.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Unable to assign Administrator role: " +
                    $"{FormatErrors(roleResult.Errors)}");
            }

            _logger.LogInformation(
                "Administrator role assigned to bootstrap account.");
        }
    }

    private async Task EnsureRoleExistsAsync(string roleName)
    {
        if (await _roleManager.RoleExistsAsync(roleName))
        {
            return;
        }

        var role = new ApplicationRole
        {
            Id = Guid.NewGuid(),
            Name = roleName
        };

        var result = await _roleManager.CreateAsync(role);

        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Unable to create role '{roleName}': " +
                $"{FormatErrors(result.Errors)}");
        }

        _logger.LogInformation(
            "Identity role {RoleName} created.",
            roleName);
    }

    private void ValidateBootstrapConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.Email))
        {
            throw new InvalidOperationException(
                "BootstrapAdmin:Email is required.");
        }

        if (string.IsNullOrWhiteSpace(_options.DisplayName))
        {
            throw new InvalidOperationException(
                "BootstrapAdmin:DisplayName is required.");
        }

        if (string.IsNullOrWhiteSpace(_options.Password))
        {
            throw new InvalidOperationException(
                "BootstrapAdmin:Password is required.");
        }
    }

    private static string FormatErrors(
        IEnumerable<IdentityError> errors)
    {
        return string.Join(
            "; ",
            errors.Select(error =>
                $"{error.Code}: {error.Description}"));
    }
}