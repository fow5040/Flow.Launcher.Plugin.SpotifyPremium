using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using SpotifyAPI.Web.Models;

namespace Wox.Plugin.Spotify
{
    public class SpotifyPlugin : IPlugin
    {
        private PluginInitContext _context;

        private SpotifyApi _api;

        private readonly Dictionary<string, Func<string, List<Result>>> _terms = new Dictionary<string, Func<string, List<Result>>>();

        private const string SpotifyIcon = "icon.png";
        private bool _isReady;

        public void Init(PluginInitContext context)
        {
            _context = context;

            // initialize data, passing it the plugin directory
            Task.Run(() =>
            {
                _api = new SpotifyApi(_context.CurrentPluginMetadata.PluginDirectory);
                _isReady = true;
            });

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

        private List<Result> Pause(string arg)
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

            return new List<Result>()
            {
                new Result()
                {
                    Title = t.TrackResource.Name,
                    SubTitle = $"{status} | by {t.ArtistResource.Name}",
                    IcoPath = _api.GetArtwork(t),
                    Action = context => true
                }
            };
        }

        private List<Result> ToggleMute(string arg)
        {
            var toggleAction = _api.IsMuted ? "unmute" : "mute";

            return new List<Result>
            {
                new Result()
                {
                    Title = "Toggle Mute",
                    SubTitle = $"Select to {toggleAction} Spotify",
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
            if (!_isReady)
            {
                return new List<Result>
                {
                    new Result("Spotify plugin is initializing", SpotifyIcon, "please try again soon...")
                };
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
                new Result { Title = "No results found on Spotify.", IcoPath = SpotifyIcon, Action = context => false }
            };
        }

        private List<Result> SearchTrack(string param)
        {
            if (string.IsNullOrWhiteSpace(param))
            {
                return new List<Result>();
            }

            // Retrieve data and return the first 20 results
            var fullTracks = _api.GetTracks(param);
            var results = new List<Result>();
            foreach (var x in fullTracks)
            {
                var artists = x.Artists.Select(a => a.Name);
                var result = new Result
                {
                    SubTitle = "Artist: " + string.Join(", ", artists),
                    Title = x.Name,
                    IcoPath = _api.GetArtwork(x),
                    Action = _ =>
                    {
                        _api.Play(x.Uri);
                        return true;
                    }
                };
                results.Add(result);
            }
            return results;
        }

        private List<Result> SearchAlbum(string param)
        {
            if (string.IsNullOrWhiteSpace(param))
            {
                return new List<Result>();
            }

            // Retrieve data and return the first 10 results
            var result = _api.GetAlbums(param).Select(x => new Result()
            {
                Title = x.Name,
                IcoPath = _api.GetArtwork(x),
                Action = _ =>
                {
                    _api.Play(x.Uri);
                    return true;
                }                
            }).ToList();
            return result;
        }

        private List<Result> SearchArtist(string param)
        {
            if (string.IsNullOrWhiteSpace(param))
            {
                return new List<Result>();
            }

            // Retrieve data and return the first 10 results
            var results = _api.GetArtists(param).Select(x => new Result()
            {
                Title = x.Name,
                SubTitle = $"Popularity: {x.Popularity}%",
                IcoPath = _api.GetArtwork(x),
                // When selected, open it with the spotify client
                Action = _ =>
                {
                    _api.Play(x.Uri);
                    return true;
                }
            }).ToList();
            return results;
        }
    }
}