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

namespace Wox.Plugin.Spotify
{
    public class SpotifyApi
    {
        //private readonly SpotifyLocalAPI _localSpotify;
        private SpotifyWebAPI _spotifyApi;
        private readonly object _lock = new object();
        private int mLastVolume = 10;
        private SecurityStore _securityStore;

        public SpotifyApi(string pluginDir = null)
        {
            var pluginDirectory = pluginDir ?? Directory.GetCurrentDirectory();
            CacheFolder = Path.Combine(pluginDirectory, "Cache");

            // Create the cache folder, if it doesn't already exist
            if (!Directory.Exists(CacheFolder))
                Directory.CreateDirectory(CacheFolder);

            /*_localSpotify = new SpotifyLocalAPI();
            _localSpotify.OnTrackChange += (o, e) => CurrentTrack = e.NewTrack;
            _localSpotify.OnPlayStateChange += (o, e) => IsPlaying = e.Playing;*/
        }

        //public bool IsPlaying { get; set; }

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

        //public bool IsConnected { get; private set; }

        public bool ApiConnected
        {
            get
            {
                return _spotifyApi != null;
            }
        }

        //public bool IsRunning => SpotifyLocalAPI.IsSpotifyRunning() && SpotifyLocalAPI.IsSpotifyWebHelperRunning();

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

        public async Task ConnectWebApi()
        {
            _securityStore = SecurityStore.Load();

            AuthorizationCodeAuth auth = new AuthorizationCodeAuth(_securityStore.ClientId, _securityStore.ClientSecret, "http://localhost:4002", "http://localhost:4002",
               Scope.PlaylistReadPrivate | Scope.PlaylistReadCollaborative | Scope.UserReadCurrentlyPlaying | Scope.UserReadPlaybackState | Scope.UserModifyPlaybackState | Scope.Streaming | Scope.UserFollowModify);


            if (_securityStore.HasRefreshToken)
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
                    _securityStore.Save();
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
                Paging<SimplePlaylist> userPlaylistsPaging = _spotifyApi.GetUserPlaylists(currentUserID,50);
                while (true)
                {
                    if (!userPlaylistsPaging.HasNextPage())
                        break;
                    userPlaylistsPaging = _spotifyApi.GetNextPage(userPlaylistsPaging);
                }
                // Filter results based on search and combine into one large SimplePlaylists list
                List<SimplePlaylist> returnedPlaylists = userPlaylistsPaging.Items.Where( playlist => playlist.Name.ToLower().Contains(s.ToLower())).ToList();
                List<SimplePlaylist> returnedFeaturedPlaylists = featuredPlaylists.Playlists.Items.Where( playlist => playlist.Name.ToLower().Contains(s.ToLower())).ToList();

                return returnedPlaylists.Concat(returnedFeaturedPlaylists);
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

        public Task<string> GetArtworkAsync(List<Image> images, string uri)
        {
            if (!images.Any())
                return null;

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

        /*public void ConnectToSpotify()
        {
            if (!IsRunning)
            {
                return;
            }
            
            var successful = _localSpotify.Connect();
            if (successful)
            {
                UpdateInfos();
                _localSpotify.ListenForEvents = true;
            }
        }*/

        /*private void UpdateInfos()
        {
            var status = _localSpotify.GetStatus();

            if (status?.Track != null) //Update track infos
            {
                CurrentTrack = status.Track;
                IsPlaying = status.Playing;
                IsConnected = true;
            }
        }*/
        
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