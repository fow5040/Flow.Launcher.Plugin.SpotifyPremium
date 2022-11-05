using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Flow.Launcher.Plugin.SpotifyPremium.ViewModels;

namespace Flow.Launcher.Plugin.SpotifyPremium.Views
{
    /// <summary>
    /// Interaction logic for SpotifyPremiumSettings.xaml
    /// </summary>
    public partial class SpotifyPremiumSettings : UserControl
    {
        private readonly SettingsViewModel _viewModel;
        private readonly Settings _settings;

        public SpotifyPremiumSettings(SettingsViewModel viewModel)
        {
            _viewModel = viewModel;
            _settings = viewModel.Settings;
            DataContext = viewModel;
            InitializeComponent();
        }

        private void SpotifyPremiumSettings_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void spotifyClientCredentials_Collapsed(object sender, RoutedEventArgs e)
        {
            spotifyClientCredentials.Height = double.NaN;
            SetButtonVisibilityToHidden();
        }
    }

    
}
