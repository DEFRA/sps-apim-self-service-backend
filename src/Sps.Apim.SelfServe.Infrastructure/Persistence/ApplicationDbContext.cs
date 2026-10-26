using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Sps.Apim.SelfServe.Domain.Entities;
using Sps.Apim.SelfServe.Infrastructure.Identity;

namespace Sps.Apim.SelfServe.Infrastructure.Persistence;

public class ApplicationDbContext
    : IdentityDbContext<
        ApplicationUser,
        ApplicationRole,
        Guid>
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Project> Projects =>
        Set<Project>();

    public DbSet<UserProject> UserProjects =>
        Set<UserProject>();

    public DbSet<AuditEvent> AuditEvents =>
        Set<AuditEvent>();

    public DbSet<AccountToken> AccountTokens =>
        Set<AccountToken>();

    protected override void OnModelCreating(
        ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly);
    }
}