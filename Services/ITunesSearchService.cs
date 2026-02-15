using System.Net.Http;
using System.Text.Json;
using Telhai.DotNet.PlayerProject.Models;

namespace Telhai.DotNet.PlayerProject.Services;

public sealed class ItunesSearchService
{
    private static readonly HttpClient _http = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public async Task<TrackMetadata?> SearchAsync(string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        // iTunes Search API:
        // https://itunes.apple.com/search?term=<query>&media=music&limit=1
        var url = $"https://itunes.apple.com/search?term={Uri.EscapeDataString(query)}&media=music&limit=1";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        using var res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();

        await using var stream = await res.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        var data = await JsonSerializer.DeserializeAsync<ItunesSearchResponse>(stream, cancellationToken: ct).ConfigureAwait(false);

        var first = data?.Results?.FirstOrDefault();
        if (first == null)
            return null;

        var artwork = UpgradeArtwork(first.ArtworkUrl100);

        return new TrackMetadata
        {
            SongName = first.TrackName ?? "",
            ArtistName = first.ArtistName ?? "",
            AlbumName = first.CollectionName ?? "",
            ArtworkUrl = artwork
        };
    }

    private static string? UpgradeArtwork(string? url100)
    {
        if (string.IsNullOrWhiteSpace(url100))
            return null;

        return url100.Replace("100x100", "300x300");
    }
}

public sealed class TrackMetadata
{
    public string SongName { get; set; } = "";
    public string ArtistName { get; set; } = "";
    public string AlbumName { get; set; } = "";
    public string? ArtworkUrl { get; set; }
}
