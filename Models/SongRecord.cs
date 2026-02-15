namespace Telhai.DotNet.PlayerProject.Models;

public class SongRecord
{
    public string FilePath { get; set; } = "";

    public string SongName { get; set; } = "";
    public string ArtistName { get; set; } = "";
    public string AlbumName { get; set; } = "";

    // artwork url from iTunes API (cached)
    public string? ApiArtworkUrl { get; set; }

    // images user adds in edit window (absolute paths)
    public List<string> CustomImages { get; set; } = new();
}
