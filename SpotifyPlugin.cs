using System.Collections.Generic;
using System.Linq;
using Wox.Plugin.Spotify.MetadataApi;

namespace Wox.Plugin.Spotify
{
    public class SpotifyPlugin : IPlugin
    {
        private PluginInitContext _context;

        private ApiData data;

        public void Init(PluginInitContext context)
        {
            this._context = context;

            // initialize data, passing it the plugin directory
            data = new ApiData(_context.CurrentPluginMetadata.PluginDirecotry);
        }

        public List<Result> Query(Query query)
        {
            var param = query.GetAllRemainingParameter();

            // check if this is an album or artist search, default to track search
            switch (query.ActionParameters[0])
            {
                case "artist":
                    param = param.Substring("artist ".Length);
                    // Retrieve data and return the first 10 results
                    return data.GetArtists(param).Artists.ToList().GetRange(0, 10).Select(x => new Result()
                        {
                            Title = x.Name,
                            SubTitle = string.Format("Popularity: {0}%", System.Convert.ToDouble(x.Popularity)*100 ),
                            // When selected, open it with the spotify client
                            Action = e => _context.ShellRun(x.Href),
                            IcoPath = "icon.png"
                        }).ToList();
                case "album":
                    param = param.Substring("album ".Length);
                    // Retrieve data and return the first 10 results
                    return data.GetAlbums(param).Albums.ToList().GetRange(0, 10).Select(x => new Result()
                        {
                            Title = x.Name,
                            SubTitle = "Artist: " + string.Join(", ", x.Artists.Select(a => a.Name).ToArray()),
                            // When selected, open it with the spotify client
                            Action = e => _context.ShellRun(x.Href),
                            IcoPath = data.GetArtwork(x.Href)
                        }).ToList();
                default:
                    if (query.ActionParameters[0] == "track")
                        param = param.Substring("track ".Length);
                    // Retrieve data and return the first 20 results
                    return data.GetTracks(param).Tracks.ToList().GetRange(0, 20).Select(x => new Result()
                        {
                            Title = x.Name,
                            SubTitle = "Artist: " + string.Join(", ", x.Artists.Select(a => a.Name).ToArray()),
                            // When selected, open it with the spotify client
                            Action = e => _context.ShellRun(x.Href),
                            IcoPath = "icon.png"
                        }).ToList();
            }
        }
    }
}