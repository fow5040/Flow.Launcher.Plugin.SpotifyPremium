using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;

namespace Wox.Plugin.Spotify
{
    class SecurityStore
    {
        public String RefreshToken { get; set; }
        public Boolean HasRefreshToken { get { return !String.IsNullOrEmpty(RefreshToken); } }

        public static SecurityStore Load()
        {
            if(new FileInfo("security.store").Exists)
            {
                return JsonConvert.DeserializeObject<SecurityStore>(File.ReadAllText("security.store"));
            }
            return new SecurityStore();
        }

        public void Save()
        {
            File.WriteAllText("security.store", JsonConvert.SerializeObject(this));
        }
    }
}
