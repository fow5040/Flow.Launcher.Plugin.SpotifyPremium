using System.Windows.Controls;

namespace Flow.Launcher.Plugin.SpotifyPremium
{
    public class SpotifySettingsUserControl : UserControl
    {
        public SpotifySettingsUserControl(SpotifySettings settings)
        {
            DataContext = settings;
        }
    }
}
