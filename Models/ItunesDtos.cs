using System.Text.Json.Serialization;

namespace Telhai.DotNet.PlayerProject.Models;

public sealed class ItunesSearchResponse
{
    [JsonPropertyName("resultCount")]
    public int ResultCount { get; set; }

    [JsonPropertyName("results")]
    public List<ItunesTrack> Results { get; set; } = new();
}

public sealed class ItunesTrack
{
    [JsonPropertyName("trackName")]
    public string? TrackName { get; set; }

    [JsonPropertyName("artistName")]
    public string? ArtistName { get; set; }

    [JsonPropertyName("collectionName")]
    public string? CollectionName { get; set; }

    [JsonPropertyName("artworkUrl100")]
    public string? ArtworkUrl100 { get; set; }
}
