using Flow.Launcher.Plugin;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Flow.Launcher.Plugin.SpotifyPremium.ViewModels
{
    public class SettingsViewModel
    {
        public Settings settings { get; set; }

        private PluginInitContext context { get; set; }

        private string storageDirectory { get; set; }
        private const string storageFilename = "settings.json";



        public SettingsViewModel(PluginInitContext context)
        {
            storageDirectory = context.CurrentPluginMetadata.PluginDirectory;
            this.context = context;
            this.settings = Load();
        }

        public Settings Load()
        {

            if(new FileInfo(storageDirectory + "\\" + storageFilename).Exists)
            {
                return JsonConvert.DeserializeObject<Settings>(File.ReadAllText(storageDirectory+"\\" + storageFilename));
            }            
            //if (fileExists) {
            if (false) {
                //validate file
                //if valid, return 
                //else, return new Settings

            }

            return new Settings();

        }


        public void Save()
        {
            File.WriteAllText(storageDirectory + "\\" + storageFilename,
                              JsonConvert.SerializeObject(settings));
        }
    }
}
