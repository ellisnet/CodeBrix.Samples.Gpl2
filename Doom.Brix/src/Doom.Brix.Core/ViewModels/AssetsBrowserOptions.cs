using Doom.Brix.Assets;

namespace Doom.Brix.ViewModels;

/// <summary>
/// Optional Assets Mode browser settings, registered by an alternate host (for
/// example an offline test fixture) through the <c>App(Action&lt;IServiceCollection&gt;)</c>
/// constructor. Without a registration the browser opens the idgames entry for doom19s.zip.
/// </summary>
public sealed class AssetsBrowserOptions
{
    /// <summary>The page the embedded browser opens once an assets folder is chosen.</summary>
    public string StartUrl { get; init; } = DoomAssetCatalog.DefaultBrowseUrl;
}
