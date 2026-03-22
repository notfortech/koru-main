using System.Diagnostics;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Application.Services;

public class ReportWorkerService
{
    private readonly IClientService _clientService;
    private readonly IDatasetRefreshLogWriter _refreshLogWriter;
    private readonly IReportingProcessingJobWriter _processingJobWriter;
    private readonly IPowerBiAssetQuery _powerBiAssetQuery;

    public ReportWorkerService(
        IClientService clientService,
        IDatasetRefreshLogWriter refreshLogWriter,
        IReportingProcessingJobWriter processingJobWriter,
        IPowerBiAssetQuery powerBiAssetQuery)
    {
        _clientService = clientService;
        _refreshLogWriter = refreshLogWriter;
        _processingJobWriter = processingJobWriter;
        _powerBiAssetQuery = powerBiAssetQuery;
    }

    private static string GetWorkingDirectory()
    {
        // From API bin/Debug/net8.0 we need to reach Backend/StudioTechBI.Workers/Python (4 levels up to Backend).
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var backendDir = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ".."));
        return Path.GetFullPath(Path.Combine(backendDir, "StudioTechBI.Workers", "Python"));
    }

    public async Task Generate(string clientId, string periodType, string period)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "python",
            Arguments = $"generate_report.py {clientId} {periodType} {period}",
            WorkingDirectory = GetWorkingDirectory(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        var process = Process.Start(psi);
        if (process == null)
            throw new InvalidOperationException("Failed to start Python process.");
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await Task.Delay(500);
        process.WaitForExit();
        if (process.ExitCode != 0)
            throw new Exception($"Python failed: {error}");
    }

    /// <summary>
    /// If the master file has data (optionally for the given period), refreshes the Power BI dataset.
    /// Master xlsx path is static for now. DatasetRefreshed is true when a refresh was run.
    /// Logs each attempt to reporting.DatasetRefreshLogs.
    /// </summary>
    public async Task<(bool Success, string Message, string Log, bool DatasetRefreshed)> RefreshReportIfUpdated(string clientId, string? period, string reportType = "monthly")
    {
        var startTime = DateTime.UtcNow;
        Guid? clientGuid = null;
        string? datasetName = null;
        try
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                await _refreshLogWriter.LogAsync(null, null, "Failed", startTime, DateTime.UtcNow, "clientId is required.", default);
                return (false, "clientId is required.", "", false);
            }
            var workingDir = GetWorkingDirectory();
            if (!Directory.Exists(workingDir))
            {
                await _refreshLogWriter.LogAsync(null, null, "Failed", startTime, DateTime.UtcNow, $"Python working directory not found: {workingDir}.", default);
                return (false, $"Python working directory not found: {workingDir}. Check that StudioTechBI.Workers\\Python exists.", "", false);
            }

            var clientDto = await _clientService.GetByClientCodeAsync(clientId.Trim(), default);
            clientGuid = clientDto?.ClientId;

            var asset = await _powerBiAssetQuery.GetActiveAssetByClientCodeAsync(
                clientId.Trim(),
                reportType ?? "monthly",
                default);

            if (asset == null || string.IsNullOrWhiteSpace(asset.WorkspaceId) || string.IsNullOrWhiteSpace(asset.DatasetId))
            {
                await _refreshLogWriter.LogAsync(
                    clientGuid,
                    clientId.Trim(),
                    "Failed",
                    startTime,
                    DateTime.UtcNow,
                    $"No Power BI asset configured for client '{clientId}' (reportType='{reportType}').",
                    default);
                return (false, "No Power BI asset configured for this client.", "", false);
            }

            datasetName = asset.DatasetId.Trim();
            var workspace = asset.WorkspaceId.Trim();
            var dataset = asset.DatasetId.Trim();

            var args = $"refresh_report_if_updated.py \"{clientId}\" \"{period ?? ""}\" \"{workspace}\" \"{dataset}\"";
            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = args,
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var process = Process.Start(psi);
            if (process == null)
            {
                await _refreshLogWriter.LogAsync(clientGuid, datasetName, "Failed", startTime, DateTime.UtcNow, "Failed to start Python (ensure 'python' is on PATH).", default);
                return (false, "Failed to start Python (ensure 'python' is on PATH).", "", false);
            }
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await process.WaitForExitAsync(cts.Token);
            var endTime = DateTime.UtcNow;
            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            var log = output + (string.IsNullOrEmpty(error) ? "" : "\n" + error);

            if (process.ExitCode != 0)
            {
                var msg = string.IsNullOrWhiteSpace(error) ? "Refresh script failed." : error.Trim();
                await _refreshLogWriter.LogAsync(clientGuid, datasetName, "Failed", startTime, endTime, msg, default);
                return (false, msg, log, false);
            }
            if (output.Contains("No master file"))
            {
                await _refreshLogWriter.LogAsync(clientGuid, datasetName, "NoMasterFile", startTime, endTime, null, default);
                return (true, "No master file found.", log, false);
            }
            if (output.Contains("No data for period"))
            {
                var msg = output.Trim().Split('\n')[0];
                await _refreshLogWriter.LogAsync(clientGuid, datasetName, "NoDataForPeriod", startTime, endTime, null, default);
                return (true, msg, log, false);
            }
            if (output.Contains("No updates to master"))
            {
                await _refreshLogWriter.LogAsync(clientGuid, datasetName, "NoUpdates", startTime, endTime, null, default);
                return (true, "No updates to master file; report is already up to date.", log, false);
            }
            if (log.Contains("POWERBI_RATE_LIMIT") || (log.Contains("429") && log.Contains("Retry")))
            {
                await _refreshLogWriter.LogAsync(clientGuid, datasetName, "RateLimit", startTime, endTime, "Power BI rate limit.", default);
                return (true, "Power BI rate limit. Please retry in 2 minutes.", log, false);
            }
            await _refreshLogWriter.LogAsync(clientGuid, datasetName, "Success", startTime, endTime, null, default);
            return (true, "Dataset refreshed; report will update in the portal.", log, true);
        }
        catch (Exception ex)
        {
            await _refreshLogWriter.LogAsync(clientGuid, datasetName, "Failed", startTime, DateTime.UtcNow, ex.Message, default);
            return (false, ex.Message, ex.ToString(), false);
        }
    }

    /// <summary>
    /// Runs the upload processing pipeline for a client and logs to reporting.ProcessingJobs.
    /// </summary>
    public async Task<(bool Success, string Message, string Log)> ProcessUploadsAsync(string clientId)
    {
        var jobId = Guid.NewGuid();
        Guid? clientGuid = null;
        try
        {
            var clientDto = await _clientService.GetByClientCodeAsync(clientId?.Trim() ?? "", default);
            clientGuid = clientDto?.ClientId;

            await _processingJobWriter.StartAsync(jobId, clientGuid, null, null, default);

            var workingDir = GetWorkingDirectory();
            if (!Directory.Exists(workingDir))
            {
                await _processingJobWriter.UpdateAsync(jobId, "Failed", null, $"Working directory not found: {workingDir}", DateTime.UtcNow, default);
                return (false, $"Working directory not found: {workingDir}", "");
            }

            var psi = new ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"worker_orchestrator.py \"{clientId}\"",
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            var process = Process.Start(psi);
            if (process == null)
            {
                await _processingJobWriter.UpdateAsync(jobId, "Failed", null, "Failed to start Python (ensure 'python' is on PATH).", DateTime.UtcNow, default);
                return (false, "Failed to start Python (ensure 'python' is on PATH).", "");
            }

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50000));
            await process.WaitForExitAsync(cts.Token);
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            var log = output + (string.IsNullOrEmpty(error) ? "" : "\n" + error);

            if (!process.HasExited)
            {
                process.Kill();
                await _processingJobWriter.UpdateAsync(jobId, "Failed", null, "Processing timeout.", DateTime.UtcNow, default);
                return (false, "Processing timeout. Please try again.", log);
            }

            if (process.ExitCode != 0)
            {
                var msg = string.IsNullOrWhiteSpace(error) ? "Script failed." : error.Trim();
                await _processingJobWriter.UpdateAsync(jobId, "Failed", null, msg, DateTime.UtcNow, default);
                return (false, msg, log);
            }

            if (output.Contains("No new files found"))
            {
                await _processingJobWriter.UpdateAsync(jobId, "Completed", "NoNewFiles", null, DateTime.UtcNow, default);
                return (true, "No new uploaded files to process", log);
            }

            await _processingJobWriter.UpdateAsync(jobId, "Completed", "RefreshDataset", null, DateTime.UtcNow, default);
            return (true, "Report updated successfully", log);
        }
        catch (Exception ex)
        {
            await _processingJobWriter.UpdateAsync(jobId, "Failed", null, ex.Message, DateTime.UtcNow, default);
            return (false, ex.Message, ex.ToString());
        }
    }
}