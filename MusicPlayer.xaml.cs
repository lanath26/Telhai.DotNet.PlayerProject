using Microsoft.Win32;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using Telhai.DotNet.PlayerProject.Services;

namespace Telhai.DotNet.PlayerProject
{
    public partial class MusicPlayer : Window
    {
        private MediaPlayer mediaPlayer = new MediaPlayer();
        private DispatcherTimer timer = new DispatcherTimer();

        private readonly ItunesSearchService _itunes = new ItunesSearchService();
        private CancellationTokenSource? _itunesCts;

        private List<MusicTrack> library = new List<MusicTrack>();
        private bool isDragging = false;
        private const string FILE_NAME = "library.json";

        public MusicPlayer()
        {
            InitializeComponent();

            SetDefaultCover();

            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += new EventHandler(Timer_Tick);

            this.Loaded += MusicPlayer_Loaded;
        }

        private void MusicPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            this.LoadLibrary();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (mediaPlayer.Source != null && mediaPlayer.NaturalDuration.HasTimeSpan && !isDragging)
            {
                sliderProgress.Maximum = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                sliderProgress.Value = mediaPlayer.Position.TotalSeconds;
            }
        }

        private async void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            // If a song is selected, start it; otherwise just resume.
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                mediaPlayer.Open(new Uri(track.FilePath));
                mediaPlayer.Play();
                timer.Start();
                txtStatus.Text = "Playing";

                await FetchAndShowMetadataAsync(track);
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
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = true;
            ofd.Filter = "MP3 Files|*.mp3";

            if (ofd.ShowDialog() == true)
            {
                foreach (string file in ofd.FileNames)
                {
                    MusicTrack track = new MusicTrack
                    {
                        Title = System.IO.Path.GetFileNameWithoutExtension(file),
                        FilePath = file
                    };
                    library.Add(track);
                }
                UpdateLibraryUI();
                SaveLibrary();
            }
        }

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

        // ---------- UI HELPERS ----------

        private void SetDefaultCover()
        {
            var uri = new Uri("pack://application:,,,/Assets/default_cover.png", UriKind.Absolute);
            imgCover.Source = new BitmapImage(uri);
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

        // ---------- ITUNES (ASYNC + CANCELLATION) ----------

        private async Task FetchAndShowMetadataAsync(MusicTrack track)
        {
            _itunesCts?.Cancel();
            _itunesCts?.Dispose();
            _itunesCts = new CancellationTokenSource();
            var ct = _itunesCts.Token;

            var query = BuildSearchQuery(track);

            try
            {
                var meta = await _itunes.SearchAsync(query, ct);

                if (ct.IsCancellationRequested)
                    return;

                if (meta == null)
                {
                    SetMetadata(track.Title, "", "", track.FilePath);
                    SetDefaultCover();
                    return;
                }

                SetMetadata(meta.SongName, meta.ArtistName, meta.AlbumName, track.FilePath);
                await SetCoverFromUrlAsync(meta.ArtworkUrl, ct);
            }
            catch (OperationCanceledException)
            {
                // normal when switching songs fast
            }
            catch
            {
                // On error: show local file name (without extension already in Title) and full path.
                SetMetadata(track.Title, "", "", track.FilePath);
                SetDefaultCover();
            }
        }

        // ---------- LIST EVENTS ----------

        private void LstLibrary_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                // Single click: show name + local path only (no API call in section 2).
                SetMetadata(track.Title, "", "", track.FilePath);
                SetDefaultCover();
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

                await FetchAndShowMetadataAsync(track);
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
                {
                    library.Add(track);
                }
            }

            UpdateLibraryUI();
            SaveLibrary();
        }
    }
}
