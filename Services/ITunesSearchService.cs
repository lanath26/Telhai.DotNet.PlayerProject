using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Telhai.DotNet.PlayerProject.Services
{
    public class ITunesTrackResult
    {
        public string TrackName { get; set; } = "";
        public string ArtistName { get; set; } = "";
        public string CollectionName { get; set; } = "";
        public string ArtworkUrl100 { get; set; } = "";
    }

    internal class ITunesSearchResponse
    {
        public int ResultCount { get; set; }
        public List<ITunesItem> Results { get; set; } = new();
    }

    internal class ITunesItem
    {
        public string? TrackName { get; set; }
        public string? ArtistName { get; set; }
        public string? CollectionName { get; set; }
        public string? ArtworkUrl100 { get; set; }
    }

    public class ITunesSearchService
    {
        private static readonly HttpClient _http = new HttpClient();

        public async Task<ITunesTrackResult?> SearchAsync(string query, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(query))
                return null;

            string encoded = Uri.EscapeDataString(query.Trim());
            string url = $"https://itunes.apple.com/search?term={encoded}&media=music&limit=1";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);

            resp.EnsureSuccessStatusCode();

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            var data = await JsonSerializer.DeserializeAsync<ITunesSearchResponse>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                ct
            );

            if (data == null || data.ResultCount == 0 || data.Results.Count == 0)
                return null;

            var first = data.Results[0];

            return new ITunesTrackResult
            {
                TrackName = first.TrackName ?? "",
                ArtistName = first.ArtistName ?? "",
                CollectionName = first.CollectionName ?? "",
                ArtworkUrl100 = first.ArtworkUrl100 ?? ""
            };
        }
    }
}
