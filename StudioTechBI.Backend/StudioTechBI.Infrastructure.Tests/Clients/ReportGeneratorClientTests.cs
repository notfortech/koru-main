using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using StudioTechBI.Infrastructure.Clients;
using Xunit;

namespace StudioTechBI.Infrastructure.Tests.Clients;

public class ReportGeneratorClientTests
{
    private static ReportGeneratorClient CreateClient(FakeHttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://reportagent.example.com/") },
            NullLogger<ReportGeneratorClient>.Instance);

    private const string TemplatesResponseJson = """
        [
          { "id": "generic-overview", "name": "Generic Overview", "industry": null, "description": "desc", "requires": { "minNumeric": 1 } }
        ]
        """;

    private const string GenerateResponseJson = """
        {
          "templateId": "generic-overview",
          "templateName": "Generic Overview",
          "primaryTable": "sales",
          "kpis": [ { "label": "Sum of Amount", "value": 1855.25, "column": "Amount", "aggregation": "sum" } ],
          "charts": [
            { "type": "line", "title": "Amount Trend", "x": ["Jan 2024"], "categories": null, "series": [ { "name": "Amount", "values": [120.5] } ] }
          ],
          "slicers": [ { "column": "Region", "values": ["North", "South"] } ],
          "appliedFilters": { "Region": "North" },
          "warnings": []
        }
        """;

    [Fact]
    public async Task ListTemplatesAsync_Success_ReturnsTemplates()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, TemplatesResponseJson);
        var client = CreateClient(handler);

        var templates = await client.ListTemplatesAsync(correlationId: "corr-1");

        Assert.Single(templates);
        Assert.Equal("generic-overview", templates[0].Id);
        Assert.Equal("Generic Overview", templates[0].Name);
        Assert.Equal(1, templates[0].Requires?["minNumeric"]);
    }

    [Fact]
    public async Task ListTemplatesAsync_Failure_ThrowsHttpRequestException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "{}");
        var client = CreateClient(handler);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.ListTemplatesAsync(correlationId: "corr-1"));
    }

    [Fact]
    public async Task GenerateReportAsync_Success_ParsesResult()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, GenerateResponseJson);
        var client = CreateClient(handler);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("OrderId,Amount\n1,120.5\n"));
        var result = await client.GenerateReportAsync(stream, "sales.csv", templateId: null, filtersJson: null, htmlTemplateId: null, correlationId: "corr-1");

        Assert.Equal("generic-overview", result.TemplateId);
        Assert.Single(result.Kpis);
        Assert.Equal(1855.25, result.Kpis[0].Value);
        Assert.Single(result.Charts);
        Assert.Equal("Amount Trend", result.Charts[0].Title);
        Assert.Equal(["Jan 2024"], result.Charts[0].X);
        Assert.Single(result.Slicers);
        Assert.Equal("Region", result.Slicers[0].Column);
        Assert.Equal("North", result.AppliedFilters["Region"]);
    }

    [Fact]
    public async Task GenerateReportAsync_SendsMultipartWithFileAndTemplateId()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, GenerateResponseJson);
        var client = CreateClient(handler);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("OrderId,Amount\n1,120.5\n"));
        await client.GenerateReportAsync(stream, "sales.csv", templateId: "generic-overview", filtersJson: null, htmlTemplateId: null, correlationId: "corr-1");

        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("sales.csv", handler.LastRequestBody);
        Assert.Contains("name=\"templateId\"", handler.LastRequestBody);
        Assert.Contains("generic-overview", handler.LastRequestBody);
    }

    [Fact]
    public async Task GenerateReportAsync_SendsFiltersAsMultipartField()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, GenerateResponseJson);
        var client = CreateClient(handler);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("OrderId,Amount\n1,120.5\n"));
        await client.GenerateReportAsync(
            stream, "sales.csv", templateId: null, filtersJson: """{"Region":"North"}""", htmlTemplateId: null, correlationId: "corr-1");

        Assert.NotNull(handler.LastRequestBody);
        Assert.Contains("name=\"filters\"", handler.LastRequestBody);
        Assert.Contains("""{"Region":"North"}""", handler.LastRequestBody);
    }

    [Fact]
    public async Task GenerateReportAsync_Failure_ThrowsHttpRequestException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.BadGateway, """{ "error": "engine failed" }""");
        var client = CreateClient(handler);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("OrderId,Amount\n1,120.5\n"));
        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.GenerateReportAsync(stream, "sales.csv", templateId: null, filtersJson: null, htmlTemplateId: null, correlationId: "corr-1"));
    }
}
