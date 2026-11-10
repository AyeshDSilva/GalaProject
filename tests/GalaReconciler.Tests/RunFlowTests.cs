using System.Net;
using GalaReconciler.Tests.Helpers;
using GalaReconciler.Web.Features.Reconciliation.Realtime;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace GalaReconciler.Tests;

/// <summary>Through the real app: upload → queued run → background worker → saved results, plus SignalR.</summary>
public class RunFlowTests(GalaWebApplicationFactory factory) : IClassFixture<GalaWebApplicationFactory>
{
    private HttpClient Client() => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [Fact]
    public async Task Upload_is_processed_in_the_background_and_saved_as_a_run()
    {
        var client = Client();

        var runUrl = await TestHandlers.UploadSamplesAsync(client);
        Assert.Matches("^/runs/[0-9a-f-]{36}$", runUrl);

        var html = await TestHandlers.WaitForRunAsync(client, runUrl);
        Assert.Contains("Reconciliation results", html);
        Assert.Contains("William Hartley", html);
        Assert.Contains("matched by email", html);

        Assert.Contains(runUrl, await client.GetStringAsync("/runs"));
        Assert.Contains(runUrl, await client.GetStringAsync("/runs?Guest=william&Type=MobileNumberMismatch"));
        Assert.DoesNotContain(runUrl, await client.GetStringAsync("/runs?Guest=nobody-with-this-name"));
    }

    [Fact]
    public async Task Files_with_missing_headers_produce_a_failed_run()
    {
        var client = Client();

        var runUrl = await TestHandlers.UploadAsync(client, SampleWorkbookBuilder.Processed().ToStream(), SampleWorkbookBuilder.SingleExportSource().ToStream());

        var html = await TestHandlers.WaitForRunAsync(client, runUrl);
        Assert.Contains("missing required column(s)", html);
        Assert.Contains("Try again", html);
    }

    [Fact]
    public async Task Deleted_run_is_gone()
    {
        var client = Client();
        var runUrl = await TestHandlers.UploadSamplesAsync(client);
        var html = await TestHandlers.WaitForRunAsync(client, runUrl);
        var token = System.Text.RegularExpressions.Regex.Match(html, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;

        var response = await client.PostAsync($"{runUrl}/delete", new FormUrlEncodedContent([new("__RequestVerificationToken", token)]));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(runUrl)).StatusCode);
    }

    [Fact]
    public async Task Hub_tells_a_watching_browser_when_the_run_completes()
    {
        var client = Client();
        var server = factory.Server;
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, ComparisonHub.Path.TrimStart('/')), options =>
            {
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
                options.Transports = HttpTransportType.LongPolling;
            })
            .Build();

        var runId = Guid.Empty;
        var finished = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<Guid, string>(ComparisonHub.RunStatusChanged, (id, status) =>
        {
            if (id == runId && status is "Completed" or "Failed")
            {
                finished.TrySetResult(status);
            }
        });
        await connection.StartAsync();

        // Same as the page: watch the run, and use the returned status in case it already finished.
        var runUrl = await TestHandlers.UploadSamplesAsync(client);
        runId = Guid.Parse(runUrl["/runs/".Length..]);
        var current = await connection.InvokeAsync<string?>("WatchRun", runId);
        if (current is "Completed" or "Failed")
        {
            finished.TrySetResult(current);
        }

        Assert.Equal("Completed", await finished.Task.WaitAsync(TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public async Task Upload_over_the_request_limit_shows_a_bad_request_page()
    {
        var client = Client();
        var form = await client.GetStringAsync("/");
        var token = System.Text.RegularExpressions.Regex.Match(form, "name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"").Groups[1].Value;
        using var content = new MultipartFormDataContent
        {
            { new StringContent(token), "__RequestVerificationToken" },
            { new ByteArrayContent(new byte[25_000_000]), "SourceFile", "Big.xlsx" },
            { new StreamContent(SampleWorkbookBuilder.Processed().ToStream()), "ProcessedFile", "Processed.xlsx" },
        };

        var response = await client.PostAsync("/Reconciliation/Compare", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Error 400", html);
        Assert.Contains("10 MB or smaller", html);
    }

    [Fact]
    public async Task Unknown_run_is_not_found() =>
        Assert.Equal(HttpStatusCode.NotFound, (await Client().GetAsync($"/runs/{Guid.NewGuid()}")).StatusCode);
}
