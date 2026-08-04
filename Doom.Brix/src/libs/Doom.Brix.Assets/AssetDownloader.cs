using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Doom.Brix.Assets;

/// <summary>
/// Downloads the permitted asset file over HTTP(S) with progress reporting.
/// The request presents itself like the embedded WPE WebKit browser the user
/// clicked the link in (User-Agent plus the clicked page as Referer), which
/// satisfies the common hotlink checks on download mirrors.
/// </summary>
public sealed class AssetDownloader
{
    // Matches the browser engine family of the embedded WebView (WPE WebKit).
    const string UserAgent =
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15";

    const int CopyBufferSize = 81920;

    static readonly HttpClient client = CreateClient();

    static HttpClient CreateClient()
    {
        var created = new HttpClient();
        // Downloads can be long on slow links; cancellation governs, not a timeout.
        created.Timeout = Timeout.InfiniteTimeSpan;
        return created;
    }

    /// <summary>
    /// Downloads the given URL to the destination file, reporting progress as
    /// a 0..1 fraction. When the server does not declare a Content-Length,
    /// the expected-size fallback (the known authentic file size) paces the
    /// progress instead.
    /// </summary>
    public async Task DownloadFileAsync(string url, string referrerUrl, string destinationFilePath,
        long expectedSizeFallback, IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("A download URL is required", nameof(url));
        if (string.IsNullOrWhiteSpace(destinationFilePath))
            throw new ArgumentException("A destination file path is required", nameof(destinationFilePath));

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);
        request.Headers.TryAddWithoutValidation("Accept", "*/*");
        if (!string.IsNullOrWhiteSpace(referrerUrl) && Uri.TryCreate(referrerUrl, UriKind.Absolute, out var referrer))
            request.Headers.Referrer = referrer;

        using var response = await client
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? expectedSizeFallback;

        var buffer = new byte[CopyBufferSize];
        long received = 0;
        using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        using (var outputStream = File.Create(destinationFilePath))
        {
            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await outputStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken).ConfigureAwait(false);
                received += bytesRead;
                if (totalBytes > 0)
                    progress?.Report(Math.Min(1d, (double) received / totalBytes));
            }
        }

        progress?.Report(1d);
    }
}
