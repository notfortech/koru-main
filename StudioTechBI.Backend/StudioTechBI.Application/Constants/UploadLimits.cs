namespace StudioTechBI.Application.Constants;

/// <summary>
/// Single source of truth for the compile-time upload-size ceiling used by every
/// <c>[RequestSizeLimit]</c> attribute across the API's file-accepting controllers.
/// ASP.NET Core requires attribute arguments to be compile-time constants, so this value can't
/// be config-driven directly -- <see cref="Options.UploadLimitsOptions"/> (bound from
/// configuration) is the actually-configurable limit each controller checks against at runtime;
/// it can tighten this ceiling via config but can never exceed it without a recompile.
/// </summary>
public static class UploadLimits
{
    public const long MaxUploadBytes = 50L * 1024L * 1024L; // 50 MB
}
