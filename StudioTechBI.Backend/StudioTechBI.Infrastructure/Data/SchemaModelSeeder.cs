using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StudioTechBI.Domain.Entities;

namespace StudioTechBI.Infrastructure.Data;

/// <summary>
/// Seeds the reference SchemaModel library: 2 models per industry (Accounting, Finance,
/// Real Estate, Transport, NDIS), each with its expected field list, and one Dashboard
/// Template row per model.
///
/// The Template.BlobPath values below are PLACEHOLDERS — this seeder does not and cannot
/// create real .pbix files or Power BI Service reports. Someone still needs to build each
/// dashboard template in Power BI, publish it to the workspace, and upload the .pbix to the
/// blob path referenced here before a template is actually usable end-to-end.
/// </summary>
public static class SchemaModelSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record FieldSeed(string Name, string DataType, bool Required);

    private sealed record ModelSeed(
        string Name,
        string Industry,
        string Description,
        string TemplateBlobPath,
        FieldSeed[] Fields);

    private static readonly ModelSeed[] Models =
    {
        new(
            "Accounting — General Ledger",
            "Accounting",
            "Journal-entry level transactions posted against a chart of accounts.",
            "templates/accounting/general-ledger/overview.pbix",
            new FieldSeed[]
            {
                new("TransactionId", "string", true),
                new("TransactionDate", "date", true),
                new("AccountCode", "string", true),
                new("AccountName", "string", true),
                new("DebitAmount", "decimal", true),
                new("CreditAmount", "decimal", true),
                new("Currency", "string", false),
                new("CostCentre", "string", false),
                new("Reference", "string", false),
                new("CreatedBy", "string", false),
            }),

        new(
            "Accounting — Accounts Payable/Receivable",
            "Accounting",
            "Vendor and client invoices with payment status, for AP/AR aging and cashflow views.",
            "templates/accounting/ap-ar/overview.pbix",
            new FieldSeed[]
            {
                new("InvoiceNumber", "string", true),
                new("InvoiceDate", "date", true),
                new("DueDate", "date", true),
                new("VendorOrClientName", "string", true),
                new("Amount", "decimal", true),
                new("PaymentStatus", "string", true),
                new("Currency", "string", false),
                new("PaymentDate", "date", false),
                new("TaxAmount", "decimal", false),
                new("PurchaseOrderNumber", "string", false),
            }),

        new(
            "Finance — Budget & Variance",
            "Finance",
            "Actuals vs. budgeted amounts by department and fiscal period, for variance analysis.",
            "templates/finance/budget-variance/overview.pbix",
            new FieldSeed[]
            {
                new("TransactionId", "string", true),
                new("Date", "date", true),
                new("Amount", "decimal", true),
                new("BudgetedAmount", "decimal", true),
                new("DepartmentId", "string", true),
                new("FiscalYear", "string", true),
                new("RevenueTarget", "decimal", false),
                new("CostCentre", "string", false),
                new("Currency", "string", false),
                new("ApprovedBy", "string", false),
            }),

        new(
            "Finance — Revenue & Forecasting",
            "Finance",
            "Recognized revenue by product line and region, with forecast comparison.",
            "templates/finance/revenue-forecasting/overview.pbix",
            new FieldSeed[]
            {
                new("RevenueId", "string", true),
                new("RecognitionDate", "date", true),
                new("Amount", "decimal", true),
                new("ProductLine", "string", true),
                new("Region", "string", true),
                new("ForecastAmount", "decimal", false),
                new("CustomerSegment", "string", false),
                new("Currency", "string", false),
                new("SalesRepId", "string", false),
            }),

        new(
            "Real Estate — Property Portfolio & Leasing",
            "Real Estate",
            "Property and lease records for occupancy, rent roll, and portfolio views.",
            "templates/real-estate/portfolio-leasing/overview.pbix",
            new FieldSeed[]
            {
                new("PropertyId", "string", true),
                new("PropertyAddress", "string", true),
                new("PropertyType", "string", true),
                new("LeaseStartDate", "date", true),
                new("MonthlyRent", "decimal", true),
                new("OccupancyStatus", "string", true),
                new("LeaseEndDate", "date", false),
                new("TenantName", "string", false),
                new("SquareFootage", "decimal", false),
                new("AgentId", "string", false),
            }),

        new(
            "Real Estate — Sales & Valuation",
            "Real Estate",
            "Listing, sale, and valuation records for sales performance and pricing views.",
            "templates/real-estate/sales-valuation/overview.pbix",
            new FieldSeed[]
            {
                new("ListingId", "string", true),
                new("PropertyId", "string", true),
                new("ListPrice", "decimal", true),
                new("SaleDate", "date", true),
                new("SalePrice", "decimal", true),
                new("AgentId", "string", false),
                new("DaysOnMarket", "int", false),
                new("ValuationAmount", "decimal", false),
                new("BuyerName", "string", false),
            }),

        new(
            "Transport — Fleet & Trip Operations",
            "Transport",
            "Per-trip vehicle and driver records for fleet utilisation and trip cost views.",
            "templates/transport/fleet-trip-operations/overview.pbix",
            new FieldSeed[]
            {
                new("TripId", "string", true),
                new("VehicleId", "string", true),
                new("DriverId", "string", true),
                new("DepartureTime", "datetime", true),
                new("ArrivalTime", "datetime", true),
                new("TripStatus", "string", true),
                new("OriginLocation", "string", false),
                new("DestinationLocation", "string", false),
                new("DistanceKm", "decimal", false),
                new("FuelCost", "decimal", false),
            }),

        new(
            "Transport — Freight & Delivery",
            "Transport",
            "Shipment and delivery records for carrier performance and freight cost views.",
            "templates/transport/freight-delivery/overview.pbix",
            new FieldSeed[]
            {
                new("ShipmentId", "string", true),
                new("OrderId", "string", true),
                new("PickupDate", "date", true),
                new("DeliveryDate", "date", true),
                new("DeliveryStatus", "string", true),
                new("CarrierId", "string", false),
                new("WeightKg", "decimal", false),
                new("FreightCost", "decimal", false),
                new("Route", "string", false),
            }),

        new(
            "NDIS — Participant Service Delivery",
            "NDIS",
            "Support delivery records against participant plans, for service and claims views.",
            "templates/ndis/participant-service-delivery/overview.pbix",
            new FieldSeed[]
            {
                new("ParticipantId", "string", true),
                new("SupportItemCode", "string", true),
                new("ServiceDate", "date", true),
                new("SupportCategory", "string", true),
                new("ServiceHours", "decimal", true),
                new("ClaimAmount", "decimal", true),
                new("ProviderId", "string", false),
                new("PlanId", "string", false),
                new("PlanBudgetRemaining", "decimal", false),
                new("Location", "string", false),
            }),

        new(
            "NDIS — Plan Management & Billing",
            "NDIS",
            "Participant plan budgets and spend, for plan management and billing views.",
            "templates/ndis/plan-management-billing/overview.pbix",
            new FieldSeed[]
            {
                new("PlanId", "string", true),
                new("ParticipantId", "string", true),
                new("PlanStartDate", "date", true),
                new("PlanEndDate", "date", true),
                new("BudgetAllocated", "decimal", true),
                new("BudgetSpent", "decimal", true),
                new("FundingCategory", "string", false),
                new("ClaimStatus", "string", false),
                new("InvoiceNumber", "string", false),
                new("ManagementType", "string", false),
            }),
    };

    public static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        var createdAt = DateTime.UtcNow;

        foreach (var seed in Models)
        {
            var existing = await context.SchemaModels
                .FirstOrDefaultAsync(m => m.Name == seed.Name && !m.IsDeleted, cancellationToken);
            if (existing != null)
                continue;

            var model = new SchemaModel
            {
                Id = Guid.NewGuid(),
                Name = seed.Name,
                Industry = seed.Industry,
                Description = seed.Description,
                CreatedAt = createdAt,
            };

            for (var i = 0; i < seed.Fields.Length; i++)
            {
                var f = seed.Fields[i];
                model.Fields.Add(new SchemaModelField
                {
                    Id = Guid.NewGuid(),
                    SchemaModelId = model.Id,
                    FieldName = f.Name,
                    DataType = f.DataType,
                    IsRequired = f.Required,
                    SortOrder = i,
                    CreatedAt = createdAt,
                });
            }

            context.SchemaModels.Add(model);

            var requiredCols = seed.Fields.Where(f => f.Required).Select(f => f.Name).ToList();
            var optionalCols = seed.Fields.Where(f => !f.Required).Select(f => f.Name).ToList();

            context.Templates.Add(new Template
            {
                Id = Guid.NewGuid(),
                TemplateName = $"{seed.Name} — Overview",
                Industry = seed.Industry,
                Version = "1.0",
                ModelId = model.Id,
                // Placeholder — no .pbix has actually been built/published for this yet.
                BlobPath = seed.TemplateBlobPath,
                RequiredColumnsJson = JsonSerializer.Serialize(requiredCols, JsonOptions),
                OptionalColumnsJson = JsonSerializer.Serialize(optionalCols, JsonOptions),
                CreatedDate = createdAt,
                CreatedAt = createdAt,
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
