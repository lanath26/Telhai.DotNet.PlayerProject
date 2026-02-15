using System.Windows;
using Telhai.DotNet.PlayerProject.ViewModels;

namespace Telhai.DotNet.PlayerProject.Views;

public partial class EditSongWindow : Window
{
    public EditSongWindow(EditSongViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
