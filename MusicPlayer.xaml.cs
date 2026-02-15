using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Telhai.DotNet.PlayerProject.Models;
using Telhai.DotNet.PlayerProject.Services;
using Telhai.DotNet.PlayerProject.ViewModels;
using Telhai.DotNet.PlayerProject.Views;

namespace Telhai.DotNet.PlayerProject
{
    public partial class MusicPlayer : Window
    {
        private MediaPlayer mediaPlayer = new MediaPlayer();
        private DispatcherTimer timer = new DispatcherTimer();

        private readonly ItunesSearchService _itunes = new ItunesSearchService();
        private CancellationTokenSource? _itunesCts;

        // JSON cache service (song_cache.json)
        private readonly SongCacheService _songCache = new SongCacheService();

        // --- Slideshow ---
        private readonly DispatcherTimer _slideshowTimer = new DispatcherTimer();
        private List<string> _slideImages = new List<string>();
        private int _slideIndex = 0;
        private string? _currentApiArtworkUrl = null;

        private List<MusicTrack> library = new List<MusicTrack>();
        private bool isDragging = false;
        private const string FILE_NAME = "library.json";

        public MusicPlayer()
        {
            InitializeComponent();

            SetDefaultCover();

            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += new EventHandler(Timer_Tick);

            _slideshowTimer.Interval = TimeSpan.FromSeconds(3);
            _slideshowTimer.Tick += SlideshowTimer_Tick;

            this.Loaded += MusicPlayer_Loaded;
        }

        private void MusicPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            LoadLibrary();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (mediaPlayer.Source != null && mediaPlayer.NaturalDuration.HasTimeSpan && !isDragging)
            {
                sliderProgress.Maximum = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                sliderProgress.Value = mediaPlayer.Position.TotalSeconds;
            }
        }

        // ---------------- BUTTONS ----------------

        private async void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                mediaPlayer.Open(new Uri(track.FilePath));
                mediaPlayer.Play();
                timer.Start();
                txtStatus.Text = "Playing";

                var rec = await ShowMetadataWithCacheAsync(track);
                if (rec != null) StartSlideshow(rec);
                else StopSlideshow();

                return;
            }

            mediaPlayer.Play();
            timer.Start();
            txtStatus.Text = "Playing";
        }

        private void BtnPause_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Pause();
            txtStatus.Text = "Paused";
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            mediaPlayer.Stop();
            timer.Stop();
            sliderProgress.Value = 0;
            txtStatus.Text = "Stopped";
            StopSlideshow();
        }

        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            mediaPlayer.Volume = sliderVolume.Value;
        }

        private void Slider_DragStarted(object sender, MouseButtonEventArgs e)
        {
            isDragging = true;
        }

        private void Slider_DragCompleted(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            mediaPlayer.Position = TimeSpan.FromSeconds(sliderProgress.Value);
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "MP3 Files|*.mp3"
            };

            if (ofd.ShowDialog() == true)
            {
                foreach (string file in ofd.FileNames)
                {
                    MusicTrack track = new MusicTrack
                    {
                        Title = Path.GetFileNameWithoutExtension(file),
                        FilePath = file
                    };
                    library.Add(track);
                }
                UpdateLibraryUI();
                SaveLibrary();
            }
        }

        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                library.Remove(track);
                UpdateLibraryUI();
                SaveLibrary();
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            Settings settingsWin = new Settings();
            settingsWin.OnScanCompleted += SettingsWin_OnScanCompleted;
            settingsWin.ShowDialog();
        }

        private void SettingsWin_OnScanCompleted(List<MusicTrack> newTracksEventData)
        {
            foreach (var track in newTracksEventData)
            {
                if (!library.Any(x => x.FilePath == track.FilePath))
                    library.Add(track);
            }

            UpdateLibraryUI();
            SaveLibrary();
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                var record = _songCache.Get(track.FilePath);

                if (record == null)
                {
                    // Create minimal record if not exists yet
                    record = new SongRecord
                    {
                        FilePath = track.FilePath,
                        SongName = track.Title
                    };
                    _songCache.SaveOrUpdate(record);
                }

                var vm = new EditSongViewModel(record, _songCache);
                var window = new EditSongWindow(vm)
                {
                    Owner = this
                };
                window.ShowDialog();
            }
        }

        // ---------------- LIST EVENTS ----------------

        private async void LstLibrary_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                // If cached -> show cached immediately (no API call)
                var cached = _songCache.Get(track.FilePath);
                if (cached != null)
                {
                    SetMetadata(cached.SongName, cached.ArtistName, cached.AlbumName, track.FilePath);

                    // show cover: if there are custom images, show the first one; else API
                    if (cached.CustomImages != null && cached.CustomImages.Count > 0 && File.Exists(cached.CustomImages[0]))
                    {
                        ShowLocalCover(cached.CustomImages[0]);
                    }
                    else
                    {
                        await ShowApiCoverAsync(cached.ApiArtworkUrl);
                    }
                }
                else
                {
                    // requirement fallback display
                    SetMetadata(track.Title, "", "", track.FilePath);
                    SetDefaultCover();
                }
            }
        }

        private async void LstLibrary_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                mediaPlayer.Open(new Uri(track.FilePath));
                mediaPlayer.Play();
                timer.Start();
                txtStatus.Text = "Playing";

                var rec = await ShowMetadataWithCacheAsync(track);
                if (rec != null) StartSlideshow(rec);
                else StopSlideshow();
            }
        }

        // ---------------- LIBRARY SAVE/LOAD ----------------

        private void UpdateLibraryUI()
        {
            lstLibrary.ItemsSource = null;
            lstLibrary.ItemsSource = library;
        }

        private void SaveLibrary()
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(library, options);
            File.WriteAllText(FILE_NAME, json);
        }

        private void LoadLibrary()
        {
            if (File.Exists(FILE_NAME))
            {
                string json = File.ReadAllText(FILE_NAME);
                library = JsonSerializer.Deserialize<List<MusicTrack>>(json) ?? new List<MusicTrack>();
                UpdateLibraryUI();
            }
        }

        // ---------------- UI HELPERS ----------------

        private void SetDefaultCover()
        {
            try
            {
                var uri = new Uri("pack://application:,,,/Assets/default_cover.png", UriKind.Absolute);
                imgCover.Source = new BitmapImage(uri);
            }
            catch
            {
                // fallback: no image (won't crash)
                imgCover.Source = null;
            }
        }



        private void SetMetadata(string song, string artist, string album, string filePath)
        {
            txtCurrentSong.Text = string.IsNullOrWhiteSpace(song) ? "No Song Selected" : song;
            txtArtist.Text = $"Artist: {(string.IsNullOrWhiteSpace(artist) ? "-" : artist)}";
            txtAlbum.Text = $"Album: {(string.IsNullOrWhiteSpace(album) ? "-" : album)}";
            txtFilePath.Text = $"Path: {(string.IsNullOrWhiteSpace(filePath) ? "-" : filePath)}";
        }

        private async Task SetCoverFromUrlAsync(string? url, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                SetDefaultCover();
                return;
            }

            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                var bytes = await http.GetByteArrayAsync(url, ct);

                await using var ms = new MemoryStream(bytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();

                imgCover.Source = bmp;
            }
            catch
            {
                SetDefaultCover();
            }
        }

        private static string BuildSearchQuery(MusicTrack track)
        {
            var raw = track.Title ?? "";
            raw = raw.Replace('-', ' ').Replace('_', ' ').Trim();
            raw = System.Text.RegularExpressions.Regex.Replace(raw, @"\s+", " ");
            return raw;
        }

        // ---------------- SECTION 3.1: JSON CACHE + API FALLBACK ----------------

        private async Task<SongRecord?> ShowMetadataWithCacheAsync(MusicTrack track)
        {
            // 1) cache first
            var cached = _songCache.Get(track.FilePath);
            if (cached != null)
            {
                SetMetadata(cached.SongName, cached.ArtistName, cached.AlbumName, track.FilePath);
                await ShowApiCoverAsync(cached.ApiArtworkUrl);
                return cached;
            }

            // 2) no cache -> call API (with cancellation)
            _itunesCts?.Cancel();
            _itunesCts?.Dispose();
            _itunesCts = new CancellationTokenSource();
            var ct = _itunesCts.Token;

            var query = BuildSearchQuery(track);

            try
            {
                var meta = await _itunes.SearchAsync(query, ct);
                if (ct.IsCancellationRequested) return null;

                if (meta == null)
                {
                    // requirement: show local name + full path on error / no result
                    SetMetadata(track.Title, "", "", track.FilePath);
                    SetDefaultCover();
                    return null;
                }

                SetMetadata(meta.SongName, meta.ArtistName, meta.AlbumName, track.FilePath);
                await SetCoverFromUrlAsync(meta.ArtworkUrl, ct);

                var record = new SongRecord
                {
                    FilePath = track.FilePath,
                    SongName = meta.SongName,
                    ArtistName = meta.ArtistName,
                    AlbumName = meta.AlbumName,
                    ApiArtworkUrl = meta.ArtworkUrl
                };

                _songCache.SaveOrUpdate(record);
                return record;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch
            {
                SetMetadata(track.Title, "", "", track.FilePath);
                SetDefaultCover();
                return null;
            }
        }

        // ---------------- SLIDESHOW ----------------

        private void StopSlideshow()
        {
            _slideshowTimer.Stop();
            _slideImages.Clear();
            _slideIndex = 0;
            _currentApiArtworkUrl = null;
        }

        private void StartSlideshow(SongRecord record)
        {
            StopSlideshow();

            _currentApiArtworkUrl = record.ApiArtworkUrl;

            if (record.CustomImages != null && record.CustomImages.Count > 0)
            {
                _slideImages = record.CustomImages
                    .Where(p => !string.IsNullOrWhiteSpace(p) && File.Exists(p))
                    .ToList();

                if (_slideImages.Count > 0)
                {
                    _slideIndex = 0;
                    ShowLocalCover(_slideImages[_slideIndex]);

                    if (_slideImages.Count > 1)
                        _slideshowTimer.Start();

                    return;
                }
            }

            _ = ShowApiCoverAsync(_currentApiArtworkUrl);
        }

        private void SlideshowTimer_Tick(object? sender, EventArgs e)
        {
            if (_slideImages.Count == 0)
            {
                _slideshowTimer.Stop();
                return;
            }

            _slideIndex = (_slideIndex + 1) % _slideImages.Count;
            ShowLocalCover(_slideImages[_slideIndex]);
        }

        private void ShowLocalCover(string path)
        {
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();

                imgCover.Source = bmp;
            }
            catch
            {
                _ = ShowApiCoverAsync(_currentApiArtworkUrl);
            }
        }

        private async Task ShowApiCoverAsync(string? url)
        {
            // reuse cancellation to avoid old cover overwriting
            _itunesCts?.Cancel();
            _itunesCts?.Dispose();
            _itunesCts = new CancellationTokenSource();
            var ct = _itunesCts.Token;

            await SetCoverFromUrlAsync(url, ct);
        }
    }
}
