namespace StudioTechBI.Application.Interfaces;

/// <summary>Writes a blended (real + mocked) dataset out as a downloadable .xlsx workbook —
/// one worksheet per table, one column of headers each. Purely an I/O concern (ClosedXML),
/// implemented in the Infrastructure layer.</summary>
public interface IWorkbookWriter
{
    Task<Stream> WriteAsync(List<BlendedTable> tables, CancellationToken cancellationToken = default);
}
