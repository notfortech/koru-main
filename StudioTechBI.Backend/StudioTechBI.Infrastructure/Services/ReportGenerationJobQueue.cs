using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StudioTechBI.Application.Interfaces;

namespace StudioTechBI.Infrastructure.Services;

/// <summary>
/// Azure Storage Queue-backed implementation of IReportGenerationJobQueue — see that interface's
/// remarks for why this isn't another System.Threading.Channels queue. Reuses the same
/// AzureBlob:ConnectionString config key BlobSasUriProvider already reads (Storage Queues live in
/// the same storage account as blobs; same connection string, sibling SDK, no new config key).
/// Message body is just the job id as a string — the row itself (ReportGenerationJob) carries all
/// real state, so the queue only ever needs to say "look at this row."
/// </summary>
public class ReportGenerationJobQueue : IReportGenerationJobQueue
{
    private const string QueueName = "report-generation-jobs";
    private readonly QueueClient? _queueClient;
    private readonly ILogger<ReportGenerationJobQueue> _logger;

    public ReportGenerationJobQueue(IConfiguration configuration, ILogger<ReportGenerationJobQueue> logger)
    {
        _logger = logger;
        var connectionString = configuration["AzureBlob:ConnectionString"];
        if (string.IsNullOrEmpty(connectionString))
        {
            _logger.LogWarning("Azure Blob connection string not configured. ReportGenerationJobQueue will no-op.");
            _queueClient = null;
            return;
        }

        _queueClient = new QueueClient(connectionString, QueueName);
        try
        {
            // Best-effort, synchronous by necessity (constructors can't be async) -- a transient
            // failure here must never crash DI resolution/app startup. If the queue genuinely
            // doesn't exist and this failed, EnqueueAsync/ReceiveAsync will surface a clear error
            // on first real use instead.
            _queueClient.CreateIfNotExists();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ReportGenerationJobQueue: failed to ensure queue '{QueueName}' exists.", QueueName);
        }
    }

    public async Task EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        if (_queueClient is null)
        {
            _logger.LogWarning("ReportGenerationJobQueue.EnqueueNoOp JobId={JobId} — queue not configured.", jobId);
            return;
        }

        await _queueClient.SendMessageAsync(jobId.ToString("N"), cancellationToken);
    }

    public async Task<IReadOnlyList<ReportGenerationQueueMessage>> ReceiveAsync(
        int maxMessages, TimeSpan visibilityTimeout, CancellationToken cancellationToken = default)
    {
        if (_queueClient is null)
            return Array.Empty<ReportGenerationQueueMessage>();

        var response = await _queueClient.ReceiveMessagesAsync(maxMessages, visibilityTimeout, cancellationToken);
        var result = new List<ReportGenerationQueueMessage>(response.Value.Length);
        foreach (var m in response.Value)
        {
            if (!Guid.TryParseExact(m.MessageText, "N", out var jobId))
            {
                // A malformed message (shouldn't happen -- nothing else writes to this queue) is
                // deleted rather than left to redeliver forever and jam the queue.
                _logger.LogWarning(
                    "ReportGenerationJobQueue.MalformedMessage MessageId={MessageId} Text={Text} — deleting.",
                    m.MessageId, m.MessageText);
                await _queueClient.DeleteMessageAsync(m.MessageId, m.PopReceipt, cancellationToken);
                continue;
            }

            result.Add(new ReportGenerationQueueMessage(jobId, m.MessageId, m.PopReceipt));
        }

        return result;
    }

    public async Task DeleteAsync(ReportGenerationQueueMessage message, CancellationToken cancellationToken = default)
    {
        if (_queueClient is null)
            return;

        await _queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken);
    }

    public async Task<ReportGenerationQueueMessage> RenewVisibilityAsync(
        ReportGenerationQueueMessage message, TimeSpan visibilityTimeout, CancellationToken cancellationToken = default)
    {
        if (_queueClient is null)
            return message;

        var updated = await _queueClient.UpdateMessageAsync(
            message.MessageId, message.PopReceipt, visibilityTimeout: visibilityTimeout, cancellationToken: cancellationToken);
        return message with { PopReceipt = updated.Value.PopReceipt };
    }
}
