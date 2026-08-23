using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.Blueprints;
using StudioTechBI.Application.DTOs.ReportDesigner;
using StudioTechBI.Application.DTOs.VisualPlan;
using StudioTechBI.Application.Models;
using StudioTechBI.Infrastructure.Clients;
using Xunit;

namespace StudioTechBI.Infrastructure.Tests.Clients;

public class AgentHostClientTests
{
    private static AgentHostClient CreateClient(
        FakeHttpMessageHandler handler,
        Uri? baseAddress,
        AgentHostOptions? options = null,
        CreditsOptions? creditsOptions = null)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = baseAddress
        };

        return new AgentHostClient(
            httpClient,
            NullLogger<AgentHostClient>.Instance,
            Options.Create(options ?? new AgentHostOptions()),
            Options.Create(creditsOptions ?? new CreditsOptions()));
    }

    private static GenerateBlueprintRequest ValidRequest(Guid? tenantId) => new()
    {
        TenantId = tenantId,
        Industry = "Retail",
        BusinessCapability = "Sales Reporting",
        BusinessGoal = "Track weekly revenue",
    };

    private const string ValidResponseJson = """
        {
          "requestId": "5b1f3c2e-6a3d-4e8c-9f1a-2b3c4d5e6f7a",
          "status": "Completed",
          "provider": "OpenAI",
          "model": "gpt-4o",
          "processingTimeMs": 1000,
          "confidence": 85,
          "blueprint": { "schemaVersion": "1.0" },
          "warnings": []
        }
        """;

    // ── CheckHealthAsync ────────────────────────────────────────────────

    [Fact]
    public async Task CheckHealthAsync_2xxResponse_ReturnsTrue()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        var client = CreateClient(handler, new Uri("https://agenthost.example.com/"));

        var healthy = await client.CheckHealthAsync();

        Assert.True(healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_NonSuccessResponse_ReturnsFalse()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.ServiceUnavailable);
        var client = CreateClient(handler, new Uri("https://agenthost.example.com/"));

        var healthy = await client.CheckHealthAsync();

        Assert.False(healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_NullBaseAddress_ReturnsFalseWithoutThrowing()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        var client = CreateClient(handler, baseAddress: null);

        var healthy = await client.CheckHealthAsync();

        Assert.False(healthy);
    }

    [Fact]
    public async Task CheckHealthAsync_UsesConfiguredHealthCheckPath()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        var client = CreateClient(
            handler,
            new Uri("https://agenthost.example.com/"),
            new AgentHostOptions { HealthCheckPath = "api/health" });

        await client.CheckHealthAsync();

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("https://agenthost.example.com/api/health", handler.LastRequest!.RequestUri!.ToString());
    }

    // ── GenerateBlueprintAsync ──────────────────────────────────────────

    [Fact]
    public async Task GenerateBlueprintAsync_SendsXTenantIdHeader()
    {
        var tenantId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponseJson);
        var client = CreateClient(handler, new Uri("https://agenthost.example.com/"));

        await client.GenerateBlueprintAsync(ValidRequest(tenantId), correlationId: "corr-1");

        Assert.NotNull(handler.LastRequest);
        Assert.True(handler.LastRequest!.Headers.TryGetValues("X-Tenant-Id", out var values));
        Assert.Equal(tenantId.ToString(), values!.Single());
    }

    [Fact]
    public async Task GenerateBlueprintAsync_NullTenantId_ThrowsBeforeSendingRequest()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponseJson);
        var client = CreateClient(handler, new Uri("https://agenthost.example.com/"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GenerateBlueprintAsync(ValidRequest(tenantId: null), correlationId: "corr-1"));

        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task GenerateBlueprintAsync_EmptyGuidTenantId_ThrowsBeforeSendingRequest()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidResponseJson);
        var client = CreateClient(handler, new Uri("https://agenthost.example.com/"));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.GenerateBlueprintAsync(ValidRequest(Guid.Empty), correlationId: "corr-1"));

        Assert.Null(handler.LastRequest);
    }

    [Fact]
    public async Task GenerateBlueprintAsync_NonSuccessStatusCode_ThrowsHttpRequestException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.BadRequest, "{\"error\":\"bad request\"}");
        var client = CreateClient(handler, new Uri("https://agenthost.example.com/"));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GenerateBlueprintAsync(ValidRequest(Guid.NewGuid()), correlationId: "corr-1"));
    }

    // ── GenerateVisualPlanAsync ─────────────────────────────────────────

    private static VisualPlanGenerationRequest ValidVisualPlanRequest() => new(
        Tables: new List<VisualPlanTableDto>
        {
            new(
                TableName: "sales",
                Columns: new List<VisualPlanColumnDto> { new("Amount", "numeric"), new("Region", "categorical") },
                SampleRows: new List<Dictionary<string, string>> { new() { ["Amount"] = "120.5", ["Region"] = "North" } })
        },
        StarSchema: new StarSchemaDto("sales", new List<string> { "region" }, new List<RelationshipDto>()));

    private const string ValidVisualPlanResponseJson = """
        [
          {
            "Id": "chart-1",
            "Title": "Amount by Region",
            "ChartType": "bar",
            "Measure": "Amount",
            "Dimension": "Region",
            "DrillPath": ["Region"],
            "FilterField": "Region",
            "ValueKind": "absolute",
            "PairId": null
          }
        ]
        """;

    [Fact]
    public async Task GenerateVisualPlanAsync_Success_ReturnsChartSpecs()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidVisualPlanResponseJson);
        var client = CreateClient(handler, new Uri("https://agenthost.example.com/"));

        var specs = await client.GenerateVisualPlanAsync(ValidVisualPlanRequest(), correlationId: "corr-1");

        Assert.Single(specs);
        Assert.Equal("chart-1", specs[0].Id);
        Assert.Equal("bar", specs[0].ChartType);
        Assert.Equal("Amount", specs[0].Measure);
        Assert.Equal("Region", specs[0].Dimension);
        Assert.Equal("absolute", specs[0].ValueKind);
        Assert.Null(specs[0].PairId);
    }

    [Fact]
    public async Task GenerateVisualPlanAsync_SendsCorrelationIdHeaderAndCorrectPath()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, ValidVisualPlanResponseJson);
        var client = CreateClient(handler, new Uri("https://agenthost.example.com/"));

        await client.GenerateVisualPlanAsync(ValidVisualPlanRequest(), correlationId: "corr-42");

        Assert.NotNull(handler.LastRequest);
        Assert.Equal("https://agenthost.example.com/api/visual-plan/generate", handler.LastRequest!.RequestUri!.ToString());
        Assert.True(handler.LastRequest.Headers.TryGetValues("X-Correlation-Id", out var values));
        Assert.Equal("corr-42", values!.Single());
    }

    [Fact]
    public async Task GenerateVisualPlanAsync_NonSuccessStatusCode_ThrowsHttpRequestException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.BadRequest, "{\"error\":\"bad request\"}");
        var client = CreateClient(handler, new Uri("https://agenthost.example.com/"));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GenerateVisualPlanAsync(ValidVisualPlanRequest(), correlationId: "corr-1"));
    }

    [Fact]
    public async Task GenerateVisualPlanAsync_EmptyArrayResponse_ReturnsEmptyList()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "[]");
        var client = CreateClient(handler, new Uri("https://agenthost.example.com/"));

        var specs = await client.GenerateVisualPlanAsync(ValidVisualPlanRequest(), correlationId: "corr-1");

        Assert.Empty(specs);
    }

    // ── DownloadPdfAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task DownloadPdfAsync_SuccessResponse_ReturnsBytes()
    {
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46 }; // "%PDF"
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(pdfBytes)
        });
        var client = CreateClient(handler, new Uri("https://agenthost.example.com/"));

        var result = await client.DownloadPdfAsync("https://agenthost.example.com/api/blueprints/abc/pdf");

        Assert.Equal(pdfBytes, result);
    }

    [Fact]
    public async Task DownloadPdfAsync_NonSuccessResponse_ReturnsNullWithoutThrowing()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.NotFound);
        var client = CreateClient(handler, new Uri("https://agenthost.example.com/"));

        var result = await client.DownloadPdfAsync("https://agenthost.example.com/api/blueprints/abc/pdf");

        Assert.Null(result);
    }

    [Fact]
    public async Task DownloadPdfAsync_NullOrEmptyUrl_ReturnsNullWithoutSendingRequest()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK);
        var client = CreateClient(handler, new Uri("https://agenthost.example.com/"));

        var result = await client.DownloadPdfAsync("");

        Assert.Null(result);
        Assert.Null(handler.LastRequest);
    }
}
