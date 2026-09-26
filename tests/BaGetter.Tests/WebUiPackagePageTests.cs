using System;
using System.Threading.Tasks;
using BaGetter.Tests.Support;
using Xunit;
using Xunit.Abstractions;

namespace BaGetter.Tests;

/// <summary>
/// The rendered package page, through the real routing.
/// </summary>
public class WebUiPackagePageTests : IDisposable
{
    private readonly BaGetterApplication _app;

    public WebUiPackagePageTests(ITestOutputHelper output)
    {
        _app = new BaGetterApplication(output);
    }

    [Fact]
    public async Task RendersTheInstallSnippets()
    {
        await _app.AddPackageAsync(TestResources.GetResourceStream(TestResources.Package));
        using var client = _app.CreateClient();

        var html = await client.GetStringAsync("/packages/TestData/1.2.3");

        Assert.Contains("\"dotnet add package TestData --version 1.2.3\"", html);
        Assert.Contains("\"NuGet\\\\Install-Package TestData -Version 1.2.3\"", html);
        Assert.Contains("\"<PackageVersion Include=\\\"TestData\\\" Version=\\\"1.2.3\\\" />\"", html);
        Assert.Contains("\"<PackageReference Include=\\\"TestData\\\" />\"", html);
        Assert.Contains("\"#r \\\"nuget: TestData, 1.2.3\\\"\"", html);
        Assert.Contains("\"#:package TestData@1.2.3\"", html);
        Assert.Contains("\"#addin nuget:?package=TestData&version=1.2.3\"", html);
        Assert.Contains("\"#tool nuget:?package=TestData&version=1.2.3\"", html);
    }

    public void Dispose()
    {
        _app.Dispose();
    }
}
