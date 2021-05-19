using System.Collections.Generic;

namespace Flow.Launcher.Plugin.SpotifyPremium
{
    public class Settings
    {
        public string refreshToken = "";
        public string userClientId = "";
        public string userClientSecret = "";

        //Flag to limit client calls to X ms after a keystroke 
        //Set to 'false' to stop optimizing client calls
        public bool userOptimizeClientQueries = true;
        //Time to wait before issuing an expensive query 
        public int userOptimizeClientQueryDelay = 500;

    }
}