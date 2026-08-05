namespace StudioTechBI.Application.Interfaces;

/// <summary>
/// Durable queue for large-file Report Generator jobs, backed by Azure Storage Queue rather than
/// an in-process System.Threading.Channels queue (the pattern IBlueprintGenerationQueue/
/// IReportValidationQueue already use). Deliberately not built on that same pattern: work sitting
/// in an in-process channel is lost on an app restart, and koru-main's deploy workflow hard-
/// restarts the single App Service instance on every deploy — not acceptable for a feature whose
/// entire point is handling processing that might take minutes. Azure Storage Queue gives
/// at-least-once delivery across restarts using the same storage account/connection-string this
/// app already depends on for blob storage (Azure.Storage.Queues is a sibling SDK to
/// Azure.Storage.Blobs) — no new service to provision.
///
/// Because delivery is at-least-once, a redelivered message must never be processed twice — the
/// consumer (ReportGenerationJobBackgroundService) claims a job idempotently via a conditional
/// Pending-&gt;Processing update on the ReportGenerationJob row itself before doing any real work,
/// using the job id carried in the message as the correlation key.
/// </summary>
public interface IReportGenerationJobQueue
{
    Task EnqueueAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Receives up to <paramref name="maxMessages"/> messages, each invisible to other
    /// receivers for <paramref name="visibilityTimeout"/> until deleted or the timeout lapses (at
    /// which point it becomes visible again for another receiver — the redelivery case callers
    /// must be idempotent against).</summary>
    Task<IReadOnlyList<ReportGenerationQueueMessage>> ReceiveAsync(
        int maxMessages, TimeSpan visibilityTimeout, CancellationToken cancellationToken = default);

    /// <summary>Removes a message once its job has been fully processed (or has permanently
    /// failed and shouldn't be retried) — must be called with the same PopReceipt returned by the
    /// receive that handed out this message, or the delete is rejected as stale.</summary>
    Task DeleteAsync(ReportGenerationQueueMessage message, CancellationToken cancellationToken = default);

    /// <summary>Extends a message's invisibility window — used by the background worker for a job
    /// whose processing is taking longer than the original visibility timeout, so another receiver
    /// doesn't pick up and double-process it mid-flight. Returns the renewed message: Azure issues
    /// a new PopReceipt on every visibility update, so callers must use the returned message (not
    /// the one passed in) for any subsequent delete/renew call, or that call will be rejected as
    /// stale.</summary>
    Task<ReportGenerationQueueMessage> RenewVisibilityAsync(
        ReportGenerationQueueMessage message, TimeSpan visibilityTimeout, CancellationToken cancellationToken = default);
}

/// <summary>One received queue message. MessageId+PopReceipt together are required to
/// delete/renew this specific delivery (a redelivered message gets a new PopReceipt each time).</summary>
public sealed record ReportGenerationQueueMessage(Guid JobId, string MessageId, string PopReceipt);
