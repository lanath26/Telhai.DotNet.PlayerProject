using Microsoft.Win32;
using System.Collections.ObjectModel;
using Telhai.DotNet.PlayerProject.Models;
using Telhai.DotNet.PlayerProject.MVVM;
using Telhai.DotNet.PlayerProject.Services;

namespace Telhai.DotNet.PlayerProject.ViewModels;

public class EditSongViewModel : ObservableObject
{
    private readonly SongCacheService _cache;
    private readonly SongRecord _record;

    public string FilePath => _record.FilePath;

    private string _songName;
    public string SongName
    {
        get => _songName;
        set { _songName = value; OnPropertyChanged(); }
    }

    private string? _selectedImage;
    public string? SelectedImage
    {
        get => _selectedImage;
        set { _selectedImage = value; OnPropertyChanged(); }
    }

    public ObservableCollection<string> CustomImages { get; }

    public RelayCommand AddImageCommand { get; }
    public RelayCommand RemoveImageCommand { get; }
    public RelayCommand SaveCommand { get; }

    public EditSongViewModel(SongRecord record, SongCacheService cache)
    {
        _record = record;
        _cache = cache;

        _songName = record.SongName;
        CustomImages = new ObservableCollection<string>(record.CustomImages ?? new List<string>());

        AddImageCommand = new RelayCommand(AddImage);
        RemoveImageCommand = new RelayCommand(RemoveImage, () => SelectedImage != null);
        SaveCommand = new RelayCommand(Save);
    }

    private void AddImage()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Image Files|*.png;*.jpg;*.jpeg",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            foreach (var file in dialog.FileNames)
                CustomImages.Add(file);
        }
    }

    private void RemoveImage()
    {
        if (SelectedImage == null) return;

        CustomImages.Remove(SelectedImage);
        SelectedImage = null;
        RemoveImageCommand.RaiseCanExecuteChanged();
    }

    private void Save()
    {
        _record.SongName = SongName;
        _record.CustomImages = CustomImages.ToList();

        _cache.SaveOrUpdate(_record);
    }
}
