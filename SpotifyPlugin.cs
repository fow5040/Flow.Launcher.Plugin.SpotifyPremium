using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SpotifyAPI.Web;

namespace Flow.Launcher.Plugin.SpotifyPremium
{
    public class SpotifyPlugin : IAsyncPlugin, ISettingProvider
    {
        private PluginInitContext _context;
        private SpotifyPluginClient _client;
        private SpotifySettings _settings;

        private readonly Dictionary<string, Func<string, Task<List<Result>>>> _terms = new(StringComparer.InvariantCultureIgnoreCase);
        private readonly Dictionary<string, Func<string, Task<List<Result>>>> _expensiveTerms = new(StringComparer.InvariantCultureIgnoreCase);

        private const string SpotifyIcon = "icon.png";
        private string currentUserId;
        private string currentQuery;
        private int cachedVolume = -1;

        private SemaphoreSlim authSemaphore = new SemaphoreSlim(1, 1);

        public System.Windows.Controls.Control CreateSettingPanel()
        {
            return new SpotifySettingsUserControl(_settings);
        }

        public Task InitAsync(PluginInitContext context)
        {
            _context = context;
            _settings = _context.API.LoadSettingJsonStorage<SpotifySettings>();

            if (_settings == null)
            {
                _settings = new SpotifySettings();
            }

            _client = new SpotifyPluginClient(context.API, _context.CurrentPluginMetadata.PluginDirectory);

            _expensiveTerms.Add("artist", SearchArtist);
            _expensiveTerms.Add("album", SearchAlbum);
            _expensiveTerms.Add("track", SearchTrack);
            _expensiveTerms.Add("playlist", SearchPlaylist);
            _expensiveTerms.Add("device", GetDevices);
            _expensiveTerms.Add("queue", QueueSearch);
            _expensiveTerms.Add("like", SearchLikeTrack);

            _terms.Add("next", PlayNext);
            _terms.Add("last", PlayLast);
            _terms.Add("pause", Pause);
            _terms.Add("play", Play);
            _terms.Add("mute", ToggleMute);
            _terms.Add("vol", SetVolume);
            _terms.Add("volume", SetVolume);
            _terms.Add("shuffle", ToggleShuffle);
            _terms.Add("repeat", ToggleRepeat);
            _terms.Add("unlike", UnlikeCurrentSong);

            _terms.Add("diag", q =>
                Task.FromResult(SingleResultInList(
                    $"Query Count: {context.CurrentPluginMetadata.QueryCount}",
                    $"Avg. Query Time: {context.CurrentPluginMetadata.AvgQueryTime}ms",
                    action: null)));

            _terms.Add("reconnect", q =>
                Task.FromResult(SingleResultInList(
                    "Reconnect",
                    "Force a reconnection and remove the refresh token",
                    action: ReconnectAction(_client, false))));

            return Task.CompletedTask;
        }

        private async Task<List<Result>> Play(string arg)
        {
            var name = await _client.GetCurrentPlaybackNameAsync();
            return SingleResultInList("Play", $"Resume: {name}", action: async () => await _client.PlayAsync());
        }

        private async Task<List<Result>> Pause(string arg = null)
        {
            var name = await _client.GetCurrentPlaybackNameAsync();
            return SingleResultInList("Pause", $"Pause: {name}", action: async () => await _client.PauseAsync());
        }

        private async Task<List<Result>> PlayNext(string arg)
        {
            var name = await _client.GetCurrentPlaybackNameAsync();
            return SingleResultInList("Next", $"Skip: {name}", action: async () => await _client.SkipAsync());
        }

        private async Task<List<Result>> PlayLast(string arg)
        {
            return SingleResultInList("Last", "Skip Backwards", action: async () => await _client.SkipBackAsync());
        }

        public async Task<List<Result>> QueryAsync(Query query, CancellationToken token)
        {
            currentQuery = query.RawQuery;

            if (!_client.RefreshTokenAvailable())
            {
                return SingleResultInList(
                    "Require Authentication", "Select to authorize",
                    action: ReconnectAction(_client),
                    hideAfterAction: false);
            }

            if (!_client.ApiConnected || !await _client.CheckTokenValidityAsync())
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
                if (!string.IsNullOrWhiteSpace(query.FirstSearch) && _terms.ContainsKey(query.FirstSearch))
                {
                    return await _terms[query.FirstSearch].Invoke(query.SecondToEndSearch);
                }

                if (string.IsNullOrWhiteSpace(query.Search))
                {
                    await Task.Delay(150, token);
                    if (token.IsCancellationRequested)
                        return null;

                    return await GetPlaying();
                }

                if (_settings.OptimizeClientUsage)
                {
                    await Task.Delay(_settings.OptimizeClientKeyDelay, token);
                    if (token.IsCancellationRequested)
                        return null;
                }

                if (_expensiveTerms.ContainsKey(query.FirstSearch))
                {
                    return await _expensiveTerms[query.FirstSearch].Invoke(query.SecondToEndSearch);
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
                return SingleResultInList(
                    "No active device", "Select device with `sp device`",
                    action: () =>
                    {
                        _context.API.ChangeQuery($"{_context.CurrentPluginMetadata.ActionKeywords[0]} device");
                    },
                    hideAfterAction: false);
            }

            var playbackContext = await _client.GetPlaybackContextAsync();
            var item = playbackContext?.Item;

            var t = item as FullTrack;
            var e = item as FullEpisode;

            var status = (playbackContext != null && playbackContext.IsPlaying) ? "Now Playing" : "Paused";
            var toggleAction = (playbackContext != null && playbackContext.IsPlaying) ? "Pause" : "Resume";

            var icon = t != null ? _client.GetArtworkAsync(t) :
                e != null ? _client.GetArtworkAsync(e) :
                null;

            string iconResult = icon != null ? await icon : SpotifyIcon;

            return new List<Result>()
            {
                SingleResultInList(
                    t?.Name ?? e?.Name ?? "Not Available",
                    $"{status} | by {(t != null ? string.Join(", ", t.Artists.Select(a => a.Name)) : string.Empty)}",
                    iconResult).First(),
                SingleResultInList(
                    "Pause / Resume",
                    $"{toggleAction}: {t?.Name ?? e?.Name}",
                    action: async () =>
                        {
                            if (playbackContext != null && playbackContext.IsPlaying)
                                await _client.PauseAsync();
                            else
                                await _client.PlayAsync();
                        },
                    hideAfterAction: true).First(),
                (await PlayNext(string.Empty)).First(),
                (await PlayLast(string.Empty)).First(),
                (await ToggleMute()).First(),
                (await ToggleShuffle()).First(),
                (await ToggleRepeat()).First(),
                (await SetVolume()).First()
            };
        }

        private async Task<List<Result>> ToggleMute(string arg = null)
        {
            var muteStatus = await _client.GetMuteStatusAsync();
            var toggleAction = muteStatus ? "Unmute" : "Mute";
            var name = await _client.GetCurrentPlaybackNameAsync();
            return SingleResultInList("Toggle Mute", $"{toggleAction}: {name}", action: async () => await _client.ToggleMuteAsync());
        }

        private async Task<List<Result>> ToggleRepeat(string arg = null)
        {
            var currentRepeatStatus = await _client.GetRepeatStatusAsync();
            var nextRepeatStatus = _client.GetNextRepeatAction(currentRepeatStatus);
            var toggleAction = nextRepeatStatus switch
            {
                PlayerSetRepeatRequest.State.Off => "Repeat Off",
                PlayerSetRepeatRequest.State.Track => "Repeat Current Track",
                PlayerSetRepeatRequest.State.Context => "Repeat Current Playlist",
                _ => "Unknown repeat status"
            };
            var name = await _client.GetCurrentPlaybackNameAsync();
            return SingleResultInList("Toggle Repeat", $"{toggleAction}: {name}", action: async () => await _client.ToggleRepeatAsync());
        }

        private struct SetVolAction
        {
            public enum VolAction { DISPLAY, ABSOLUTE, DECREASE, INCREASE }
            public VolAction action;
            public int target;
            public int current;
            public bool validAction;

            public SetVolAction(string actionString, int current)
            {
                this.validAction = false;
                this.target = -1;
                this.current = current;
                if (string.IsNullOrWhiteSpace(actionString))
                {
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

                    if (this.target is >= 0 and <= 100)
                    {
                        this.validAction = true;
                        return;
                    }
                }
                this.action = VolAction.DISPLAY;
            }
        }

        private async Task<List<Result>> SetVolume(string arg = null)
        {
            cachedVolume = await _client.GetCurrentVolumeAsync();
            SetVolAction volAction = new SetVolAction(arg, cachedVolume);

            if (volAction.validAction)
            {
                return SingleResultInList(
                    $"Set Volume to {volAction.target}",
                    $"Current Volume: {cachedVolume}",
                    action: async () => { await _client.SetVolumeAsync(volAction.target); });
            }

            return SingleResultInList($"Volume", $"Current Volume: {cachedVolume}", action: null);
        }

        private async Task<List<Result>> ToggleShuffle(string arg = null)
        {
            var shuffleStatus = await _client.GetShuffleStatusAsync();
            var toggleAction = shuffleStatus ? "Off" : "On";
            return SingleResultInList("Toggle Shuffle", $"Turn Shuffle {toggleAction}", action: async () => await _client.ToggleShuffleAsync());
        }

        private async Task<List<Result>> AddLikeCurrentSong(string arg = null)
        {
            var currentSong = await _client.GetCurrentPlaybackNameAsync();
            return SingleResultInList("Like", $"Add '{currentSong}' to liked songs", action: async () => await _client.AddLikeCurrentSongAsync());
        }

        private async Task<List<Result>> UnlikeCurrentSong(string arg = null)
        {
            var currentSong = await _client.GetCurrentPlaybackNameAsync();
            return SingleResultInList("Remove", $"Remove '{currentSong}' from liked songs", action: async () => await _client.UnlikeCurrentSongAsync());
        }

        private async Task<List<Result>> SearchAllAsync(string param)
        {
            if (!_client.ApiConnected) return AuthenticateResult;

            if (string.IsNullOrWhiteSpace(param))
            {
                return SingleResultInList("sp {any search term}", "Perform a full search on albums, tracks, artists, and playlists.");
            }

            var searchResults = await _client.SearchAll(param);
            var results = searchResults.Select(async x => new Result()
            {
                Title = x.Title,
                SubTitle = x.Subtitle,
                IcoPath = await _client.GetArtworkAsync(x),
                Action = _ =>
                {
                    Task.Run(async () => await _client.PlayAsync(x.Uri));
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

            var searchResults = await _client.GetTracks(param);
            var results = searchResults.Select(async x => new Result()
            {
                Title = x.Name,
                SubTitle = (shouldQueue ? "Queue track by " : "") + "Artist: " + string.Join(", ", x.Artists.Select(a => a.Name)),
                IcoPath = await _client.GetArtworkAsync(x),
                Action = _ =>
                {
                    if (shouldQueue)
                        Task.Run(async () => await _client.EnqueueAsync(x.Uri));
                    else
                        Task.Run(async () => await _client.PlayAsync(x.Uri));
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

            var searchResults = await _client.GetAlbums(param);
            var results = searchResults.Select(async x => new Result()
            {
                Title = x.Name,
                SubTitle = "by " + string.Join(", ", x.Artists.Select(a => a.Name)),
                IcoPath = await _client.GetArtworkAsync(x),
                Action = _ =>
                {
                    Task.Run(async () => await _client.PlayAsync(x.Uri));
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

            var searchResults = await _client.GetArtists(param);
            var results = searchResults.Select(async x => new Result()
            {
                Title = x.Name,
                SubTitle = $"Popularity: {x.Popularity}%",
                IcoPath = await _client.GetArtworkAsync(x),
                Action = _ =>
                {
                    Task.Run(async () => await _client.PlayAsync(x.Uri));
                    return true;
                }
            }).ToArray();

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

            var searchResults = await _client.GetPlaylists(param);
            var results = searchResults.Select(async x => new Result()
            {
                Title = x.Name,
                SubTitle = x.Type,
                IcoPath = await _client.GetArtworkAsync(x),
                Action = _ =>
                {
                    Task.Run(async () => await _client.PlayAsync(x.Uri));
                    return true;
                }
            }).ToArray();

            await Task.WhenAll(results);
            return searchResults.Any() ? results.Select(x => x.Result).ToList() : NothingFoundResult;
        }

        private async Task<List<Result>> SearchLikeTrack(string param)
        {
            if (!_client.ApiConnected) return AuthenticateResult;

            if (string.IsNullOrWhiteSpace(param))
            {
                var currentSongName = await _client.GetCurrentPlaybackNameAsync();
                var currentSongId = await _client.GetCurrentPlaybackIdAsync();
                var currentSongIsLiked = await _client.CheckLikedByIdAsync(currentSongId);
                var subtitle = currentSongIsLiked
                    ? $"Remove '{currentSongName}' from liked songs"
                    : $"Add '{currentSongName}' to liked songs";
                return SingleResultInList("Like", subtitle, action: async () => await _client.ToggleLikeCurrentSongAsync());
            }

            var searchResults = await _client.GetTracks(param);
            var results = searchResults.Select(async x => new Result()
            {
                Title = x.Name,
                SubTitle = await _client.CheckLikedByIdAsync(x.Id)
                    ? $"Remove '{x.Name}' by {string.Join(", ", x.Artists.Select(a => a.Name))} from liked songs"
                    : $"Add '{x.Name}' by {string.Join(", ", x.Artists.Select(a => a.Name))} to liked songs",
                IcoPath = await _client.GetArtworkAsync(x),
                Action = _ =>
                {
                    Task.Run(async () => await _client.ToggleLikeByIdAsync(x.Id));
                    return true;
                }
            }).ToArray();

            await Task.WhenAll(results);
            return results.Any() ? results.Select(x => x.Result).ToList() : NothingFoundResult;
        }

        private async Task<List<Result>> GetDevices(string param = null)
        {
            var allDevices = await _client.GetDevicesAsync();
            if (allDevices.Count == 0)
                return SingleResultInList("No devices found on Spotify.", "Reconnect to client", action: () => { Task.Run(async () => await ReconnectAsync()); });

            var results = allDevices.Where(device => !device.IsRestricted).Select(x => new Result
            {
                Title = $"{x.Type}  {x.Name}",
                SubTitle = x.IsActive ? "Active Device" : "Inactive",
                IcoPath = SpotifyIcon,
                Action = (a) =>
                {
                    Task.Run(async () => await _client.SetDevice(x.Id));
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
            try
            {
                await _client.ConnectWebClient(keepRefreshToken);

                if (!_client.ApiConnected)
                {
                    return;
                }

                currentUserId = await _client.GetUserIdAsync();
            }
            finally
            {
                authSemaphore.Release();
            }
        }

        private Action ReconnectAction(SpotifyPluginClient client, bool keepRefreshToken = true)
        {
            return () =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        await ReconnectAsync(keepRefreshToken);
                        _context.API.ChangeQuery(_context.CurrentPluginMetadata.ActionKeywords[0] + " ", true);
                    }
                    catch (Exception ex)
                    {
                        _context.API.LogException(nameof(SpotifyPlugin), "Failed to write client ID", ex);
                    }
                });
            };
        }

        private List<Result> AuthenticateResult =>
            SingleResultInList(
                "Authentication required to search the Spotify library",
                "Click this to authenticate",
                action: ReconnectAction(_client));

        private List<Result> NothingFoundResult =>
            SingleResultInList("No results found on Spotify.", "Please try refining your search", action: null);

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