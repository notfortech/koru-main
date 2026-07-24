using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StudioTechBI.Application.DTOs.ReportGenerator;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.Services;

/// <summary>
/// Renders a Report Generator result as a branded PDF using QuestPDF — same library/pattern as
/// stbi-agenthost's BlueprintPdfService, kept local to koru-main since the report data already
/// lives here and there's no persisted "request" record to look a saved file up by later.
/// Charts render as data tables (category/series rows), not chart images — an honest static
/// representation rather than a fake rasterized chart.
/// </summary>
public sealed class ReportGeneratorPdfService : IReportGeneratorPdfService
{
    private static readonly object LicenseInit = new();
    private static bool _licenseSet;

    public ReportGeneratorPdfService()
    {
        EnsureLicense();
    }

    public byte[] Generate(GeneratedReportDto report, string? aiSummary = null, List<string>? insights = null)
    {
        var generatedAt = DateTimeOffset.UtcNow;

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(c => RenderHeader(c, report, generatedAt));
                page.Content().Element(c => RenderContent(c, report, aiSummary, insights));
                page.Footer().Element(RenderFooter);
            });
        }).GeneratePdf();
    }

    private static void RenderHeader(IContainer container, GeneratedReportDto report, DateTimeOffset generated)
    {
        container
            .BorderBottom(1).BorderColor(Colors.Blue.Darken3)
            .PaddingBottom(8)
            .Column(col =>
            {
                col.Item()
                    .Text(string.IsNullOrWhiteSpace(report.TemplateName) ? "Report" : report.TemplateName!)
                    .FontSize(18).Bold().FontColor(Colors.Blue.Darken3);
                col.Item()
                    .Text($"Generated {generated:yyyy-MM-dd HH:mm} UTC" +
                          (string.IsNullOrWhiteSpace(report.PrimaryTable) ? "" : $" · from {report.PrimaryTable}"))
                    .FontSize(8).FontColor(Colors.Grey.Darken1);

                if (report.AppliedFilters.Count > 0)
                {
                    var filterText = string.Join(", ", report.AppliedFilters.Select(f => $"{f.Key} = {f.Value}"));
                    col.Item().PaddingTop(2).Text($"Filtered by: {filterText}").FontSize(8).Italic();
                }
            });
    }

    private static void RenderContent(IContainer container, GeneratedReportDto report, string? aiSummary, List<string>? insights)
    {
        container.PaddingTop(12).Column(col =>
        {
            col.Spacing(14);

            if (!string.IsNullOrWhiteSpace(aiSummary) || (insights?.Count ?? 0) > 0)
            {
                col.Item().Element(c => RenderAiSummary(c, aiSummary, insights));
            }

            if (report.Kpis.Count > 0)
            {
                col.Item().Element(c => RenderKpis(c, report.Kpis));
            }

            foreach (var chart in report.Charts)
            {
                col.Item().Element(c => RenderChart(c, chart));
            }

            if (report.Warnings.Count > 0)
            {
                col.Item().Element(c => RenderWarnings(c, report.Warnings));
            }
        });
    }

    private static void RenderAiSummary(IContainer container, string? summary, List<string>? insights)
    {
        container
            .Background(Colors.Blue.Lighten5)
            .Padding(10)
            .Column(col =>
            {
                col.Spacing(4);
                col.Item().Text("AI Summary").FontSize(11).Bold().FontColor(Colors.Blue.Darken2);
                if (!string.IsNullOrWhiteSpace(summary))
                    col.Item().Text(summary).FontSize(9);
                if (insights is { Count: > 0 })
                {
                    foreach (var insight in insights)
                        col.Item().Text($"• {insight}").FontSize(9);
                }
            });
    }

    private static void RenderKpis(IContainer container, List<ReportKpiDto> kpis)
    {
        container.Column(col =>
        {
            col.Item().Text("Key Metrics").FontSize(12).Bold().FontColor(Colors.Blue.Darken2);
            col.Item().PaddingTop(4).Grid(grid =>
            {
                grid.Columns(3);
                grid.Spacing(8);
                foreach (var kpi in kpis)
                {
                    grid.Item().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(kc =>
                    {
                        kc.Item().Text(kpi.Label).FontSize(8).FontColor(Colors.Grey.Darken1);
                        kc.Item().Text(FormatValue(kpi.Value, kpi.Unit)).FontSize(14).Bold();
                        if (kpi.Change is not null)
                        {
                            var up = kpi.Change >= 0;
                            kc.Item().Text($"{(up ? "▲" : "▼")} {Math.Abs(kpi.Change.Value):0.0}%")
                                .FontSize(8).Bold()
                                .FontColor(up ? Colors.Green.Darken1 : Colors.Red.Darken1);
                        }
                    });
                }
            });
        });
    }

    private static void RenderChart(IContainer container, ReportChartDto chart)
    {
        container.Column(col =>
        {
            col.Item().Text(chart.Title).FontSize(11).Bold().FontColor(Colors.Blue.Darken2);

            var labels = chart.Categories ?? chart.X ?? new List<string>();
            if (labels.Count == 0 || chart.Series.Count == 0)
            {
                col.Item().Text("No data to display.").FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                return;
            }

            col.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    foreach (var _ in chart.Series) columns.RelativeColumn(1);
                });

                table.Header(header =>
                {
                    HeaderCell(header, chart.Series.Count == 1 ? "Category" : "");
                    foreach (var series in chart.Series)
                        HeaderCell(header, series.Name);
                });

                for (var i = 0; i < labels.Count; i++)
                {
                    TableCell(table, labels[i]);
                    foreach (var series in chart.Series)
                    {
                        var value = i < series.Values.Count ? series.Values[i] : 0.0;
                        var text = FormatValue(value, series.Unit);
                        if (series.PercentOfTotal is { } pct && i < pct.Count)
                            text += $" ({pct[i]:0.0}%)";
                        TableCell(table, text);
                    }
                }
            });
        });
    }

    private static void RenderWarnings(IContainer container, List<string> warnings)
    {
        container.Column(col =>
        {
            col.Item().Text("Notes").FontSize(10).Bold().FontColor(Colors.Grey.Darken2);
            foreach (var warning in warnings)
                col.Item().Text($"• {warning}").FontSize(8).FontColor(Colors.Grey.Darken1);
        });
    }

    private static void RenderFooter(IContainer container) =>
        container.AlignCenter().Text(text =>
        {
            text.DefaultTextStyle(x => x.FontSize(7).FontColor(Colors.Grey.Darken1));
            text.Span("Generated by StudioTechBI — Report Generator");
        });

    private static void HeaderCell(TableCellDescriptor table, string text) =>
        table.Cell().Background(Colors.Blue.Lighten4).Padding(3).Text(text).Bold().FontSize(8);

    private static void TableCell(TableDescriptor table, string text) =>
        table.Cell()
            .BorderBottom(1).BorderColor(Colors.Grey.Lighten3)
            .Padding(3)
            .Text(text).FontSize(8);

    private static string FormatValue(double value, string? unit) => unit switch
    {
        "currency" => value.ToString("$#,##0.00"),
        "percent" => value.ToString("0.0") + "%",
        "days" => value.ToString("0") + "d",
        _ => value.ToString("#,##0.##")
    };

    private static void EnsureLicense()
    {
        if (_licenseSet) return;
        lock (LicenseInit)
        {
            if (_licenseSet) return;
            QuestPDF.Settings.License = LicenseType.Community;
            _licenseSet = true;
        }
    }
}
