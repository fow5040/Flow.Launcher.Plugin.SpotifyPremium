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

        private List<Result> Play(string arg)
        {
            return new List<Result>()
            {
                new Result()
                {
                    IcoPath = SpotifyIcon,
                    Title = "Play",
                    SubTitle = $"Resume: {_api.CurrentTrack.TrackResource.Name}",
                    Action = context =>
                    {
                        _api.Play();
                        return true;
                    }
                }
            };
        }

        private List<Result> Pause(string arg = null)
        {
            return new List<Result>()
            {
                new Result()
                {
                    IcoPath = SpotifyIcon,
                    Title = "Pause",
                    SubTitle = $"Pause: {_api.CurrentTrack.TrackResource.Name}",
                    Action = context =>
                    {
                        _api.Pause();
                        return true;
                    }
                }
            };
        }

        private List<Result> PlayNext(string arg)
        {
            return new List<Result>()
            {
                new Result()
                {
                    IcoPath = SpotifyIcon,
                    Title = "Next",
                    SubTitle = $"Skip: {_api.CurrentTrack.TrackResource.Name}",
                    Action = context =>
                    {
                        _api.Skip();
                        return true;
                    }
                }
            };
        }

        private List<Result> GetPlaying()
        {
            var t = _api.CurrentTrack;

            if (t == null)
            {
                return new List<Result>()
                {
                    new Result()
                    {
                        Action = context => true,
                        Title = "No track playing",
                        IcoPath = SpotifyIcon
                    }
                };
            }

            var status = _api.IsPlaying ? "Now Playing" : "Paused";
            var toggleAction = _api.IsPlaying ? "Pause" : "Resume";
            var icon = _api.GetArtworkAsync(t);
            icon.Wait();

            return new List<Result>()
            {
                new Result()
                {
                    Title = t.TrackResource.Name,
                    SubTitle = $"{status} | by {t.ArtistResource.Name}",
                    IcoPath = icon.Result
                },
                new Result()
                {
                    IcoPath = SpotifyIcon,
                    Title = "Pause / Resume",
                    SubTitle = $"{toggleAction}: {t.TrackResource.Name}",
                    Action = _ =>
                    {
                        if (_api.IsPlaying)
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
                    SubTitle = $"Skip: {t.TrackResource.Name}",
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

            return new List<Result>
            {
                new Result()
                {
                    Title = "Toggle Mute",
                    SubTitle = $"{toggleAction}: {_api.CurrentTrack.TrackResource.Name}",
                    IcoPath = SpotifyIcon,
                    Action = context =>
                    {
                        _api.ToggleMute();
                        return true;
                    }
                }
            };
        }

        public List<Result> Query(Query query)
        {
            if (!_api.IsRunning)
            {
                return new List<Result>
                {
                    new Result() {
                        Title = "Spotify is not running",
                        SubTitle = "select to open Spotify",
                        IcoPath = SpotifyIcon,
                        Action = _ =>
                        {
                            _api.RunSpotify();
                            _context.API.ChangeQuery("");
                            return true;
                        }
                    }
                };
            }
            else if (!_api.IsConnected)
            {
                _api.ConnectToSpotify();
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
            return new List<Result>()
            {
                new Result() {
                    Title = "No results found on Spotify.",
                    IcoPath = SpotifyIcon
                }
            };
        }

        private List<Result> SearchTrack(string param)
        {
            if (!_api.IsWebApiCOnnected) return AuthenticateResult;

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
            if (!_api.IsWebApiCOnnected) return AuthenticateResult;

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
            if (!_api.IsWebApiCOnnected) return AuthenticateResult;

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

        private List<Result> AuthenticateResult => new List<Result>()
        {
            new Result()
            {
                Title = "Authentication required to search the Spotify library",
                SubTitle = "Click this to authenticate",
                IcoPath = SpotifyIcon,
                Action = _ => {
                    // This will prompt the user to authenticate
                    var t = new System.Threading.Thread(async () => {
                        await _api.ConnectWebApi();
                    });
                    t.Start();

                    return true;
                }
            }
        };
    }
}