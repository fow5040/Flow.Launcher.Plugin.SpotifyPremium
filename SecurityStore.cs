using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.SpotifyPremium
{
    class SecurityStore
    {
        public string RefreshToken { get; set; }

        public bool HasRefreshToken => !string.IsNullOrEmpty(RefreshToken);
        [JsonInclude]
        public string ClientSecret { get; private set;}
        [JsonInclude]
        public string ClientId {get; private set;}

        public static SecurityStore Load(string pluginDir = null)
        {
            if(new FileInfo(pluginDir+"\\security.store").Exists)
            {
                return JsonSerializer.Deserialize<SecurityStore>(File.ReadAllText(pluginDir+"\\security.store"));
            }
            //Including personal ClientID and Secret
            //May remove if potential App/API usage gets out of hand
            SecurityStore _securityStore = new SecurityStore();
            _securityStore.ClientId = "41544e27545e4196bdfb6ff3c90d4451";
            _securityStore.ClientSecret = "c575cd3b2a2347eb846b28f35d541ace";
            return _securityStore;
        }

        public void Save(string pluginDir = null)
        {
            File.WriteAllText(pluginDir+"\\security.store", JsonSerializer.Serialize(this));
        }
    }
}
