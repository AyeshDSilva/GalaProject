using System.Net;
using GalaReconciler.Tests.Helpers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace GalaReconciler.Tests;

/// <summary>Checks the app boots and views resolve from the feature folders.</summary>
public class WebSmokeTests(GalaWebApplicationFactory factory) : IClassFixture<GalaWebApplicationFactory>
{
    [Fact]
    public async Task Upload_page_renders_from_the_feature_folder()
    {
        var client = factory.CreateClient();

        var html = await client.GetStringAsync("/");

        Assert.Contains("GALA Reconciler", html);
        Assert.Contains("Source file (RSVP export)", html);
        Assert.Contains("Processed file (master guest list)", html);
        Assert.Contains("__RequestVerificationToken", html);
        Assert.Contains("/css/site.css", html);
    }

    [Fact]
    public async Task Unknown_page_shows_the_friendly_error_page()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Page not found", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Getting_the_compare_url_redirects_to_the_upload_page()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Reconciliation/Compare");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }
}
