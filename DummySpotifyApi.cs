using System.Collections.Generic;
using SpotifyAPI.Local.Models;
using SpotifyAPI.Web.Models;

namespace Wox.Plugin.Spotify
{
    internal class DummySpotifyApi : ISpotifyApi
    {
        public Track CurrentTrack => new Track()
        {
            TrackResource =
            {
                Name = "Spotify plugin is initializing",
            },
            ArtistResource =
            {
                Name = ""
            }
        };

        public IEnumerable<SimpleAlbum> GetAlbums(string s)
        {
            return new List<SimpleAlbum>();
        }
        
        public IEnumerable<FullArtist> GetArtists(string s)
        {
            return new List<FullArtist>();
        }

        public string GetArtwork(SimpleAlbum album)
        {
            return null;
        }

        public string GetArtwork(Track track)
        {
            return null;
        }

        public string GetArtwork(FullTrack track)
        {
            return null;
        }

        public string GetArtwork(FullArtist artist)
        {
            return null;
        }

        public IEnumerable<FullTrack> GetTracks(string s)
        {
            return new List<FullTrack>();
        }

        public void Pause()
        {
        }

        public void Play()
        {
        }

        public void Play(SimpleAlbum album)
        {
        }

        public void Play(FullTrack track)
        {
        }

        public void Skip()
        {
        }
    }
}