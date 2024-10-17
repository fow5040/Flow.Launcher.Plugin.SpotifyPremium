using Flow.Launcher.Plugin;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SpotifyAPI.Web;

namespace Flow.Launcher.Plugin.SpotifyPremium
{
    public class SpotifyPlugin : IAsyncPlugin
    {
        private PluginInitContext _context;

        private SpotifyPluginClient _client;

        private readonly Dictionary<string, Func<string, List<Result>>> _terms = new(StringComparer.InvariantCultureIgnoreCase);
        private readonly Dictionary<string, Func<string, Task<List<Result>>>> _expensiveTerms = new(StringComparer.InvariantCultureIgnoreCase);

        private const string SpotifyIcon = "icon.png";

        private string currentUserId; //Required for playlist querying

        private bool optimizeclientUsage = true; //Flag to limit client calls to X ms after a keystroke 
        //Set to 'false' to stop optimizing client calls

        private DateTime lastQueryTime; //Record the time on every query
        //Almost every keypress counts as a new query

        private string currentQuery;

        private const int OptimizeClientKeyDelay = 200; //Time to wait before issuing an expensive query
        private int cachedVolume = -1;

        private SemaphoreSlim authSemaphore = new SemaphoreSlim(1, 1);

        //Wait for delay before querying 
        //Specify expensive search terms for optimizing client usage
        private readonly string[] expensiveSearchTerms =
        {
            "artist", "album", "track", "playlist", "queue"
        };


        public Task InitAsync(PluginInitContext context)
        {
            _context = context;
            lastQueryTime = DateTime.UtcNow;

            // initialize data, passing it the plugin directory
            Task.Run(() => _client = new SpotifyPluginClient(context.API, _context.CurrentPluginMetadata.PluginDirectory));

            _expensiveTerms.Add("artist", SearchArtist);
            _expensiveTerms.Add("album", SearchAlbum);
            _expensiveTerms.Add("track", SearchTrack);
            _expensiveTerms.Add("playlist", SearchPlaylist);
            _expensiveTerms.Add("device", GetDevices);
            _expensiveTerms.Add("queue", QueueSearch);

            _terms.Add("next", PlayNext);
            _terms.Add("last", PlayLast);
            _terms.Add("pause", Pause);
            _terms.Add("play", Play);
            _terms.Add("mute", ToggleMute);
            _terms.Add("vol", SetVolume);
            _terms.Add("volume", SetVolume);
            _terms.Add("shuffle", ToggleShuffle);
            _terms.Add("like", AddLikeCurrentSong);
            _terms.Add("unlike", UnlikeCurrentSong);
            _terms.Add("toggle", ToggleLikeCurrentSong);

            //view query count and average query duration
            _terms.Add("diag", q =>
                SingleResultInList(
                    $"Query Count: {context.CurrentPluginMetadata.QueryCount}",
                    $"Avg. Query Time: {context.CurrentPluginMetadata.AvgQueryTime}ms",
                    action: null));

            _terms.Add("reconnect", q =>
                SingleResultInList(
                    "Reconnect",
                    "Force a reconnection and remove the refresh token",
                    action: ReconnectAction(_client, false)));

            return Task.CompletedTask;
        }

        private List<Result> Play(string arg) =>
            SingleResultInList("Play", $"Resume: {_client.CurrentPlaybackName}", action: _client.Play);

        private List<Result> Pause(string arg = null) =>
            SingleResultInList("Pause", $"Pause: {_client.CurrentPlaybackName}", action: _client.Pause);

        private List<Result> PlayNext(string arg) =>
            SingleResultInList("Next", $"Skip: {_client.CurrentPlaybackName}", action: _client.Skip);

        private List<Result> PlayLast(string arg) =>
            SingleResultInList("Last", "Skip Backwards", action: _client.SkipBack);

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            //Save the raw query for requery API call. Refreshes current playing info
            currentQuery = query.RawQuery;

            if (!_client.RefreshTokenAvailable())
            {
                return SingleResultInList(
                    "Require Authentication", "Select to authorize",
                    action: ReconnectAction(_client),
                    hideAfterAction: false);
            }
            
            if (!_client.ApiConnected)
            {
                await ReconnectAsync();
            }

            if (!await _client.CheckTokenValidityAsync())
            {
                await ReconnectAsync();
            }

            if (!await _client.UserHasSpotifyPremium())
            {
                return SingleResultInList(
                    "Current Spotify account is not premium!",
                    "Switch to premium account, then select this to use new login",
                    action: ReconnectAction(_client, false),
                    hideAfterAction: false);
            }

            if (token.IsCancellationRequested)
                return null;

            try
            {
                List<Result> results;

                // display status if no parameters are added
                if (string.IsNullOrWhiteSpace(query.Search))
                {
                    return await GetPlaying();
                }

                //Run the query if it is not an expensive search term
                if (_terms.ContainsKey(query.FirstSearch))
                {
                    return _terms[query.FirstSearch].Invoke(query.SecondToEndSearch);
                }

                //If query is expensive, AND if optimize client Usage is flagged
                //  return null if query is updated within set number ms
                //  this limits the client calls made
                //  if you type a 10 character query quickly enough, only the last keypress searches the Spotify client
                if (optimizeclientUsage)
                {
                    await Task.Delay(OptimizeClientKeyDelay, token);
                    if (token.IsCancellationRequested)
                        return null;
                }

                if (_expensiveTerms.ContainsKey(query.FirstSearch))
                {
                    results = await _expensiveTerms[query.FirstSearch].Invoke(query.SecondToEndSearch);
                    return results;
                }


                return await SearchAllAsync(query.Search);


            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return SingleResultInList(
                    "There was an error with your request",
                    e.GetBaseException().Message);
            }
        }

        private async Task<List<Result>> GetPlaying()
        {
            var d = await _client.GetActiveDeviceNameAsync();
            if (d == null)
            {
                //Must have an active device to control Spotify
                return SingleResultInList(
                    "No active device", "Select device with `sp device`", 
                    action: () =>
                        {
                            _context.API.ChangeQuery($"{_context.CurrentPluginMetadata.ActionKeywords[0]} device");
                        }, 
                    hideAfterAction: false);
                    
            }

            var playbackContext = _client.PlaybackContext;

            var item = playbackContext.Item;

            var t = item as FullTrack;
            var e = item as FullEpisode;

            var status = playbackContext.IsPlaying ? "Now Playing" : "Paused";
            var toggleAction = playbackContext.IsPlaying ? "Pause" : "Resume";

            // Check if item is a track, episode, or default icon if neither work
            var icon = t != null ? _client.GetArtworkAsync(t) :
                e != null ? _client.GetArtworkAsync(e) :
                null;

            return new List<Result>()
            {
                SingleResultInList(
                    t?.Name ?? e?.Name ?? "Not Available",
                    $"{status} | by {string.Join(", ", t.Artists.Select(a => String.Join("", a.Name)))}",
                    icon != null ? icon.Result : SpotifyIcon).First(),
                SingleResultInList(
                    "Pause / Resume",
                    $"{toggleAction}: {t.Name}",
                    action: () =>
                        {
                            if (playbackContext.IsPlaying)
                            {
                                _client.Pause();
                            }
                            else
                            {
                                _client.Play();
                            }

                        },
                    hideAfterAction: true).First(),
                PlayNext(string.Empty).First(),
                PlayLast(string.Empty).First(),
                ToggleMute().First(),
                ToggleShuffle().First(),
                SetVolume().First()
            };
        }

        private List<Result> ToggleMute(string arg = null)
        {
            var toggleAction = _client.MuteStatus ? "Unmute" : "Mute";
            return SingleResultInList("Toggle Mute", $"{toggleAction}: {_client.CurrentPlaybackName}", action: _client.ToggleMute);
        }

        private struct SetVolAction {
            public enum VolAction {
                DISPLAY,
                ABSOLUTE,
                DECREASE,
                INCREASE
            }

            public VolAction action;
            public int target;
            public int current;
            // validAction returns false if parsing the actionString fails
            // to create a valid volume change operation, or if no
            // action needs to be taken
            public bool validAction;

            public SetVolAction(string actionString, int current) {
                this.validAction = false;
                this.target = -1;
                this.current = current;
                if (string.IsNullOrWhiteSpace(actionString)) {
                    this.action = VolAction.DISPLAY;
                    return;
                }
                string intString = actionString;
                this.action = VolAction.ABSOLUTE;
                if (actionString[0] == '+')
                {
                    this.action = VolAction.INCREASE;
                    intString = actionString.Substring(1);
                }
                if (actionString[0] == '-')
                {
                    this.action = VolAction.DECREASE;
                    intString = actionString.Substring(1);
                }

                if (int.TryParse(intString, out var amt))
                {
                    switch (this.action)
                    {
                        case VolAction.ABSOLUTE:
                            this.target = amt;
                            break;
                        case VolAction.INCREASE:
                            this.target = this.current + amt;
                            if (this.target > 100) this.target = 100;
                            break;
                        case VolAction.DECREASE:
                            this.target = this.current - amt;
                            if (this.target < 0) this.target = 0;
                            break;
                    }

                    if (this.target is >= 0 and <= 100) {
                        this.validAction = true;
                        return;
                    } 

                }

                // If there's no valid action to take, fall back to displaying
                // the current volume
                this.action = VolAction.DISPLAY;
            }

        }

        private List<Result> SetVolume(string arg = null)
        {

            cachedVolume = _client.CurrentVolume;
            SetVolAction volAction = new SetVolAction(arg, cachedVolume);

            if (volAction.validAction)
            {
                return SingleResultInList(
                    $"Set Volume to {volAction.target}",
                    $"Current Volume: {cachedVolume}",
                    action: () =>
                        {
                            _client.SetVolume(volAction.target);
                        });
            }

            return SingleResultInList($"Volume", $"Current Volume: {cachedVolume}", action: () => { });
        }

        private List<Result> ToggleShuffle(string arg = null)
        {
            var toggleAction = _client.ShuffleStatus ? "Off" : "On";
            return SingleResultInList("Toggle Shuffle", $"Turn Shuffle {toggleAction}", action: _client.ToggleShuffle);
        }

        private List<Result> AddLikeCurrentSong(string arg = null)
        {
            var currentSong = _client.CurrentPlaybackName;
            return SingleResultInList("Like", $"Add '{currentSong}' to liked songs", action: _client.AddLikeCurrentSong);
        }

        private List<Result> UnlikeCurrentSong(string arg = null)
        {
            var currentSong = _client.CurrentPlaybackName;
            return SingleResultInList("Remove", $"Remove '{currentSong}' from liked songs", action: _client.UnlikeCurrentSong);
        }

        private List<Result> ToggleLikeCurrentSong(string arg = null)
        {
            var currentSongName = _client.CurrentPlaybackName;
            var currentSongId = _client.CurrentPlaybackId;
            var currentSongIsLiked = _client.CheckLikedById(currentSongId);
            var subtitle = currentSongIsLiked 
                ? $"Remove '{currentSongName}' from liked songs" 
                : $"Add '{currentSongName}' to liked songs";
            return SingleResultInList("Toggle", subtitle, action: _client.ToggleLikeCurrentSong);
        }

        private async Task<List<Result>> SearchAllAsync(string param)
        {
            if (!_client.ApiConnected) return AuthenticateResult;

            if (string.IsNullOrWhiteSpace(param))
            {
                return SingleResultInList("sp {any search term}", "Perform a full search on albums, tracks, artists, and playlists.");
            }

            // Retrieve data and return the first 20 results
            var searchResults = await _client.SearchAll(param);
            var results = searchResults.Select(async x => new Result()
            {
                Title = x.Title,
                SubTitle = x.Subtitle,
                IcoPath = await _client.GetArtworkAsync(x),
                Action = _ =>
                {
                    _client.Play(x.Uri);
                    return true;
                }
            }).ToArray();

            await Task.WhenAll(results);

            return results.Any() ? results.Select(x => x.Result).ToList() : NothingFoundResult;
        }

        private Task<List<Result>> SearchTrack(string param) => SearchTrack(param, false);
        private async Task<List<Result>> SearchTrack(string param, bool shouldQueue = false)
        {
            if (!_client.ApiConnected) return AuthenticateResult;

            if (string.IsNullOrWhiteSpace(param))
            {
                return SingleResultInList("sp track {track name}", "Search for a single Track to play.");
            }

            // Retrieve data and return the first 20 results
            var searchResults = _client.GetTracks(param).Result;
            var results = searchResults.Select(async x => new Result()
            {
                Title = x.Name,
                SubTitle = (shouldQueue ? "Queue track by " : "") +
                           "Artist: " + string.Join(", ", x.Artists.Select(a => a.Name)),
                IcoPath = await _client.GetArtworkAsync(x),
                Action = _ =>
                {
                    if (shouldQueue)
                        _client.Enqueue(x.Uri);
                    else
                        _client.Play(x.Uri);
                    return true;
                }
            }).ToArray();

            await Task.WhenAll(results);
            return results.Any() ? results.Select(x => x.Result).ToList() : NothingFoundResult;
        }

        private async Task<List<Result>> SearchAlbum(string param)
        {
            if (!_client.ApiConnected) return AuthenticateResult;

            if (string.IsNullOrWhiteSpace(param))
            {
                return SingleResultInList("sp album {album name}", "Search for an Album to play.");
            }

            //Get first page of results
            var searchResults = _client.GetAlbums(param).Result;
            var results = searchResults.Select(async x => new Result()
            {
                Title = x.Name,
                SubTitle = "by " + string.Join(", ", x.Artists.Select(a => a.Name)),
                IcoPath = await _client.GetArtworkAsync(x),
                Action = _ =>
                {
                    _client.Play(x.Uri);
                    return true;
                }
            }).ToArray();

            await Task.WhenAll(results);
            return searchResults.Any() ? results.Select(x => x.Result).ToList() : NothingFoundResult;
        }

        private async Task<List<Result>> SearchArtist(string param)
        {
            if (!_client.ApiConnected) return AuthenticateResult;

            if (string.IsNullOrWhiteSpace(param))
            {
                return SingleResultInList("sp artist {artist name}", "Search for an Artist to play.");
            }

            //Get first page of results
            var searchResults = _client.GetArtists(param).Result;
            var results = searchResults.Select(async x => new Result()
            {
                Title = x.Name,
                SubTitle = $"Popularity: {x.Popularity}%",
                IcoPath = await _client.GetArtworkAsync(x),
                // When selected, open it with the spotify client
                Action = _ =>
                {
                    _client.Play(x.Uri);
                    return true;
                }
            });

            await Task.WhenAll(results);
            return searchResults.Any() ? results.Select(x => x.Result).ToList() : NothingFoundResult;
        }
        private async Task<List<Result>> SearchPlaylist(string param)
        {
            if (!_client.ApiConnected) return AuthenticateResult;

            if (string.IsNullOrWhiteSpace(param))
            {
                param = "";
            }

            // Retrieve data and return the first 500 playlists
            var searchResults = await _client.GetPlaylists(param);
            var results = searchResults.Select(async x => new Result()
            {
                Title = x.Name,
                SubTitle = x.Type,
                IcoPath = await _client.GetArtworkAsync(x),
                Action = _ =>
                {
                    _client.Play(x.Uri);
                    return true;
                }
            }).ToArray();

            await Task.WhenAll(results);
            return searchResults.Any() ? results.Select(x => x.Result).ToList() : NothingFoundResult;
        }

        private async Task<List<Result>> GetDevices(string param = null)
        {
            //Retrieve all available devices
            var allDevices = await _client.GetDevicesAsync();
            if (allDevices.Count == 0)
                return SingleResultInList("No devices found on Spotify.", "Reconnect to client", action: ReconnectAction(_client));

            var results = allDevices.Where(device => !device.IsRestricted).Select(x => new Result
            {
                Title = $"{x.Type}  {x.Name}",
                SubTitle = x.IsActive ? "Active Device" : "Inactive",
                //TODO: Add computer and phone icons
                //IcoPath = await _client.GetArtworkAsync(x.Images,x.Uri),
                IcoPath = SpotifyIcon,
                Action = (a) =>
                {
                    _ = _client.SetDevice(x.Id);
                    return true;
                }
            }).ToList();

            return results.Any() ? results : NothingFoundResult;
        }


        private async Task ReconnectAsync(bool keepRefreshToken = true)
        {
            if (authSemaphore.CurrentCount == 0)
            {
                await authSemaphore.WaitAsync();
                authSemaphore.Release();
                return;
            }
            await authSemaphore.WaitAsync();
            await _client.ConnectWebClient(keepRefreshToken);
            currentUserId = await _client.GetUserIdAsync();
            authSemaphore.Release();
        }

        //Return a generic reconnection action
        private Action ReconnectAction(SpotifyPluginClient client, bool keepRefreshToken = true)
        {
            // ReSharper disable once AsyncVoidLambda
            return async () =>
            {
                //Assign client ID asynchronously when connection finishes
                try
                {
                    await ReconnectAsync(keepRefreshToken);
                    _context.API.ChangeQuery(_context.CurrentPluginMetadata.ActionKeywords[0] + " ", true);
                }
                catch
                {
                    Console.WriteLine("Failed to write client ID");
                }
            };
        }

        private List<Result> AuthenticateResult =>
            SingleResultInList(
                "Authentication required to search the Spotify library",
                "Click this to authenticate",
                action: ReconnectAction(_client));


        // Returns a SingleResult if no search results are found
        private List<Result> NothingFoundResult =>
            SingleResultInList("No results found on Spotify.", "Please try refining your search", action: () => { });

        // Returns a list with a single result
        private List<Result> SingleResultInList(
            string title,
            string subtitle = "",
            string icoPath = SpotifyIcon,
            Action action = default,
            bool hideAfterAction = true,
            bool requery = true)
            =>
            new List<Result>
            {
                new ()
                {
                    Title = title,
                    SubTitle = subtitle,
                    IcoPath = icoPath,
                    Action = _ =>
                    {
                        action?.Invoke();

                        if (requery)
                            RefreshDisplayInfo();

                        return hideAfterAction;
                    }
                }
            };

        private async Task<List<Result>> QueueSearch(string param)
        {
            if (!_client.ApiConnected) return AuthenticateResult;

            if (string.IsNullOrWhiteSpace(param))
            {
                return SingleResultInList("sp queue {trackname}", "Search for a track to add it to your play queue.");
            }

            var results = await SearchTrack(param, true);

            return results.Any() ? results : NothingFoundResult;
        }
            
        private void RefreshDisplayInfo() => _context.API.ChangeQuery(currentQuery, true);
    }
}
