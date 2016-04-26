using System.Collections.Generic;
using SpotifyAPI.Local.Models;
using SpotifyAPI.Web.Models;

namespace Wox.Plugin.Spotify
{
    public interface ISpotifyApi
    {
        Track CurrentTrack { get; }

        IEnumerable<SimpleAlbum> GetAlbums(string s);
        IEnumerable<FullArtist> GetArtists(string s);
        string GetArtwork(SimpleAlbum album);
        string GetArtwork(Track track);
        string GetArtwork(FullTrack track);
        string GetArtwork(FullArtist artist);
        IEnumerable<FullTrack> GetTracks(string s);
        void Pause();
        void Play();
        void Play(SimpleAlbum album);
        void Play(FullTrack track);
        void Skip();
    }
}