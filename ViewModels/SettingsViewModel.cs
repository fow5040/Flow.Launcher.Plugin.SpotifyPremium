using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Flow.Launcher.Plugin.SpotifyPremium.ViewModels
{
    public class SettingsViewModel : BaseModel
    {
        public SettingsViewModel(Settings settings)
        {
            Settings = settings;
        }

        public Settings Settings { get; init; }
    }
}