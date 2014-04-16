using System;
using System.IO;
using System.Text;
using System.Net;
using Newtonsoft.Json;

namespace Wox.Plugin.Spotify.MetadataApi
{
    public class ApiData
    {
        // base URL for track/album/artist data requests
        private const string SPOTIFY_API_URI = "http://ws.spotify.com/search/1/{0}.json?q={1}";        

        // base url to retrieve artwork of any track/album/artist data
        private const string SPOTIFY_EMBED_URI = "https://embed.spotify.com/oembed/?url={0}";

        // path to the plugin directory
        private string PluginDirecotry;

        public ApiData(string pluginDir = null)
        {
            this.PluginDirecotry = pluginDir ?? Directory.GetCurrentDirectory();

            // Create the cache folder, if it doesn't already exist
            if (!Directory.Exists(CacheFodler))
                Directory.CreateDirectory(CacheFodler);
        }

        /// <summary>
        /// Retrieves track data and returns the (deserialized) response
        /// </summary>
        public TracksApiResponse GetTracks(string searchTerms)
        {
            var data = GetData(searchTerms, SearchType.Track);
            return JsonConvert.DeserializeObject<TracksApiResponse>(data);
        }

        /// <summary>
        /// Retrieves album data and returns the (deserialized) response
        /// </summary>
        public AlbumsApiResponse GetAlbums(string searchTerms)
        {
            var data = GetData(searchTerms, SearchType.Album);
            return JsonConvert.DeserializeObject<AlbumsApiResponse>(data);
        }

        /// <summary>
        /// Retrieves artist data and returns the (deserialized) response
        /// </summary>
        public ArtistsApiResponse GetArtists(string searchTerms)
        {
            var data = GetData(searchTerms, SearchType.Artist);
            return JsonConvert.DeserializeObject<ArtistsApiResponse>(data);
        }

        /// <summary>
        /// Creates the request uri based on the specified search terms and search type
        /// </summary>
        /// <param name="searchTerms">what to search for</param>
        /// <param name="type">type of search (track, album or artist)</param>
        /// <returns>the API response as string</returns>
        public string GetData(string searchTerms, SearchType type)
        {
            var uri = string.Format(SPOTIFY_API_URI, type.ToString().ToLower(), searchTerms);

            return GetData(uri);
        }

        /// <summary>
        /// Creates a request to the specified uri and returns the response as string
        /// </summary>
        public string GetData(string uri)
        {
            var request = WebRequest.Create(uri);
            var response = (HttpWebResponse) request.GetResponse();

            if (response.StatusCode != HttpStatusCode.OK || response.GetResponseStream() == null)
            {
                return null;
            }

            return new StreamReader(response.GetResponseStream(), Encoding.UTF8).ReadToEnd();
        }

        /// <summary>
        /// Retrieve and deserialize artwork information, download the image
        /// in the Cache folder and return a path to the local file.
        /// </summary>
        /// <param name="href">The Spotify URL of any track, album or artist</param>
        public string GetArtwork(string href)
        {
            var uri = string.Format(SPOTIFY_EMBED_URI, href);
            
            var request = (HttpWebRequest)WebRequest.Create(uri);
            // Set a UserAgent, otherwise Spotify's response will be empty!
            request.UserAgent = "Mozilla/5.0";
            var response = (HttpWebResponse)request.GetResponse();
            if (response.StatusCode != HttpStatusCode.OK || response.GetResponseStream() == null)
            {
                return null;
            }

            // Read response stream
            var responseData = new StreamReader(response.GetResponseStream(), Encoding.UTF8).ReadToEnd();

            // get a URL to the thumbnail, which could be an album cover or an artist avatar
            var thumbnailUrl = JsonConvert.DeserializeObject<EmbedApiResponse>(responseData).ThumbnailUrl;

            // use the unique spotify ID as the local file name
            var uniqueId = href.Substring(href.LastIndexOf(":") + 1);

            // local path to the image file, located in the Cache folder
            var path = string.Format(@"{0}\{1}.jpg", CacheFodler, uniqueId);

            //TODO: Optimize...
            // Download the image file            
            if (!File.Exists(path))
                new WebClient().DownloadFile(new Uri(thumbnailUrl), path);

            return path;
        }

        private string CacheFodler
        {
            get { return Path.Combine(PluginDirecotry, "Cache"); }
        }
    }

    public enum SearchType
    {
        Track, Album, Artist
    }
}
