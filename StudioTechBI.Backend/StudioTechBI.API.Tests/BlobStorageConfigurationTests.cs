using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using StudioTechBI.Infrastructure.Services;
using Xunit;

namespace StudioTechBI.API.Tests;

/// <summary>Regression tests for Priority 2 of the staging-readiness plan: every blob
/// container/queue name a koru-main storage service uses must be configurable, defaulting to
/// today's real names so this change is a no-op for any environment that doesn't set the new
/// BlobStorage:* keys. Uses the well-known Azurite connection string, which the Azure SDK parses
/// successfully without needing a running emulator -- these tests only construct clients and read
/// their .Name property, they never issue a network call.</summary>
public class BlobStorageConfigurationTests
{
    private const string FakeConnectionString = "UseDevelopmentStorage=true";

    private static IConfiguration BuildConfiguration(string? key = null, string? value = null)
    {
        var data = new Dictionary<string, string?> { ["AzureBlob:ConnectionString"] = FakeConnectionString };
        if (key != null)
        {
            data[key] = value;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(data).Build();
    }

    private static string? GetContainerOrQueueName(object service, string fieldName)
    {
        var field = service.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        var client = field!.GetValue(service);
        Assert.NotNull(client);
        var nameProperty = client!.GetType().GetProperty("Name");
        Assert.NotNull(nameProperty);
        return (string?)nameProperty!.GetValue(client);
    }

    [Fact]
    public void BlobStorageService_DefaultsToClientsContainer()
    {
        var service = new BlobStorageService(BuildConfiguration(), NullLogger<BlobStorageService>.Instance);
        Assert.Equal("clients", GetContainerOrQueueName(service, "_containerClient"));
    }

    [Fact]
    public void BlobStorageService_UsesConfiguredContainerOverride()
    {
        var service = new BlobStorageService(
            BuildConfiguration("BlobStorage:ClientsContainer", "clients-staging"),
            NullLogger<BlobStorageService>.Instance);
        Assert.Equal("clients-staging", GetContainerOrQueueName(service, "_containerClient"));
    }

    [Fact]
    public void BlobSasUriProvider_DefaultsToClientsContainer()
    {
        var service = new BlobSasUriProvider(BuildConfiguration(), NullLogger<BlobSasUriProvider>.Instance);
        Assert.Equal("clients", GetContainerOrQueueName(service, "_containerClient"));
    }

    [Fact]
    public void BlobSasUriProvider_UsesConfiguredContainerOverride()
    {
        var service = new BlobSasUriProvider(
            BuildConfiguration("BlobStorage:ClientsContainer", "clients-staging"),
            NullLogger<BlobSasUriProvider>.Instance);
        Assert.Equal("clients-staging", GetContainerOrQueueName(service, "_containerClient"));
    }

    [Fact]
    public void BlueprintStorageService_DefaultsToBlueprintsContainer()
    {
        var service = new BlueprintStorageService(BuildConfiguration(), NullLogger<BlueprintStorageService>.Instance);
        Assert.Equal("blueprints", GetContainerOrQueueName(service, "_containerClient"));
    }

    [Fact]
    public void BlueprintStorageService_UsesConfiguredContainerOverride()
    {
        var service = new BlueprintStorageService(
            BuildConfiguration("BlobStorage:BlueprintsContainer", "blueprints-staging"),
            NullLogger<BlueprintStorageService>.Instance);
        Assert.Equal("blueprints-staging", GetContainerOrQueueName(service, "_containerClient"));
    }

    [Fact]
    public void ReportValidationScratchStorageService_DefaultsToReportValidationScratchContainer()
    {
        var service = new ReportValidationScratchStorageService(
            BuildConfiguration(), NullLogger<ReportValidationScratchStorageService>.Instance);
        Assert.Equal("report-validation-scratch", GetContainerOrQueueName(service, "_containerClient"));
    }

    [Fact]
    public void ReportValidationScratchStorageService_UsesConfiguredContainerOverride()
    {
        var service = new ReportValidationScratchStorageService(
            BuildConfiguration("BlobStorage:ReportValidationScratchContainer", "report-validation-scratch-staging"),
            NullLogger<ReportValidationScratchStorageService>.Instance);
        Assert.Equal("report-validation-scratch-staging", GetContainerOrQueueName(service, "_containerClient"));
    }

    [Fact]
    public void ReportTemplateAssetService_DefaultsToReportTemplatesContainer()
    {
        var service = new ReportTemplateAssetService(BuildConfiguration(), NullLogger<ReportTemplateAssetService>.Instance);
        Assert.Equal("report-templates", GetContainerOrQueueName(service, "_containerClient"));
    }

    [Fact]
    public void ReportTemplateAssetService_UsesConfiguredContainerOverride()
    {
        var service = new ReportTemplateAssetService(
            BuildConfiguration("BlobStorage:ReportTemplatesContainer", "report-templates-staging"),
            NullLogger<ReportTemplateAssetService>.Instance);
        Assert.Equal("report-templates-staging", GetContainerOrQueueName(service, "_containerClient"));
    }

    [Fact]
    public void ReportGenerationJobQueue_DefaultsToReportGenerationJobsQueue()
    {
        var service = new ReportGenerationJobQueue(BuildConfiguration(), NullLogger<ReportGenerationJobQueue>.Instance);
        Assert.Equal("report-generation-jobs", GetContainerOrQueueName(service, "_queueClient"));
    }

    [Fact]
    public void ReportGenerationJobQueue_UsesConfiguredQueueOverride()
    {
        var service = new ReportGenerationJobQueue(
            BuildConfiguration("BlobStorage:ReportGenerationJobsQueue", "report-generation-jobs-staging"),
            NullLogger<ReportGenerationJobQueue>.Instance);
        Assert.Equal("report-generation-jobs-staging", GetContainerOrQueueName(service, "_queueClient"));
    }
}
