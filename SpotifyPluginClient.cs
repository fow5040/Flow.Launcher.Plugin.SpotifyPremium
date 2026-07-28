using SpotifyAPI.Web;
using SpotifyAPI.Web.Auth;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http;
using static SpotifyAPI.Web.Scopes;
using SpotifyAPI.Web.Http;
using Newtonsoft.Json;

namespace Flow.Launcher.Plugin.SpotifyPremium
{
    public class SpotifyPluginClient
    {
        private readonly IPublicAPI _api;
        private SpotifyClient _spotifyClient;
        private static readonly HttpClient _httpClient = new HttpClient();
        private readonly object _lock = new object();
        private int mLastVolume = 10;
        private SecurityStore _securityStore;
        private string pluginDirectory;
        private const string UnknownIcon = "icon.png";
        public PrivateUser profile;

        public SpotifyPluginClient(IPublicAPI api, string pluginDir = null)
        {
            _api = api;
            pluginDirectory = pluginDir ?? Directory.GetCurrentDirectory();
            CacheFolder = Path.Combine(pluginDirectory, "Cache");

            if (!Directory.Exists(CacheFolder))
                Directory.CreateDirectory(CacheFolder);
        }

        public async Task<CurrentlyPlayingContext> GetPlaybackContextAsync()
        {
            return await _spotifyClient.Player.GetCurrentPlayback();
        }

        public async Task<bool> GetMuteStatusAsync()
        {
            var context = await GetPlaybackContextAsync();
            return context?.Device?.VolumePercent == 0;
        }

        public async Task<bool> GetShuffleStatusAsync()
        {
            var context = await GetPlaybackContextAsync();
            return context?.ShuffleState ?? false;
        }

        public async Task<PlayerSetRepeatRequest.State> GetRepeatStatusAsync()
        {
            var context = await GetPlaybackContextAsync();
            if (context == null) return PlayerSetRepeatRequest.State.Off;

            return context.RepeatState switch
            {
                "off" => PlayerSetRepeatRequest.State.Off,
                "context" => PlayerSetRepeatRequest.State.Context,
                "track" => PlayerSetRepeatRequest.State.Track,
                _ => PlayerSetRepeatRequest.State.Off
            };
        }

        public async Task<int> GetCurrentVolumeAsync()
        {
            var context = await GetPlaybackContextAsync();
            return (int)(context?.Device?.VolumePercent ?? 0);
        }

        public async Task<string> GetCurrentPlaybackNameAsync()
        {
            var context = await GetPlaybackContextAsync();
            IPlayableItem item = context?.Item;

            if (item is FullTrack track) return track.Name;
            if (item is FullEpisode episode) return episode.Name;

            return "Unknown";
        }

        public async Task<string> GetCurrentPlaybackIdAsync()
        {
            var context = await GetPlaybackContextAsync();
            IPlayableItem item = context?.Item;

            if (item is FullTrack track) return track.Id;
            if (item is FullEpisode episode) return episode.Id;

            return "Unknown";
        }

        public PlayerSetRepeatRequest.State GetRepeatStatusFromContext(CurrentlyPlayingContext context)
        {
            if (context == null) return PlayerSetRepeatRequest.State.Off;
            return context.RepeatState switch
            {
                "off" => PlayerSetRepeatRequest.State.Off,
                "context" => PlayerSetRepeatRequest.State.Context,
                "track" => PlayerSetRepeatRequest.State.Track,
                _ => PlayerSetRepeatRequest.State.Off
            };
        }

        public PlayerSetRepeatRequest.State GetNextRepeatAction(PlayerSetRepeatRequest.State currentStatus)
        {
            return currentStatus switch
            {
                PlayerSetRepeatRequest.State.Off => PlayerSetRepeatRequest.State.Context,
                PlayerSetRepeatRequest.State.Context => PlayerSetRepeatRequest.State.Track,
                PlayerSetRepeatRequest.State.Track => PlayerSetRepeatRequest.State.Off,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public async Task<string> GetActiveDeviceNameAsync()
        {
            var allDevices = await _spotifyClient.Player.GetAvailableDevices();
            if (!allDevices.Devices.Any()) return null;

            var activeDevice = allDevices.Devices.FindLast(device => device.IsActive);
            return activeDevice?.Name;
        }

        public async Task<string> GetUserIdAsync() => (await _spotifyClient.UserProfile.Current()).Id;

        private string CacheFolder { get; }

        public bool ApiConnected => _spotifyClient != null;

        public async Task<bool> CheckTokenValidityAsync()
        {
            try
            {
                var prof = await _spotifyClient.UserProfile.Current();
                this.profile = prof;
                return true;
            }
            catch (APIUnauthorizedException)
            {
                return false;
            }
        }

        public async Task<bool> UserHasSpotifyPremium()
        {
            try
            {
                if (this.profile == null)
                {
                    var prof = await _spotifyClient.UserProfile.Current();
                    this.profile = prof;
                }
                return this.profile.Product == "premium";
            }
            catch (APIUnauthorizedException)
            {
                return false;
            }
        }

        public async Task PlayAsync()
        {
            var context = await GetPlaybackContextAsync();
            if (context == null || !context.IsPlaying)
            {
                await _spotifyClient.Player.ResumePlayback();
            }
        }

        public async Task PlayAsync(string uri)
        {
            var startSongRequest = new PlayerResumePlaybackRequest();

            if (uri.Contains(":track:"))
            {
                startSongRequest.Uris = new List<string> { uri };
            }
            else
            {
                startSongRequest.ContextUri = uri;
            }

            try
            {
                await _spotifyClient.Player.ResumePlayback(startSongRequest);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public async Task EnqueueAsync(string uri)
        {
            var enqueueRequest = new PlayerAddToQueueRequest(uri);
            try
            {
                await _spotifyClient.Player.AddToQueue(enqueueRequest);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public async Task PauseAsync()
        {
            await _spotifyClient.Player.PausePlayback();
        }

        public async Task SkipAsync()
        {
            await _spotifyClient.Player.SkipNext();
        }

        public async Task SkipBackAsync()
        {
            await _spotifyClient.Player.SkipPrevious();
        }

        public async Task ToggleMuteAsync()
        {
            var context = await GetPlaybackContextAsync();
            if (context?.Device == null) return;

            int volRequest;
            if (context.Device.VolumePercent != 0)
            {
                mLastVolume = (int)(context.Device.VolumePercent ?? 100);
                volRequest = 0;
            }
            else
            {
                volRequest = mLastVolume;
            }

            await SetVolumeAsync(volRequest);
        }

        public async Task SetVolumeAsync(int volumePercent = 0)
        {
            var currentVolume = await GetCurrentVolumeAsync();
            if (currentVolume == volumePercent)
                return;

            mLastVolume = currentVolume;

            var volRequest = new PlayerVolumeRequest(volumePercent);
            await _spotifyClient.Player.SetVolume(volRequest);

            await Task.Delay(200);
        }

        public async Task ToggleShuffleAsync()
        {
            var shuffleStatus = await GetShuffleStatusAsync();
            var shuffleRequest = new PlayerShuffleRequest(!shuffleStatus);
            await _spotifyClient.Player.SetShuffle(shuffleRequest);
        }

        public async Task ToggleRepeatAsync()
        {
            var currentRepeatStatus = await GetRepeatStatusAsync();
            var nextRepeatAction = GetNextRepeatAction(currentRepeatStatus);
            var playerSetRepeatRequest = new PlayerSetRepeatRequest(nextRepeatAction);
            await _spotifyClient.Player.SetRepeat(playerSetRepeatRequest);
        }

        public async Task<bool> CheckLikedByIdAsync(string trackId)
        {
            var checkLikeRequest = new LibraryCheckTracksRequest(new List<string> { trackId });
            var response = await _spotifyClient.Library.CheckTracks(checkLikeRequest);
            return response.Any() && response[0];
        }

        public async Task ToggleLikeByIdAsync(string trackId)
        {
            var isLiked = await CheckLikedByIdAsync(trackId);
            if (!isLiked)
            {
                await LikeByIdAsync(trackId);
            }
            else
            {
                await UnlikeByIdAsync(trackId);
            }
        }

        public async Task LikeByIdAsync(string trackId)
        {
            var likeRequest = new LibrarySaveTracksRequest(new List<string> { trackId });
            await _spotifyClient.Library.SaveTracks(likeRequest);
        }

        public async Task UnlikeByIdAsync(string trackId)
        {
            var likeRemoveRequest = new LibraryRemoveTracksRequest(new List<string> { trackId });
            await _spotifyClient.Library.RemoveTracks(likeRemoveRequest);
        }

        public async Task AddLikeCurrentSongAsync()
        {
            var currentSongId = await GetCurrentPlaybackIdAsync();
            if (currentSongId != "Unknown") await LikeByIdAsync(currentSongId);
        }

        public async Task UnlikeCurrentSongAsync()
        {
            var currentSongId = await GetCurrentPlaybackIdAsync();
            if (currentSongId != "Unknown") await UnlikeByIdAsync(currentSongId);
        }

        public async Task ToggleLikeCurrentSongAsync()
        {
            var currentSongId = await GetCurrentPlaybackIdAsync();
            if (currentSongId != "Unknown") await ToggleLikeByIdAsync(currentSongId);
        }

        public bool RefreshTokenAvailable()
        {
            _securityStore = SecurityStore.Load(pluginDirectory);
            return _securityStore.HasRefreshToken;
        }

        public async Task ConnectWebClient(bool keepRefreshToken = true)
        {
            _securityStore = SecurityStore.Load(pluginDirectory);
            var server = new EmbedIOAuthServer(new Uri("http://127.0.0.1:4002/callback"), 4002);

            if (_securityStore.HasRefreshToken && keepRefreshToken)
            {
                var refreshRequest = new AuthorizationCodeRefreshRequest(_securityStore.ClientId,
                    _securityStore.ClientSecret,
                    _securityStore.RefreshToken);
                var refreshResponse = await new OAuthClient().RequestToken(refreshRequest);

                lock (_lock)
                {
                    var config = SpotifyClientConfig.CreateDefault(refreshResponse.AccessToken)
                                    .WithJSONSerializer(new JsonSerializerDecorator(_api));
                    _spotifyClient = new SpotifyClient(config);
                }
            }
            else
            {
                await server.Start();

                server.AuthorizationCodeReceived += async (_, response) =>
                {
                    await server.Stop();

                    var token = await new OAuthClient().RequestToken(
                        new AuthorizationCodeTokenRequest(_securityStore.ClientId,
                            _securityStore.ClientSecret,
                            response.Code,
                            server.BaseUri));
                    lock (_lock)
                    {
                        _securityStore.RefreshToken = token.RefreshToken;
                        _securityStore.Save(pluginDirectory);

                        var config = SpotifyClientConfig.CreateDefault(token.AccessToken)
                                    .WithJSONSerializer(new JsonSerializerDecorator(_api));
                        _spotifyClient = new SpotifyClient(config);
                    }
                };

                server.ErrorReceived += async (sender, error, state) =>
                {
                    Console.WriteLine($"Aborting authorization, error received: {error}");
                    await server.Stop();
                };

                var request = new LoginRequest(server.BaseUri, _securityStore.ClientId, LoginRequest.ResponseType.Code)
                {
                    Scope = new List<string>
                    {
                        UserLibraryRead,
                        UserLibraryModify,
                        UserReadEmail,
                        UserReadPrivate,
                        UserReadPlaybackPosition,
                        UserReadCurrentlyPlaying,
                        UserReadPlaybackState,
                        UserModifyPlaybackState,
                        AppRemoteControl,
                        PlaylistReadPrivate,
                    }
                };

                var uri = request.ToUri();
                try
                {
                    BrowserUtil.Open(uri);
                }
                catch (Exception)
                {
                    Console.WriteLine("Unable to open URL, manually open: {0}", uri);
                }
            }
        }

        public async Task<List<FullArtist>> GetArtists(string s)
        {
            var searchRequest = new SearchRequest(SearchRequest.Types.Artist, s);
            var searchResponse = await _spotifyClient.Search.Item(searchRequest);
            return searchResponse.Artists.Items;
        }

        public async Task<List<SimpleAlbum>> GetAlbums(string s)
        {
            var searchRequest = new SearchRequest(SearchRequest.Types.Album, s);
            var searchResponse = await _spotifyClient.Search.Item(searchRequest);
            return searchResponse.Albums.Items;
        }

        public async Task<List<FullTrack>> GetTracks(string s)
        {
            var searchRequest = new SearchRequest(SearchRequest.Types.Track, s);
            var searchResponse = await _spotifyClient.Search.Item(searchRequest);
            return searchResponse.Tracks.Items;
        }

        public async Task<List<SimplePlaylist>> GetPlaylists(string s)
        {
            var featuredPlaylists = (await _spotifyClient.Browse.GetFeaturedPlaylists()).Playlists.Items;
            var userPlaylistsPage = await _spotifyClient.Playlists.CurrentUsers();
            var userPlaylists = await _spotifyClient.PaginateAll(userPlaylistsPage);
            var returnedPlaylists = new List<SimplePlaylist>();

            returnedPlaylists.AddRange(
                userPlaylists.Where(playlist => playlist.Name.Contains(s, StringComparison.InvariantCultureIgnoreCase)));

            if (featuredPlaylists != null)
                returnedPlaylists.AddRange(
                    featuredPlaylists.Where(playlist => playlist.Name.Contains(s, StringComparison.InvariantCultureIgnoreCase)));

            return returnedPlaylists;
        }

        public async Task<List<SpotifySearchResult>> SearchAll(string s)
        {
            var q = $"{s.Replace(' ', '+')}*";
            var searchRequest = new SearchRequest(SearchRequest.Types.All, q) { Limit = 3 };
            var searchResponse = await _spotifyClient.Search.Item(searchRequest);
            var returnResults = new List<SpotifySearchResult>();

            if (searchResponse.Albums.Items?.Count > 0)
            {
                returnResults.AddRange(searchResponse.Albums.Items.Select(x => new SpotifySearchResult()
                {
                    Title = $"Album  :  {x.Name}",
                    Subtitle = "Album by: " + string.Join(", ", x.Artists.Select(a => a.Name)),
                    Id = x.Id,
                    Name = x.Name,
                    Uri = x.Uri,
                    Images = x.Images
                }));
            }

            if (searchResponse.Artists.Items?.Count > 0)
            {
                returnResults.AddRange(searchResponse.Artists.Items.Select(x => new SpotifySearchResult()
                {
                    Title = $"Artist  :  {x.Name}",
                    Subtitle = $"Artist Radio: {x.Name}",
                    Id = x.Id,
                    Name = x.Name,
                    Uri = x.Uri,
                    Images = x.Images
                }));
            }

            if (searchResponse.Tracks.Items?.Count > 0)
            {
                returnResults.AddRange(searchResponse.Tracks.Items.Select(x => new SpotifySearchResult()
                {
                    Title = $"Track  :  {x.Name}",
                    Subtitle = $"Album: {x.Album.Name}, by: " + string.Join(", ", x.Artists.Select(a => a.Name)),
                    Id = x.Id,
                    Name = x.Name,
                    Uri = x.Uri,
                    Images = x.Album.Images
                }));
            }

            if (searchResponse.Playlists.Items?.Count > 0)
            {
                returnResults.AddRange(searchResponse.Playlists.Items.Select(x => new SpotifySearchResult
                {
                    Title = $"Playlist :  {x.Name}",
                    Subtitle = $"Playlist by: {x.Owner.DisplayName} | {x.Tracks.Total} songs",
                    Id = x.Id,
                    Name = x.Name,
                    Uri = x.Uri,
                    Images = x.Images
                }));
            }

            return returnResults;
        }

        public async Task<List<Device>> GetDevicesAsync() => (await _spotifyClient.Player.GetAvailableDevices()).Devices;

        public async Task SetDevice(string deviceId = "")
        {
            var transferRequest = new PlayerTransferPlaybackRequest(new List<string> { deviceId });
            await _spotifyClient.Player.TransferPlayback(transferRequest);
        }

        public Task<string> GetArtworkAsync(SimpleAlbum album) => GetArtworkAsync(album.Images, album.Uri);
        public Task<string> GetArtworkAsync(FullAlbum album) => GetArtworkAsync(album.Images, album.Uri);
        public Task<string> GetArtworkAsync(FullArtist artist) => GetArtworkAsync(artist.Images, artist.Uri);
        public Task<string> GetArtworkAsync(FullTrack track) => GetArtworkAsync(track.Album);
        public Task<string> GetArtworkAsync(FullEpisode episode) => GetArtworkAsync(episode.Images, episode.Uri);
        public Task<string> GetArtworkAsync(SimplePlaylist playlist) => GetArtworkAsync(playlist.Images, playlist.Uri);
        public Task<string> GetArtworkAsync(SpotifySearchResult searchResult) => GetArtworkAsync(searchResult.Images, searchResult.Uri);

        private Task<string> GetArtworkAsync(List<Image> images, string uri)
        {
            if (!images.Any())
            {
                return Task.Run(() => UnknownIcon);
            }
            var url = images.Last().Url;
            return GetArtworkAsync(url, uri);
        }

        private async Task<string> GetArtworkAsync(string url, string resourceUri)
        {
            var uniqueId = GetUniqueIdForArtwork(resourceUri);
            return await DownloadImageAsync(uniqueId, url);
        }

        private static string GetUniqueIdForArtwork(string uri) => uri[(uri.LastIndexOf(":", StringComparison.Ordinal) + 1)..];

        private async Task<string> DownloadImageAsync(string uniqueId, string url)
        {
            var path = $@"{CacheFolder}\{uniqueId}.jpg";
            if (File.Exists(path))
            {
                return path;
            }

            var bytes = await _httpClient.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(path, bytes);
            return path;
        }
    }

    public class JsonSerializerDecorator : IJSONSerializer
    {
        private readonly NewtonsoftJSONSerializer _jsonSerializer = new();
        private IPublicAPI flowAPI;

        public JsonSerializerDecorator(IPublicAPI flowAPI)
        {
            this.flowAPI = flowAPI;
        }

        public void SerializeRequest(IRequest request)
        {
            _jsonSerializer.SerializeRequest(request);
        }

        public IAPIResponse<T> DeserializeResponse<T>(IResponse response)
        {
            try
            {
                return _jsonSerializer.DeserializeResponse<T>(response);
            }
            catch (JsonReaderException e)
            {
                flowAPI.LogDebug("JsonSerializerDecorator",
                    "Spotify API deserialize error handled safely. "
                    + "See https://github.com/JohnnyCrazy/SpotifyAPI-NET/issues/980. "
                    + string.Format("Details:\n{0}", e));
                return new APIResponse<T>(response);
            }
        }
    }
}