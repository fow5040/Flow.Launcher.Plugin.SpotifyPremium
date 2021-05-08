using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using SpotifyAPI.Web;

namespace Flow.Launcher.Plugin.SpotifyPremium
{
    public class SpotifySearchResult
    {
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string Uri { get; set; }
        public List<Image> Images { get; set; }
    }
}