using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;

namespace Sps.Apim.SelfServe.UnitTests;

internal static class ControllerTestContext
{
    public static ControllerContext Create(
        string traceIdentifier = "test-trace",
        System.Security.Claims.ClaimsPrincipal? user = null)
    {
        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = traceIdentifier,
            User = user ?? new System.Security.Claims.ClaimsPrincipal()
        };

        return new ControllerContext
        {
            HttpContext = httpContext
        };
    }
}

internal sealed class TestWebHostEnvironment(string environmentName)
    : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "UnitTests";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = environmentName;
    public string ContentRootPath { get; set; } = string.Empty;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
