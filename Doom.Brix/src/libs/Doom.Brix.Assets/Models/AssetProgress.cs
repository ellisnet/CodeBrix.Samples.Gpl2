namespace Doom.Brix.Assets.Models;

/// <summary>A progress report from the asset-installation pipeline.</summary>
public readonly struct AssetProgress
{
    /// <summary>Creates a progress report.</summary>
    public AssetProgress(AssetStage stage, double fraction)
    {
        Stage = stage;
        Fraction = fraction;
    }

    /// <summary>The pipeline stage the report belongs to.</summary>
    public AssetStage Stage { get; }

    /// <summary>How far the stage has progressed, from 0.0 to 1.0.</summary>
    public double Fraction { get; }
}
