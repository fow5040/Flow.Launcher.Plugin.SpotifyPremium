using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SpotifyAPI.Web;

namespace Wox.Plugin.SpotifyPremium
{
    public class SpotifyPlugin : IPlugin
    {
        private PluginInitContext _context;

        private SpotifyPluginClient _client;

        private readonly Dictionary<string, Func<string, List<Result>>> _terms = new Dictionary<string, Func<string, List<Result>>>();

        private const string SpotifyIcon = "icon.png";

        private string currentUserId; //Required for playlist querying

        private bool optimizeclientUsage = true;   //Flag to limit client calls to X ms after a keystroke 
                                                //Set to 'false' to stop optimizing client calls

        private DateTime lastQueryTime; //Record the time on every query
                                        //Almost every keypress counts as a new query

        private int optimzeclientKeyDelay = 500; //Time to wait before issuing an expensive query
        private int cachedVolume = -1;

        
        private String[] expensiveSearchTerms = {"artist","album","track","playlist", "queue"};  //Specify expensive search terms for optimizing client usage
                                                                                        //Wait for delay before querying 

        public void Init(PluginInitContext context)
        {
            _context = context;
            lastQueryTime = DateTime.UtcNow;

            // initialize data, passing it the plugin directory
            Task.Run(() => _client = new SpotifyPluginClient(_context.CurrentPluginMetadata.PluginDirectory));

            _terms.Add("artist", SearchArtist);
            _terms.Add("album", SearchAlbum);
            _terms.Add("playlist", SearchPlaylist);
            _terms.Add("track", SearchTrack);
            _terms.Add("next", PlayNext);
	        _terms.Add("last", PlayLast);
            _terms.Add("pause", Pause);
            _terms.Add("play", Play);
            _terms.Add("mute", ToggleMute);
            _terms.Add("vol", SetVolume);
            _terms.Add("volume", SetVolume);
            _terms.Add("device", GetDevices);
            _terms.Add("shuffle", ToggleShuffle);
            _terms.Add("queue", QueueSearch);

            //view query count and average query duration
            _terms.Add("diag", q =>
                SingleResult($"Query Count: {context.CurrentPluginMetadata.QueryCount}",
                $"Avg. Query Time: {context.CurrentPluginMetadata.AvgQueryTime}ms",
                null));

            _terms.Add("reconnect", q =>
                SingleResult("Reconnect","Force a reconnection and remove the refresh token",reconnectAction(_client, false))
                );
        }

        private List<Result> Play(string arg) =>
            SingleResult("Play", $"Resume: {_client.CurrentPlaybackName}", _client.Play);

        private List<Result> Pause(string arg = null) =>
            SingleResult("Pause", $"Pause: {_client.CurrentPlaybackName}", _client.Pause);

        private List<Result> PlayNext(string arg) =>
            SingleResult("Next", $"Skip: {_client.CurrentPlaybackName}", _client.Skip);

        private List<Result> PlayLast(string arg) =>
            SingleResult("Last", "Skip Backwards", _client.SkipBack);

        public List<Result> Query(Query query)
        {
            //Record the time the query was issued
            lastQueryTime = DateTime.UtcNow;
            DateTime thisQueryStartTime = DateTime.UtcNow;

            if (!_client.ApiConnected)
            {
                return SingleResult("Spotify client unreachable", "Select to re-authorize", reconnectAction(_client));
            }

            if (!_client.TokenValid)
            {
                return SingleResult("Spotify client Token Expired", "Select to re-authorize", reconnectAction(_client));
            }

            try
            {
                // display status if no parameters are added
                if (string.IsNullOrWhiteSpace(query.Search))
                {
                    return GetPlaying();
                }
                
                //Run the query if it is not an expensive search term
                if(_terms.ContainsKey(query.FirstSearch) && !expensiveSearchTerms.Contains(query.FirstSearch)){
                    var results = _terms[query.FirstSearch].Invoke(query.SecondToEndSearch);
                    return results;                    
                }

                //If query is expensive, AND if optimizeclientUsage is flagged
                //  return null if query is updated within set number ms
                //  this limits the client calls made
                //  if you type a 10 character query quickly enough, only the last keypress searches the Spotify client
                if(optimizeclientUsage){
                    System.Threading.Thread.Sleep(optimzeclientKeyDelay);
                    if(lastQueryTime > thisQueryStartTime){
                        return null;
                    }
                }

                if (_terms.ContainsKey(query.FirstSearch))
                {
                    var results = _terms[query.FirstSearch].Invoke(query.SecondToEndSearch);
                    return results;
                }

                return SearchAll(query.Search);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            //If searches run into an exception, return results not found Result
            return NothingFoundResult; 
        }

        private List<Result> GetPlaying()
        {
            var d = _client.ActiveDeviceName;
            if (d == null)
            {
                //Must have an active device to control Spotify
                return SingleResult("No active device","Select device with `sp device`",()=>{});
            }

            var item = _client.PlaybackContext.Item;
            if (item == null)
            {
                return SingleResult("Nothing playing",$"Active Device: {d}",()=>{});
            }

            FullTrack t = (item is FullTrack track ? track : null);
            FullEpisode e = (item is FullEpisode episode ? episode : null);

            var status = _client.PlaybackContext.IsPlaying ? "Now Playing" : "Paused";
            var toggleAction = _client.PlaybackContext.IsPlaying ? "Pause" : "Resume";

            // Check if item is a track, episode, or default icon if neither work
            var icon = ( t != null ? _client.GetArtworkAsync(t) : 
                         e != null ? _client.GetArtworkAsync(e) :
                         null );

            return new List<Result>()
            {
                new Result()
                {
                    Title = t.Name,
                    SubTitle = $"{status} | by {String.Join(", ",t.Artists.Select(a => String.Join("",a.Name)))}",
                    IcoPath = (icon != null ? icon.Result : SpotifyIcon)
                },
                new Result()
                {
                    IcoPath = SpotifyIcon,
                    Title = "Pause / Resume",
                    SubTitle = $"{toggleAction}: {t.Name}",
                    Action = _ =>
                    {
                        if (_client.PlaybackContext.IsPlaying)
                            _client.Pause();
                        else
                            _client.Play();
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
                        _client.Skip();
                        return true;
                    }
                },
                new Result()
                {
                    IcoPath = SpotifyIcon,
                    Title = "Last",
                    SubTitle = "Skip backwards",
                    Action = context =>
                    {
                        _client.SkipBack();
                        return true;
                    }
                },
                ToggleMute().First(),
                ToggleShuffle().First(),
                SetVolume().First()
            };
        }

        private List<Result> ToggleMute(string arg = null)
        {
            var toggleAction = _client.MuteStatus ? "Unmute" : "Mute";

            return SingleResult("Toggle Mute", $"{toggleAction}: {_client.CurrentPlaybackName}", _client.ToggleMute);
        }

        private List<Result> SetVolume(string arg = null)
        {
            if (Int32.TryParse(arg, out int tempInt)){
                if (tempInt >= 0 && tempInt <= 100){
                    return SingleResult($"Set Volume to {tempInt}",$"Current Volume: {cachedVolume}", ()=>{
                        _client.SetVolume(tempInt);
                        });
                }
            }

            cachedVolume = _client.CurrentVolume;
            return SingleResult($"Volume", $"Current Volume: {cachedVolume}", ()=>{});
        }

        private List<Result> ToggleShuffle(string arg = null)
        {
            var toggleAction = _client.ShuffleStatus ? "Off" : "On";

            return SingleResult("Toggle Shuffle", $"Turn Shuffle {toggleAction}", _client.ToggleShuffle);
        }

        private List<Result> SearchAll(string param, bool shouldQueue = false)
        {
            if (!_client.ApiConnected) return AuthenticateResult;

            if (string.IsNullOrWhiteSpace(param))
            {
                return new List<Result>();
            }

            // Retrieve data and return the first 20 results
            var searchResults = _client.SearchAll(param).Result;
            var results = searchResults.Select(async x => new Result()
            {
                Title = x.Title,
                SubTitle = (shouldQueue ? "Queue " : "") + x.Subtitle,
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

            Task.WaitAll(results);
            return (results.Count() > 0) ? results.Select(x => x.Result).ToList() : NothingFoundResult;
        }

        private List<Result> SearchTrack(string param)
        {
            if (!_client.ApiConnected) return AuthenticateResult;

            if (string.IsNullOrWhiteSpace(param))
            {
                return new List<Result>();
            }

            // Retrieve data and return the first 20 results
            var searchResults = _client.GetTracks(param).Result;
            var results = searchResults.Select(async x => new Result()
            {
                Title = x.Name,
                SubTitle = "Artist: " + string.Join(", ", x.Artists.Select(a => a.Name)),
                IcoPath = await _client.GetArtworkAsync(x),
                Action = _ =>
                {
                    _client.Play(x.Uri);
                    return true;
                }
            }).ToArray();

            Task.WaitAll(results);
            return (results.Count() > 0) ? results.Select(x => x.Result).ToList() : NothingFoundResult;
        }

        private List<Result> SearchAlbum(string param)
        {
            if (!_client.ApiConnected) return AuthenticateResult;

            if (string.IsNullOrWhiteSpace(param))
            {
                return new List<Result>();
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

            Task.WaitAll(results);
            return (results.Count() > 0) ? results.Select(x => x.Result).ToList() : NothingFoundResult;
        }

        private List<Result> SearchArtist(string param)
        {
            if (!_client.ApiConnected) return AuthenticateResult;

            if (string.IsNullOrWhiteSpace(param))
            {
                return new List<Result>();
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

            return (results.Count() > 0) ? results.Select(x => x.Result).ToList() : NothingFoundResult;
        }
        private List<Result> SearchPlaylist(string param)
        {
            if (!_client.ApiConnected) return AuthenticateResult;

            if (string.IsNullOrWhiteSpace(param))
            {
                param = "";
            }

            // Retrieve data and return the first 500 playlists
            var searchResults = _client.GetPlaylists(param).Result;
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

            Task.WaitAll(results);
            return (results.Count() > 0) ? results.Select(x => x.Result).ToList() : NothingFoundResult;
        }

        private List<Result> GetDevices(string param = null)
        {
            //Retrieve all available devices
            List<Device> allDevices = _client.GetDevices();
            if (allDevices == null || allDevices.Count == 0) return SingleResult("No devices found on Spotify.","Reconnect to client",reconnectAction(_client));

            var results = _client.GetDevices().Where( device => !device.IsRestricted).Select(async x => new Result()
            {
                Title = $"{x.Type}  {x.Name}",
                SubTitle = x.IsActive ? "Active Device" : "Inactive",
                //TODO: Add computer and phone icons
                //IcoPath = await _client.GetArtworkAsync(x.Images,x.Uri),
                IcoPath = SpotifyIcon,
                Action = _ =>
                {
                    _client.SetDevice(x.Id);
                    return true;
                }                
            }).ToArray();

            Task.WaitAll(results);
            return results.Select(x => x.Result).ToList();
        }
        
        //Return a generic reconnection action
        private Action reconnectAction(SpotifyPluginClient client, bool keepRefreshToken = true){
            return () =>
            {
                Task connectTask = client.ConnectWebClient(keepRefreshToken);
                //Assign client ID asynchronously when connection finishes
                connectTask.ContinueWith((connectResult) => { 
                    try{
                        currentUserId = client.UserID;
                    }
                    catch{
                        Console.WriteLine("Failed to write client ID");
                    }
                });
            };
        }

        private List<Result> AuthenticateResult =>
            SingleResult("Authentication required to search the Spotify library", "Click this to authenticate", reconnectAction(_client));



        // Returns a SingleResult if no search results are found
        private List<Result> NothingFoundResult =>
            SingleResult("No results found on Spotify.", "Please try refining your search", () => {});
            
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

        private List<Result> QueueSearch(string param)
        {
            if (!_client.ApiConnected) return AuthenticateResult;

            if (string.IsNullOrWhiteSpace(param))
            {
                return SingleResult("Queue", "Search for a song to queue it up.");
            }

            var results = SearchAll(param, true);

            return (results.Count() > 0) ? results : NothingFoundResult;
        }

    }
}
