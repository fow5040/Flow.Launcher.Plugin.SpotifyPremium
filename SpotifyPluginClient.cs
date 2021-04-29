using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Net;
using static SpotifyAPI.Web.Scopes;

namespace Wox.Plugin.SpotifyPremium
{
    public class SpotifyPluginClient
    {
        private SpotifyClient _spotifyClient;
        private readonly object _lock = new object();
        private int mLastVolume = 10;
        private SecurityStore _securityStore;
        private string pluginDirectory;
        private const string UnknownIcon = "icon.png";

        public SpotifyPluginClient(string pluginDir = null)
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
                return (int) PlaybackContext.Device.VolumePercent ;  //Device.VolumePercent;
            }
        }

        public CurrentlyPlayingContext PlaybackContext {
            get
            {
                return _spotifyClient.Player.GetCurrentPlayback().GetAwaiter().GetResult();
            }
        }

        public String CurrentPlaybackName {
            get
            {
                IPlayableItem item =  _spotifyClient.Player.GetCurrentPlayback().GetAwaiter().GetResult().Item;
                if (item is FullTrack track){
                    return track.Name;
                }

                if (item is FullEpisode episode){
                    return episode.Name;
                }

                return "Unknown";

            }
        }

        public String ActiveDeviceName{
            get
            {
                //Returns null, or active device string
                DeviceResponse allDevices = _spotifyClient.Player.GetAvailableDevices().GetAwaiter().GetResult();;
                if (allDevices.Devices == null) return null;
                
                Device ActiveDevice = allDevices.Devices.FindLast( device => device.IsActive);
                return (ActiveDevice != null) ? ActiveDevice.Name : null;
            }
        }

        public String UserID{
            get
            {
                return _spotifyClient.UserProfile.Current().GetAwaiter().GetResult().Id;
            }
        }


        private string CacheFolder { get; }

        public bool ApiConnected
        {
            get
            {
                return _spotifyClient != null;
            }
        }

        public bool TokenValid
        {
            get
            {
                //Hit a lightweight endpoint to see if the current token is still valid
                try {
                    var prof = _spotifyClient.UserProfile.Current().GetAwaiter().GetResult();
                    return true;
                    
                }
                catch (APIUnauthorizedException e) {
                    return false;
                }

            }
        }

        public void Play()
        {
            if ( ! PlaybackContext.IsPlaying)
            {
                _spotifyClient.Player.ResumePlayback().GetAwaiter().GetResult();
            }
        }

        // Due to API Enhancements, the Spotify API can now return FullEpisodes or FullTracks 
        //   which have different parameters, requiring casting within the Play method
        public void Play(String uri)
        {
            PlayerResumePlaybackRequest startSongRequest = new PlayerResumePlaybackRequest();
            startSongRequest.Uris.Add(uri);
            _spotifyClient.Player.ResumePlayback(startSongRequest).GetAwaiter().GetResult();

            //if(uri.Contains(":track:")){
            //    _spotifyClient.ResumePlaybackAsync("","", new List<string>() { uri }, "", 0);
            //}
            //else{
            //    _spotifyClient.ResumePlaybackAsync("",uri, null, "", 0);
            //}
        }
        public void Enqueue(String uri)
        {
            PlayerAddToQueueRequest enqueueRequest = new PlayerAddToQueueRequest(uri);
            _spotifyClient.Player.AddToQueue(enqueueRequest).GetAwaiter().GetResult();;
        }

        public void Pause()
        {
            _spotifyClient.Player.PausePlayback();
        }

        public void Skip()
        {
            _spotifyClient.Player.SkipNext();
        }

        public void SkipBack()
        {
            _spotifyClient.Player.SkipPrevious();
	    }

        public void ToggleMute()
        {
            Device currentDevice = PlaybackContext.Device;
            PlayerVolumeRequest volRequest;
            if(currentDevice.VolumePercent != 0)
            {
                // VolumePercent is nullable for whatever reason - assume to be 100 if null
                mLastVolume = (currentDevice.VolumePercent != null ? (int) currentDevice.VolumePercent : 100);
                volRequest = new PlayerVolumeRequest(0);
            }
            else
            {
                volRequest = new PlayerVolumeRequest(mLastVolume);
            }

            _spotifyClient.Player.SetVolume(volRequest).GetAwaiter().GetResult();;
        }

        public void SetVolume(int volumePercent = 0)
        {
            var volRequest = new PlayerVolumeRequest(volumePercent);
            _spotifyClient.Player.SetVolume(volRequest).GetAwaiter().GetResult();;
        }

        public void ToggleShuffle()
        {
            var shuffleRequest = new PlayerShuffleRequest(!ShuffleStatus);
            _spotifyClient.Player.SetShuffle(shuffleRequest).GetAwaiter().GetResult();;
            //_spotifyClient.SetShuffleAsync(!ShuffleStatus);
        }

        public async Task ConnectWebClient(bool keepRefreshToken = true)
        {
            _securityStore = SecurityStore.Load(pluginDirectory);

            EmbedIOAuthServer _server = new EmbedIOAuthServer(new Uri("http://localhost:5000/callback"), 5000);

            if (_securityStore.HasRefreshToken && keepRefreshToken)
            {
               // var refreshRequest = new TokenSwapRefreshRequest(
               //     new Uri("http://localhost:5001/refresh"),
               //     _securityStore.RefreshToken
               // );
                
                var refreshRequest = new AuthorizationCodeRefreshRequest(_securityStore.ClientId, 
                                                                         _securityStore.ClientSecret,
                                                                         _securityStore.RefreshToken);
                var refreshResponse = await new OAuthClient().RequestToken(refreshRequest) ;

                _spotifyClient = new SpotifyClient(refreshResponse.AccessToken);

            }
            else
            {
                await _server.Start();

                _server.AuthorizationCodeReceived += async (object sender, AuthorizationCodeResponse response) =>
                {
                    await _server.Stop();

                    AuthorizationCodeTokenResponse token = await new OAuthClient().RequestToken(
                        new AuthorizationCodeTokenRequest(_securityStore.ClientId,
                                                          _securityStore.ClientSecret,
                                                          response.Code,
                                                          _server.BaseUri));

                    _securityStore.RefreshToken = token.RefreshToken;
                    _securityStore.Save(pluginDirectory);
                    _spotifyClient = new SpotifyClient(token.AccessToken);
                };

                var request = new LoginRequest(_server.BaseUri, _securityStore.ClientId, LoginRequest.ResponseType.Code)
                {
                    Scope = new List<string> { UserLibraryRead,
                                               UserReadEmail,
                                               UserReadPrivate,
                                               UserReadPlaybackPosition,
                                               UserReadCurrentlyPlaying,
                                               UserModifyPlaybackState,
                                               AppRemoteControl,
                                               PlaylistReadPrivate }
                };

                Uri uri = request.ToUri();
                try {
                    BrowserUtil.Open(uri);
                } catch (Exception) {
                    Console.WriteLine("Unable to open URL, manually open: {0}", uri);
                };
            };

        }

        public async Task<List<FullArtist>> GetArtists(string s)
        {
            var searchRequest = new SearchRequest( SearchRequest.Types.Artist, s);
            var searchResponse = await _spotifyClient.Search.Item(searchRequest);
            return searchResponse.Artists.Items;
        }
        public async Task<List<SimpleAlbum>> GetAlbums(string s)
        {
            var searchRequest = new SearchRequest( SearchRequest.Types.Album, s);
            var searchResponse = await _spotifyClient.Search.Item(searchRequest);
            return searchResponse.Albums.Items;
        }

        public async Task<List<FullTrack>> GetTracks(string s)
        {
            var searchRequest = new SearchRequest( SearchRequest.Types.Track, s);
            var searchResponse = await _spotifyClient.Search.Item(searchRequest);
            return searchResponse.Tracks.Items;
        }

        public async Task<List<SimplePlaylist>> GetPlaylists(string s)
        {

            var featuredPlaylists = _spotifyClient.Browse.GetFeaturedPlaylists().Result.Playlists.Items;
            var userPlaylistsPage = await _spotifyClient.Playlists.CurrentUsers();
            var userPlaylists = await _spotifyClient.PaginateAll(userPlaylistsPage);
            List<SimplePlaylist> returnedPlaylists = new List<SimplePlaylist>();

            //Add User playlists that contain the query
            returnedPlaylists = returnedPlaylists.Concat(
                userPlaylists.Where(
                    playlist => playlist.Name.ToLower().Contains(s.ToLower())).ToList()).ToList();
            

            //Add Featured playlists that contain the query
            returnedPlaylists = returnedPlaylists.Concat(
                featuredPlaylists.ToList<SimplePlaylist>().Where(
                    playlist => playlist.Name.ToLower().Contains(s.ToLower())).ToList()).ToList();

            return returnedPlaylists;
        }

        public async Task<List<SpotifySearchResult>> SearchAll(string s)
        {
            string q = String.Concat(s.Replace(' ','+'),"*");
            var searchRequest = new SearchRequest( SearchRequest.Types.All, q);
            searchRequest.Limit = 3;
            var searchResponse = await _spotifyClient.Search.Item(searchRequest);

            List<SpotifySearchResult> returnResults = new List<SpotifySearchResult>(); 

            if(searchResponse.Albums.Items.Count() > 0){
                returnResults.AddRange(searchResponse.Albums.Items.Select(x => new SpotifySearchResult()
                {
                    Title = $"Album  :  {x.Name}",
                    Subtitle = "Album by: " + string.Join(", ", x.Artists.Select(a => a.Name)),
                    Id = x.Id,
                    Name = x.Name,
                    Uri = x.Uri,
                    Images = x.Images
                }).ToList());
            }

            if(searchResponse.Artists.Items.Count() > 0){
                returnResults.AddRange(searchResponse.Artists.Items.Select( x => new SpotifySearchResult()
                {
                    Title = $"Artist  :  {x.Name}",
                    Subtitle = $"Artist Radio: {x.Name}",
                    Id = x.Id,
                    Name = x.Name,
                    Uri = x.Uri,
                    Images = x.Images
                }).ToList());
            }

            if(searchResponse.Tracks.Items.Count() > 0){
                returnResults.AddRange(searchResponse.Tracks.Items.Select( x => new SpotifySearchResult()
                {
                    Title = $"Track  :  {x.Name}",
                    Subtitle = $"Album: {x.Album.Name}, by: " + string.Join(", ", x.Artists.Select(a => a.Name)),
                    Id = x.Id,
                    Name = x.Name,
                    Uri = x.Uri,
                    Images = x.Album.Images
                }).ToList());
            }

            if(searchResponse.Playlists.Items.Count() > 0){
                returnResults.AddRange(searchResponse.Playlists.Items.Select( x => new SpotifySearchResult()
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

        public List<Device> GetDevices()
        {
            lock (_lock)
            {
                return _spotifyClient.Player.GetAvailableDevices().Result.Devices;
            }
        }

        public void SetDevice(string deviceId = "")
        {
            var transferRequest = new PlayerTransferPlaybackRequest(new List<string>{deviceId});
            _spotifyClient.Player.TransferPlayback(transferRequest).GetAwaiter().GetResult();;
        }

        public Task<string> GetArtworkAsync(SimpleAlbum album) => GetArtworkAsync(album.Images, album.Uri);

        public Task<string> GetArtworkAsync(FullAlbum album) => GetArtworkAsync(album.Images, album.Uri);

        public Task<string> GetArtworkAsync(FullArtist artist) => GetArtworkAsync(artist.Images, artist.Uri);

        public Task<string> GetArtworkAsync(FullTrack track) => GetArtworkAsync(track.Album);
        public Task<string> GetArtworkAsync(FullEpisode episode) => GetArtworkAsync(episode.Images, episode.Uri);

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