using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using SpotifyAPI.Web.Enums;
using SpotifyAPI.Web.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Wox.Plugin.SpotifyPremium
{
    public class SpotifyApi
    {
        private SpotifyWebAPI _spotifyApi;
        private readonly object _lock = new object();
        private int mLastVolume = 10;
        private SecurityStore _securityStore;
        private string pluginDirectory;
        private const string UnknownIcon = "icon.png";

        public SpotifyApi(string pluginDir = null)
        {
            pluginDirectory = pluginDir ?? Directory.GetCurrentDirectory();
            CacheFolder = Path.Combine(pluginDirectory, "Cache");

            // Create the cache folder, if it doesn't already exist
            if (!Directory.Exists(CacheFolder))
                Directory.CreateDirectory(CacheFolder);

        }

        public bool MuteStatus
        {
            get
            {
                return PlaybackContext.Device.VolumePercent == 0;
            }
        }
        
        public bool ShuffleStatus
        {
            get{
                return PlaybackContext.ShuffleState;
            }
        }

        public int CurrentVolume
        {
            get{
                return PlaybackContext.Device.VolumePercent;
            }
        }

        public PlaybackContext PlaybackContext {
            get
            {
                return _spotifyApi.GetPlayback();
            }
        }

        public String ActiveDeviceName{
            get
            {
                //Returns null, or active device string
                AvailabeDevices allDevices = _spotifyApi.GetDevices();
                if (allDevices.Devices == null) return null;
                
                Device ActiveDevice = allDevices.Devices.FindLast( device => device.IsActive);
                return (ActiveDevice != null) ? ActiveDevice.Name : null;
            }
        }

        private string CacheFolder { get; }

        public bool ApiConnected
        {
            get
            {
                return _spotifyApi != null;
            }
        }

        public bool TokenValid
        {
            get
            {
                //Hit a lightweight endpoint to see if the current token is still valid
                return !_spotifyApi.GetPrivateProfile().HasError();
            }
        }

        public void Play()
        {
            _spotifyApi.ResumePlaybackAsync("", "", null, "", 0);
        }

        public void Play(string uri)
        {
            if(uri.Contains(":track:")){
                _spotifyApi.ResumePlaybackAsync("","", new List<string>() { uri }, "", 0);
            }
            else{
                _spotifyApi.ResumePlaybackAsync("",uri, null, "", 0);
            }
        }

        public void Pause()
        {
            _spotifyApi.PausePlaybackAsync();
        }

        public void Skip()
        {
            _spotifyApi.SkipPlaybackToNextAsync();
        }

        public void SkipBack()
        {
            _spotifyApi.SkipPlaybackToPreviousAsync();
	    }

        public void ToggleMute()
        {
            Device currentDevice = PlaybackContext.Device;
            if(currentDevice.VolumePercent != 0)
            {
                mLastVolume = currentDevice.VolumePercent;
                _spotifyApi.SetVolumeAsync(0, currentDevice.Id);
            }
            else
            {
                _spotifyApi.SetVolumeAsync(mLastVolume, currentDevice.Id);
            }
        }

        public void SetVolume(int volumePercent = 0)
        {
            _spotifyApi.SetVolumeAsync(volumePercent, PlaybackContext.Device.Id);
        }

        public void ToggleShuffle()
        {
            _spotifyApi.SetShuffleAsync(!ShuffleStatus);
        }

        public async Task ConnectWebApi(bool keepRefreshToken = true)
        {
            _securityStore = SecurityStore.Load(pluginDirectory);

            AuthorizationCodeAuth auth = new AuthorizationCodeAuth(_securityStore.ClientId, _securityStore.ClientSecret, "http://localhost:4002", "http://localhost:4002",
               Scope.PlaylistReadPrivate | Scope.PlaylistReadCollaborative | Scope.UserReadCurrentlyPlaying | Scope.UserReadPlaybackState | Scope.UserModifyPlaybackState | Scope.Streaming | Scope.UserFollowModify);


            if (_securityStore.HasRefreshToken && keepRefreshToken)
            {
                Token token = await auth.RefreshToken(_securityStore.RefreshToken);
                _spotifyApi = new SpotifyWebAPI() { TokenType = token.TokenType, AccessToken = token.AccessToken };
            }
            else
            {
                auth.AuthReceived += async (sender, payload) =>
                {
                    auth.Stop();
                    Token token = await auth.ExchangeCode(payload.Code);
                    _securityStore.RefreshToken = token.RefreshToken;
                    _securityStore.Save(pluginDirectory);
                    _spotifyApi = new SpotifyWebAPI() { TokenType = token.TokenType, AccessToken = token.AccessToken };
                };
                auth.Start();
                auth.OpenBrowser();
            }
        }

        public string GetUserID(){
            return _spotifyApi.GetPrivateProfile().Id;
        }

        public IEnumerable<FullArtist> GetArtists(string s)
        {
            lock (_lock)
            {
                var searchItems = _spotifyApi.SearchItems(s, SearchType.Artist, 10);

                return searchItems.Artists.Items;
            }
        }

        public IEnumerable<FullAlbum> GetAlbums(string s)
        {
            lock (_lock)
            {
                var searchItems = _spotifyApi.SearchItems(s, SearchType.Album, 10);

                var results = _spotifyApi.GetSeveralAlbums(searchItems.Albums.Items.Select(a => a.Id).ToList());

                return results.Albums;
            }
        }

        public IEnumerable<FullTrack> GetTracks(string s)
        {
            lock (_lock)
            {
                var searchItems = _spotifyApi.SearchItems(s, SearchType.Track, 10);

                return searchItems.Tracks.Items;
            }
        }

        public IEnumerable<SimplePlaylist> GetPlaylists(string s, string currentUserID)
        {

            lock (_lock)
            {
                FeaturedPlaylists featuredPlaylists = _spotifyApi.GetFeaturedPlaylists();
                Paging<SimplePlaylist> userPlaylistsPaging = _spotifyApi.GetUserPlaylists(currentUserID,500);
                List<SimplePlaylist> returnedPlaylists = new List<SimplePlaylist>();

                while (true)
                {
                    // Add current page to returnedPlaylists list
                    // Also, filter results based on search string
                    returnedPlaylists = returnedPlaylists.Concat(
                        userPlaylistsPaging.Items.Where(
                            playlist => playlist.Name.ToLower().Contains(s.ToLower())).ToList()).ToList();

                    if (!userPlaylistsPaging.HasNextPage())
                        break;
                    userPlaylistsPaging = _spotifyApi.GetNextPage(userPlaylistsPaging);
                }

                // Filter results based on search and combine into one large SimplePlaylists list
                List<SimplePlaylist> returnedFeaturedPlaylists = featuredPlaylists.Playlists.Items.Where( playlist => playlist.Name.ToLower().Contains(s.ToLower())).ToList();

                return returnedPlaylists.Concat(returnedFeaturedPlaylists);
            }
        }

        public IEnumerable<SpotifySearchResult> SearchAll(string s)
        {
            lock (_lock)
            {
                string q = String.Concat(s.Replace(' ','+'),"*");
                SearchItem searchResults = _spotifyApi.SearchItems(q,SearchType.All,3);
                List<SpotifySearchResult> returnResults = new List<SpotifySearchResult>(); 

                if(searchResults.Albums.Items.Count() > 0){
                    returnResults.AddRange(searchResults.Albums.Items.Select(x => new SpotifySearchResult()
                    {
                        Title = $"Album  :  {x.Name}",
                        Subtitle = "Album by: " + string.Join(", ", x.Artists.Select(a => a.Name)),
                        Id = x.Id,
                        Name = x.Name,
                        Uri = x.Uri,
                        Images = x.Images
                    }).ToList());
                }

                if(searchResults.Artists.Items.Count() > 0){
                    returnResults.AddRange(searchResults.Artists.Items.Select( x => new SpotifySearchResult()
                    {
                        Title = $"Artist  :  {x.Name}",
                        Subtitle = $"Play Artist Radio: {x.Name}",
                        Id = x.Id,
                        Name = x.Name,
                        Uri = x.Uri,
                        Images = x.Images
                    }).ToList());
                }

                if(searchResults.Tracks.Items.Count() > 0){
                    returnResults.AddRange(searchResults.Tracks.Items.Select( x => new SpotifySearchResult()
                    {
                        Title = $"Track  :  {x.Name}",
                        Subtitle = $"Album: {x.Album.Name}, by: " + string.Join(", ", x.Artists.Select(a => a.Name)),
                        Id = x.Id,
                        Name = x.Name,
                        Uri = x.Uri,
                        Images = x.Album.Images
                    }).ToList());
                }

                if(searchResults.Playlists.Items.Count() > 0){
                    returnResults.AddRange(searchResults.Playlists.Items.Select( x => new SpotifySearchResult()
                    {
                        Title = $"Playlist :  {x.Name}",
                        Subtitle = $"Playlist by: {x.Owner.DisplayName} | {x.Tracks.Total} songs",
                        Id = x.Id,
                        Name = x.Name,
                        Uri = x.Uri,
                        Images = x.Images
                    }).ToList());
                }

                return returnResults;

            }
        }

        public List<Device> GetDevices()
        {
            lock (_lock)
            {
                return _spotifyApi.GetDevices().Devices;
            }
        }

        public void SetDevice(string deviceId = "")
        {
            _spotifyApi.TransferPlayback(new List<string>{deviceId}, false);
        }

        public Task<string> GetArtworkAsync(SimpleAlbum album) => GetArtworkAsync(album.Images, album.Uri);

        public Task<string> GetArtworkAsync(FullAlbum album) => GetArtworkAsync(album.Images, album.Uri);

        public Task<string> GetArtworkAsync(FullArtist artist) => GetArtworkAsync(artist.Images, artist.Uri);

        public Task<string> GetArtworkAsync(FullTrack track) => GetArtworkAsync(track.Album);

        public Task<string> GetArtworkAsync(SimplePlaylist playlist) => GetArtworkAsync(playlist.Images,playlist.Uri);

        public Task<string> GetArtworkAsync(SpotifySearchResult searchResult) => GetArtworkAsync(searchResult.Images,searchResult.Uri);

        public Task<string> GetArtworkAsync(List<Image> images, string uri)
        {
            if (!images.Any()){
                return Task<string>.Run( () => UnknownIcon );
            }

            var url = images.Last().Url;

            return GetArtworkAsync(url, uri);
        }

        private async Task<string> GetArtworkAsync(string url, string resourceUri)
        {
            // use the unique spotify ID as the local file name
            var uniqueId = GetUniqueIdForArtwork(resourceUri);

            return await DownloadImageAsync(uniqueId, url);
        }

        private static string GetUniqueIdForArtwork(string uri) => uri.Substring(uri.LastIndexOf(":", StringComparison.Ordinal) + 1);

        private async Task<string> DownloadImageAsync(string uniqueId, string url)
        {
            // local path to the image file, located in the Cache folder
            var path = $@"{CacheFolder}\{uniqueId}.jpg";

            if (File.Exists(path))
            {
                return path;
            }

            using (var wc = new WebClient())
            {
                await wc.DownloadFileTaskAsync(new Uri(url), path);
            }

            return path;
        }
    }
}