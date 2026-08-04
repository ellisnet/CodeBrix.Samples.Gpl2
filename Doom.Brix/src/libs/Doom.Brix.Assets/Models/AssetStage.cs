namespace Doom.Brix.Assets.Models;

/// <summary>The stage of the asset-installation pipeline a progress report or failure belongs to.</summary>
public enum AssetStage
{
    /// <summary>The asset file is downloading to the temp folder.</summary>
    Downloading,

    /// <summary>The downloaded file's authenticity is being verified (size, CRC-32, MD5).</summary>
    Verifying,

    /// <summary>The game assets are being extracted into the user's assets folder.</summary>
    Extracting,
}
