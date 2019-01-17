using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Wox.Plugin.Spotify
{
    public class SpotifyPlugin : IPlugin
    {
        private PluginInitContext _context;

        private SpotifyApi _api;

        private readonly Dictionary<string, Func<string, List<Result>>> _terms = new Dictionary<string, Func<string, List<Result>>>();

        private const string SpotifyIcon = "icon.png";

        public void Init(PluginInitContext context)
        {
            _context = context;

            // initialize data, passing it the plugin directory
            Task.Run(() => _api = new SpotifyApi(_context.CurrentPluginMetadata.PluginDirectory));

            _terms.Add("artist", SearchArtist);
            _terms.Add("album", SearchAlbum);
            _terms.Add("track", SearchTrack);
            _terms.Add("next", PlayNext);
            _terms.Add("pause", Pause);
            _terms.Add("play", Play);
            _terms.Add("mute", ToggleMute);
        }

        private List<Result> Play(string arg) =>
            SingleResult("Play", $"Resume: {_api.PlaybackContext.Item.Name}", _api.Play);

        private List<Result> Pause(string arg = null) =>
            SingleResult("Pause", $"Pause: {_api.PlaybackContext.Item.Name}", _api.Pause);

        private List<Result> PlayNext(string arg) =>
            SingleResult("Next", $"Skip: {_api.PlaybackContext.Item.Name}", _api.Skip);

        private List<Result> GetPlaying()
        {
            var t = _api.PlaybackContext.Item;

            if (t == null)
            {
                return SingleResult("No track playing");
            }

            var status = _api.PlaybackContext.IsPlaying ? "Now Playing" : "Paused";
            var toggleAction = _api.PlaybackContext.IsPlaying ? "Pause" : "Resume";
            var icon = _api.GetArtworkAsync(t);
            icon.Wait();

            return new List<Result>()
            {
                new Result()
                {
                    Title = t.Name,
                    SubTitle = $"{status} | by {String.Join(", ",t.Artists.Select(a => String.Join("",a.Name)))}",
                    IcoPath = icon.Result
                },
                new Result()
                {
                    IcoPath = SpotifyIcon,
                    Title = "Pause / Resume",
                    SubTitle = $"{toggleAction}: {t.Name}",
                    Action = _ =>
                    {
                        if (_api.PlaybackContext.IsPlaying)
                            _api.Pause();
                        else
                            _api.Play();
                        return true;
                    }
                },
                new Result()
                {
                    IcoPath = SpotifyIcon,
                    Title = "Next",
                    SubTitle = $"Skip: {t.Name}",
                    Action = context =>
                    {
                        _api.Skip();
                        return true;
                    }
                },
                ToggleMute().First()
            };
        }

        private List<Result> ToggleMute(string arg = null)
        {
            var toggleAction = _api.IsMuted ? "Unmute" : "Mute";

            return SingleResult("Toggle Mute", $"{toggleAction}: {_api.PlaybackContext.Item.Name}", _api.ToggleMute);
        }

        public List<Result> Query(Query query)
        {
            if (!_api.IsApiConnected)
            {
                return SingleResult("Spotify API unreachable", "Select to re-authorize", () =>
                {
                    _api.ConnectWebApi();
                    _context.API.ChangeQuery("");
                });
            }

            try
            {
                // display status if no parameters are added
                if (string.IsNullOrWhiteSpace(query.Search))
                {
                    return GetPlaying();
                }

                if (_terms.ContainsKey(query.FirstSearch))
                {
                    var results = _terms[query.FirstSearch].Invoke(query.SecondToEndSearch);
                    return results;
                }

                return SearchTrack(query.Search);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return SingleResult("No results found on Spotify.");
        }

        private List<Result> SearchTrack(string param)
        {
            if (!_api.IsApiConnected) return AuthenticateResult;

            if (string.IsNullOrWhiteSpace(param))
            {
                return new List<Result>();
            }

            // Retrieve data and return the first 20 results
            var results = _api.GetTracks(param).Select(async x => new Result()
            {
                Title = x.Name,
                SubTitle = "Artist: " + string.Join(", ", x.Artists.Select(a => a.Name)),
                IcoPath = await _api.GetArtworkAsync(x),
                Action = _ =>
                {
                    _api.Play(x.Uri);
                    return true;
                }
            }).ToArray();

            Task.WaitAll(results);
            return results.Select(x => x.Result).ToList();
        }

        private List<Result> SearchAlbum(string param)
        {
            if (!_api.IsApiConnected) return AuthenticateResult;

            if (string.IsNullOrWhiteSpace(param))
            {
                return new List<Result>();
            }

            // Retrieve data and return the first 10 results
            var results = _api.GetAlbums(param).Select(async x => new Result()
            {
                Title = x.Name,
                SubTitle = "by " + string.Join(", ", x.Artists.Select(a => a.Name)),
                IcoPath = await _api.GetArtworkAsync(x),
                Action = _ =>
                {
                    _api.Play(x.Uri);
                    return true;
                }                
            }).ToArray();

            Task.WaitAll(results);
            return results.Select(x => x.Result).ToList();
        }

        private List<Result> SearchArtist(string param)
        {
            if (!_api.IsApiConnected) return AuthenticateResult;

            if (string.IsNullOrWhiteSpace(param))
            {
                return new List<Result>();
            }

            // Retrieve data and return the first 10 results
            var results = _api.GetArtists(param).Select(async x => new Result()
            {
                Title = x.Name,
                SubTitle = $"Popularity: {x.Popularity}%",
                IcoPath = await _api.GetArtworkAsync(x),
                // When selected, open it with the spotify client
                Action = _ =>
                {
                    _api.Play(x.Uri);
                    return true;
                }
            }).ToArray();

            Task.WaitAll(results);
            return results.Select(x => x.Result).ToList();
        }

        private List<Result> AuthenticateResult =>
            SingleResult("Authentication required to search the Spotify library", "Click this to authenticate", () =>
                {
                    // This will prompt the user to authenticate
                    var t = new System.Threading.Thread(async () => await _api.ConnectWebApi());
                    t.Start();
                });

        // Returns a list with a single result
        private List<Result> SingleResult(string title, string subtitle = "", Action action = default(Action)) =>
            new List<Result>()
            {
                new Result()
                {
                    Title = title,
                    SubTitle = subtitle,
                    IcoPath = SpotifyIcon,
                    Action = _ =>
                    {
                        action();
                        return true;
                    }
                }
            };
    }
}