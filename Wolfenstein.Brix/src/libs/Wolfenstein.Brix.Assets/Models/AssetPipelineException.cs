using System;

namespace Wolfenstein.Brix.Assets.Models;

/// <summary>
/// Thrown when a stage of the asset-installation pipeline fails; the
/// <see cref="Stage"/> tells the caller which wording to show the user
/// (download, verification or extraction).
/// </summary>
public class AssetPipelineException : Exception
{
    /// <summary>The pipeline stage that failed.</summary>
    public AssetStage Stage { get; }

    /// <summary>Creates the exception for a failed stage.</summary>
    public AssetPipelineException(AssetStage stage, string message) : base(message)
    {
        Stage = stage;
    }

    /// <summary>Creates the exception for a failed stage, keeping the underlying cause.</summary>
    public AssetPipelineException(AssetStage stage, string message, Exception innerException)
        : base(message, innerException)
    {
        Stage = stage;
    }
}
