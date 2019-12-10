using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

namespace Wox.Plugin.SpotifyPremium
{
    class SecurityStore
    {
        public String RefreshToken { get; set; }
        public Boolean HasRefreshToken { get { return !String.IsNullOrEmpty(RefreshToken); } }
        public String ClientSecret { get; set;}
        public String ClientId {get; set;}

        public static SecurityStore Load(string pluginDir = null)
        {
            if(new FileInfo(pluginDir+"\\security.store").Exists)
            {
                return JsonConvert.DeserializeObject<SecurityStore>(File.ReadAllText(pluginDir+"\\security.store"));
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
            File.WriteAllText(pluginDir+"\\security.store", JsonConvert.SerializeObject(this));
        }
    }
}
