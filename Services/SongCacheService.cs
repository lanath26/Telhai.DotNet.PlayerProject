using System.IO;
using System.Text.Json;
using Telhai.DotNet.PlayerProject.Models;

namespace Telhai.DotNet.PlayerProject.Services;

public class SongCacheService
{
    private const string FILE_NAME = "song_cache.json";
    private Dictionary<string, SongRecord> _cache = new();

    public SongCacheService()
    {
        Load();
    }

    private void Load()
    {
        if (!File.Exists(FILE_NAME))
            return;

        try
        {
            var json = File.ReadAllText(FILE_NAME);
            var data = JsonSerializer.Deserialize<Dictionary<string, SongRecord>>(json);
            if (data != null)
                _cache = data;
        }
        catch
        {
            _cache = new Dictionary<string, SongRecord>();
        }
    }

    private void Save()
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(_cache, options);
        File.WriteAllText(FILE_NAME, json);
    }

    public SongRecord? Get(string filePath)
    {
        _cache.TryGetValue(filePath, out var record);
        return record;
    }

    public void SaveOrUpdate(SongRecord record)
    {
        _cache[record.FilePath] = record;
        Save();
    }
}
