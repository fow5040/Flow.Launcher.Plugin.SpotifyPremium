using System;
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
            data = new ApiData(_context.CurrentPluginMetadata.PluginDirectory);
        }

        public List<Result> Query(Query query)
        {
            var param = query.GetAllRemainingParameter();
            var Results = new List<Result>();

            // Avoid exceptions when no parameters are passed
            if (query.ActionParameters.Count == 0 || (new[] { "artist", "album", "track" }.Contains(query.ActionParameters[0]) && query.ActionParameters.Count == 1))
                return Results;

            // check if this is an album or artist search, default to track search
            switch (query.ActionParameters[0])
            {
                case "artist":
                    param = param.Substring("artist ".Length);
                    // Retrieve data and return the first 10 results
                    Results = data.GetArtists(param).Artists.ToList().GetRangeSafe(0, 10).Select(x => new Result()
                        {
                            Title = x.Name,
                            SubTitle = string.Format("Popularity: {0}%", System.Convert.ToDouble(x.Popularity)*100 ),
                            // When selected, open it with the spotify client
                            Action = e => _context.API.ShellRun(x.Href),
                            IcoPath = "icon.png"
                        }).ToList();
                    break;
                case "album":
                    param = param.Substring("album ".Length);
                    // Retrieve data and return the first 10 results
                    Results = data.GetAlbums(param).Albums.ToList().GetRangeSafe(0, 10).Select(x => new Result()
                        {
                            Title = x.Name,
                            SubTitle = "Artist: " + string.Join(", ", x.Artists.Select(a => a.Name).ToArray()),
                            // When selected, open it with the spotify client
                            Action = e => _context.API.ShellRun(x.Href),
                            IcoPath = data.GetArtwork(x.Href)
                        }).ToList();
                    break;
                default:
                    if (query.ActionParameters[0] == "track")
                        param = param.Substring("track ".Length);
                    // Retrieve data and return the first 20 results
                    Results = data.GetTracks(param).Tracks.ToList().GetRangeSafe(0, 20).Select(x => new Result()
                        {
                            Title = x.Name,
                            SubTitle = "Artist: " + string.Join(", ", x.Artists.Select(a => a.Name).ToArray()),
                            // When selected, open it with the spotify client
                            Action = e => _context.API.ShellRun(x.Href),
                            IcoPath = "icon.png"
                        }).ToList();
                    break;
            }
            if (Results.Count > 0)
                return Results;
            else
                return new List<Result>()
                    {
                        new Result() { Title = "No results found on Spotify.", IcoPath = "icon.png" }
                    };
        }        
    }

    public static class Extensions
    {
        /// <summary>
        /// Returns a range of elements in the source List, limiting the number of 
        /// results to the specified maximum and starting from the specified index.
        /// </summary>
        public static List<T> GetRangeSafe<T> (this List<T> source, int index, int maxCount)
        {
            // Index cannot be negative
            if (index < 0)
                throw new ArgumentOutOfRangeException("index");

            // if index, count are not out of bounds, use GetRange
            if (source.Count - index >= maxCount)
                return source.GetRange(index, maxCount);
            
            // if count is greater than the number of items after the specified position (index),
            //  return a list of all items after that position.
            if (source.Count - index >= 0)
                return source.GetRange(index, source.Count - index);
            
            // In any other case, return an empty list
            return new List<T>();
        }
    }
}