using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StudioTechBI.Application.DTOs.ReportDesigner;
using StudioTechBI.Application.Models;
using StudioTechBI.Infrastructure.Clients;
using Xunit;

namespace StudioTechBI.Infrastructure.Tests.Clients;

public class ReportDesignerClientTests
{
    private static ReportDesignerClient CreateClient(FakeHttpMessageHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://transformers.example.com/") },
            NullLogger<ReportDesignerClient>.Instance,
            Options.Create(new ReportDesignerOptions()));

    private static GenerateReportModelRequest ValidRequest() => new(
        Schema: new ExtractedSchemaDto(
            Source: "excel",
            FileName: "sales.xlsx",
            Tables:
            [
                new TableSchemaDto("Invoices", null, 100, [new ColumnSchemaDto("InvoiceId", "int", false, null)])
            ],
            SchemaHash: "hash123",
            ExtractedAt: DateTimeOffset.UtcNow),
        PreferredTheme: null);

    private const string ConnectResponseJson = """{ "sessionId": "sess-1" }""";

    private const string DesignsResponseJson = """
        [
          { "templateId": "t1", "name": "Executive Overview", "matchScore": 0.92 },
          { "templateId": "t2", "name": "Sales Detail", "matchScore": 0.71 }
        ]
        """;

    private const string BlueprintWithDataModelJson = """
        {
          "meta": { "title": "Sales Blueprint" },
          "data_model": {
            "fact_tables": [ { "name": "Fact_Sales", "grain": "one row per invoice", "source": "excel" } ],
            "dimension_tables": [ { "name": "Dim_Customer" }, { "name": "Dim_Date" } ],
            "relationships": [
              { "from": "Fact_Sales[CustomerId]", "to": "Dim_Customer[CustomerId]", "cardinality": "Many:One" }
            ]
          }
        }
        """;

    private const string BlueprintWithoutDataModelJson = """{ "meta": { "title": "No Model" } }""";

    private static HttpResponseMessage RouteByPath(
        HttpRequestMessage request, string designsBody, HttpStatusCode designsStatus, string generateBody)
    {
        var path = request.RequestUri!.AbsolutePath;

        if (path.EndsWith("/connect"))
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ConnectResponseJson) };

        if (path.EndsWith("/designs"))
            return new HttpResponseMessage(designsStatus) { Content = new StringContent(designsBody) };

        if (path.EndsWith("/generate"))
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(generateBody) };

        throw new InvalidOperationException($"Unexpected request path: {path}");
    }

    [Fact]
    public async Task GenerateReportModelAsync_SuccessfulFlow_PopulatesStarSchemaAndTemplates()
    {
        var handler = new FakeHttpMessageHandler(req =>
            RouteByPath(req, DesignsResponseJson, HttpStatusCode.OK, BlueprintWithDataModelJson));
        var client = CreateClient(handler);

        var response = await client.GenerateReportModelAsync(ValidRequest(), correlationId: "corr-1");

        Assert.NotNull(response.StarSchema);
        Assert.Equal("Fact_Sales", response.StarSchema!.FactTable);
        Assert.Equal(["Dim_Customer", "Dim_Date"], response.StarSchema.DimensionTables);
        Assert.Single(response.StarSchema.Relationships);
        Assert.Equal("Fact_Sales", response.StarSchema.Relationships[0].FromTable);
        Assert.Equal("CustomerId", response.StarSchema.Relationships[0].FromColumn);
        Assert.Equal("Dim_Customer", response.StarSchema.Relationships[0].ToTable);
        Assert.Equal("CustomerId", response.StarSchema.Relationships[0].ToColumn);

        Assert.NotNull(response.Templates);
        Assert.Equal(2, response.Templates!.Count);
        Assert.Equal("t1", response.Templates[0].ThemeId);
        Assert.Equal("Executive Overview", response.Templates[0].ThemeName);
        Assert.Equal(0.92, response.Templates[0].Score);
    }

    [Fact]
    public async Task GenerateReportModelAsync_DesignsCallFails_StillSucceedsWithNullTemplates()
    {
        var handler = new FakeHttpMessageHandler(req =>
            RouteByPath(req, "{}", HttpStatusCode.InternalServerError, BlueprintWithDataModelJson));
        var client = CreateClient(handler);

        var response = await client.GenerateReportModelAsync(ValidRequest(), correlationId: "corr-1");

        Assert.Null(response.Templates);
        Assert.NotNull(response.StarSchema);
    }

    [Fact]
    public async Task GenerateReportModelAsync_BlueprintHasNoDataModel_StarSchemaIsNull()
    {
        var handler = new FakeHttpMessageHandler(req =>
            RouteByPath(req, DesignsResponseJson, HttpStatusCode.OK, BlueprintWithoutDataModelJson));
        var client = CreateClient(handler);

        var response = await client.GenerateReportModelAsync(ValidRequest(), correlationId: "corr-1");

        Assert.Null(response.StarSchema);
        Assert.NotNull(response.Templates);
    }
}
