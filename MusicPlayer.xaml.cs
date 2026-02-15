using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Telhai.DotNet.PlayerProject.Services;

namespace Telhai.DotNet.PlayerProject
{
    /// <summary>
    /// Interaction logic for MusicPlayer.xaml
    /// </summary>
    public partial class MusicPlayer : Window
    {
        private MediaPlayer mediaPlayer = new MediaPlayer();
        private DispatcherTimer timer = new DispatcherTimer();
        private List<MusicTrack> library = new List<MusicTrack>();
        private bool isDragging = false;
        private const string FILE_NAME = "library.json";

        // iTunes Service + cancellation
        private readonly ITunesSearchService _itunes = new ITunesSearchService();
        private CancellationTokenSource? _itunesCts;

        // Default artwork (online placeholder for now)
        private readonly BitmapImage _defaultArtwork =
            new BitmapImage(new Uri("https://via.placeholder.com/150/444444/FFFFFF?text=No+Art"));

        public MusicPlayer()
        {
            //--init all Hardcoded xaml into Elements Tree
            InitializeComponent();

            timer.Interval = TimeSpan.FromMilliseconds(500);
            timer.Tick += new EventHandler(Timer_Tick);

            this.Loaded += MusicPlayer_Loaded;
            // this.MouseDoubleClick += MusicPlayer_MouseDoubleClick;
            // this.MouseDoubleClick += new MouseButtonEventHandler(MusicPlayer_MouseDoubleClick);
        }

        private void MusicPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            this.LoadLibrary();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            // Update slider ONLY if music is loaded AND user is NOT holding the handle
            if (mediaPlayer.Source != null && mediaPlayer.NaturalDuration.HasTimeSpan && !isDragging)
            {
                sliderProgress.Maximum = mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
                sliderProgress.Value = mediaPlayer.Position.TotalSeconds;
            }
        }

        // --- EMPTY PLACEHOLDERS TO MAKE IT BUILD ---
        private void BtnPlay_Click(object sender, RoutedEventArgs e)
        {
            //if (sender is Button btn)
            //{
            //    btn.Background = Brushes.LightGreen;
            //}

            // חובה לפי הדרישה: PLAY ינגן את השיר שנבחר
            if (lstLibrary.SelectedItem is not MusicTrack track)
            {
                txtStatus.Text = "Select a song first";
                return;
            }

            // If no source loaded or different track selected -> load it
            if (mediaPlayer.Source == null ||
                !string.Equals(mediaPlayer.Source.LocalPath, track.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                mediaPlayer.Open(new Uri(track.FilePath));
                txtCurrentSong.Text = track.Title;
                txtFilePath.Text = track.FilePath;
            }

            mediaPlayer.Play();
            timer.Start();
            txtStatus.Text = "Playing";

            // דרישה: הקריאה ל-API במקביל לניגון, ללא חסימת UI + ביטול קריאה קודמת
            _itunesCts?.Cancel();
            _itunesCts = new CancellationTokenSource();
            _ = LoadItunesMetadataAsync(track, _itunesCts.Token);
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

            // דרישה: למנוע קריאות מיותרות לשירות ע"י ביטול קריאה קודמת בעת מעבר/עצירה
            _itunesCts?.Cancel();
        }

        private void SliderVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            mediaPlayer.Volume = sliderVolume.Value;
        }

        private void Slider_DragStarted(object sender, MouseButtonEventArgs e)
        {
            isDragging = true; // Stop timer updates
        }

        private void Slider_DragCompleted(object sender, MouseButtonEventArgs e)
        {
            isDragging = false; // Resume timer updates
            mediaPlayer.Position = TimeSpan.FromSeconds(sliderProgress.Value);
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            //File Dialog to choose file from system
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Multiselect = true;
            ofd.Filter = "MP3 Files|*.mp3";

            //User Confirmed
            if (ofd.ShowDialog() == true)
            {
                //iterate all files selected as tring
                foreach (string file in ofd.FileNames)
                {
                    //Create Object for each filr
                    MusicTrack track = new MusicTrack
                    {
                        //Only file name
                        Title = System.IO.Path.GetFileNameWithoutExtension(file),
                        //full path
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
            //Take All library list as Source to the listbox
            //diaplay tostring for inner object whithin list
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
                //read File
                string json = File.ReadAllText(FILE_NAME);
                //Create List Of MusicTrack from json
                library = JsonSerializer.Deserialize<List<MusicTrack>>(json) ?? new List<MusicTrack>();
                //Show All loaded MusicTrack in List Box
                UpdateLibraryUI();
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

        // Single click (SelectionChanged) shows local title + local path
        private void LstLibrary_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                txtCurrentSong.Text = track.Title;
                txtFilePath.Text = track.FilePath;
                txtStatus.Text = "Selected";

                // דרישה: לחיצה רגילה תציג שם שיר ומסלול הקובץ + ברירת מחדל לתמונה/metadata
                ShowDefaultMetadataForLocal(track, "Selected");
            }
        }

        private void LstLibrary_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // דרישה: לחיצה כפולה ברשימה תנגן אותו
            if (lstLibrary.SelectedItem is MusicTrack track)
            {
                mediaPlayer.Open(new Uri(track.FilePath));
                mediaPlayer.Play();
                timer.Start();

                txtCurrentSong.Text = track.Title;
                txtFilePath.Text = track.FilePath;
                txtStatus.Text = "Playing";

                // דרישה: קריאת API במקביל לניגון + ביטול קריאה קודמת
                _itunesCts?.Cancel();
                _itunesCts = new CancellationTokenSource();
                _ = LoadItunesMetadataAsync(track, _itunesCts.Token);
            }
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            //1) Create Settings Window Instance
            Settings settingsWin = new Settings();

            //2) Subscribe/register to OnScanCompleted Event
            settingsWin.OnScanCompleted += SettingsWin_OnScanCompleted;

            settingsWin.ShowDialog();
        }

        private void SettingsWin_OnScanCompleted(List<MusicTrack> newTracksEventData)
        {
            foreach (var track in newTracksEventData)
            {
                // Prevent duplicates based on FilePath
                if (!library.Any(x => x.FilePath == track.FilePath))
                {
                    library.Add(track);
                }
            }

            UpdateLibraryUI();
            SaveLibrary();
        }

        // ===== iTunes helpers =====

        private static string BuildSearchQueryFromFileName(string filePath)
        {
            // דרישה: החיפוש ל-API ייגזר משם הקובץ (מופרד ברווחים או מקף)
            string name = Path.GetFileNameWithoutExtension(filePath) ?? "";
            name = name.Replace("-", " ").Replace("_", " ").Trim();
            return name;
        }

        private void ShowDefaultMetadataForLocal(MusicTrack track, string? status = null)
        {
            // דרישה: הגדר תמונת ברירת מחדל לשירים
            txtMetaSong.Text = track.Title;
            txtMetaArtist.Text = "-";
            txtMetaAlbum.Text = "-";
            imgArtwork.Source = _defaultArtwork;

            // מסלול קובץ במחשב (לא מה API)
            txtCurrentSong.Text = track.Title;
            txtFilePath.Text = track.FilePath;

            if (!string.IsNullOrWhiteSpace(status))
                txtStatus.Text = status;
        }

        private async Task LoadItunesMetadataAsync(MusicTrack track, CancellationToken ct)
        {
            // UI לא נחסם: מציגים local מיד, ואת ה-API מביאים במקביל
            ShowDefaultMetadataForLocal(track, "Loading metadata...");

            string query = BuildSearchQueryFromFileName(track.FilePath);

            try
            {
                // דרישה: שימוש ב ASYNC AWAIT + הפרדה לשכבת Service
                var result = await _itunes.SearchAsync(query, ct);

                // אם בוטל בגלל מעבר שיר - לא ממשיכים לעדכן UI
                ct.ThrowIfCancellationRequested();

                if (result == null)
                {
                    // דרישה: במידה והתקבלה שגיאה/אין מידע -> להציג שם קובץ ללא סיומת + מסלול מלא
                    ShowDefaultMetadataForLocal(track, "No metadata found (showing local)");
                    return;
                }

                // הצגת נתונים מה API
                txtMetaSong.Text = string.IsNullOrWhiteSpace(result.TrackName) ? track.Title : result.TrackName;
                txtMetaArtist.Text = string.IsNullOrWhiteSpace(result.ArtistName) ? "-" : result.ArtistName;
                txtMetaAlbum.Text = string.IsNullOrWhiteSpace(result.CollectionName) ? "-" : result.CollectionName;

                // תמונת עטיפת אלבום (ברירת מחדל אם אין)
                if (!string.IsNullOrWhiteSpace(result.ArtworkUrl100))
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(result.ArtworkUrl100);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();

                    imgArtwork.Source = bmp;
                }
                else
                {
                    imgArtwork.Source = _defaultArtwork;
                }

                txtStatus.Text = "Playing (metadata loaded)";
            }
            catch (OperationCanceledException)
            {
                // דרישה: מניעת קריאות מיותרות לשירות - ביטול קריאה קודמת (זה תקין)
            }
            catch
            {
                // דרישה: במידה והתקבלה שגיאה בקריאה יש להציג local (שם קובץ ללא סיומת + מסלול מלא)
                ShowDefaultMetadataForLocal(track, "Metadata error (showing local)");
            }
        }
    }

    //private void MusicPlayer_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    //{
    //    MainWindow p = new MainWindow();
    //    p.Title = "YYYYY";
    //    p.Show();
    //}
}
