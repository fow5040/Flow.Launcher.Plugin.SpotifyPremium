using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using SpotifyAPI.Local;
using SpotifyAPI.Local.Enums;
using SpotifyAPI.Local.Models;
using SpotifyAPI.Web;
using SpotifyAPI.Web.Enums;
using SpotifyAPI.Web.Models;

namespace Wox.Plugin.Spotify
{
    public class SpotifyApi : ISpotifyApi
    {
        private readonly SpotifyLocalAPI _localSpotify;
        private readonly SpotifyWebAPI _spotifyApi;
        private readonly object _lock = new object();


        public SpotifyApi(string pluginDir = null)
        {
            var pluginDirectory = pluginDir ?? Directory.GetCurrentDirectory();
            CacheFodler = Path.Combine(pluginDirectory, "Cache");

            // Create the cache folder, if it doesn't already exist
            if (!Directory.Exists(CacheFodler))
                Directory.CreateDirectory(CacheFodler);

            _localSpotify = new SpotifyLocalAPI();
            _localSpotify.OnTrackChange += LocalSpotifyOnTrackChange;
            ConnectToSpotify();

            _spotifyApi = new SpotifyWebAPI
            {
                UseAuth = false
            };
        }
        
        
        public bool IsPlaying { get; set; }

        private string CacheFodler { get; }

        public Track CurrentTrack { get; private set; }



        public void Play()
        {
            _localSpotify.Play();
        }

        public void Play(FullTrack track)
        {
            _localSpotify.PlayURL(track.Uri);
        }

        public void Play(SimpleAlbum album)
        {
            _localSpotify.PlayURL(album.Uri);
        }

        public void Pause()
        {
            _localSpotify.Pause();
        }

        public void Skip()
        {
            _localSpotify.Skip();
        }

        public IEnumerable<FullArtist> GetArtists(string s)
        {
            lock (_lock)
            {
                var searchItems = _spotifyApi.SearchItems(s, SearchType.Artist, 10);

                return searchItems.Artists.Items;
            }
        }

        public IEnumerable<SimpleAlbum> GetAlbums(string s)
        {
            lock (_lock)
            {
                var searchItems = _spotifyApi.SearchItems(s, SearchType.Album, 10);

                return searchItems.Albums.Items;
            }
        }

        public IEnumerable<FullTrack> GetTracks(string s)
        {
            lock (_lock)
            {
                var searchItems = _spotifyApi.SearchItems(s, SearchType.Track, 10);

                return searchItems.Tracks.Items;
            }
        }

        public string GetArtwork(SimpleAlbum album)
        {
            if (!album.Images.Any())
                return null;

            var url = album.Images[0].Url;

            // use the unique spotify ID as the local file name
            var uniqueId = GetUniqueIdForArtwork(album.Uri);

            return DownloadImage(uniqueId, url);
        }

        public string GetArtwork(FullArtist artist)
        {
            if (!artist.Images.Any())
                return null;

            var url = artist.Images[0].Url;

            // use the unique spotify ID as the local file name
            var uniqueId = GetUniqueIdForArtwork(artist.Uri);

            return DownloadImage(uniqueId, url);
        }

        public string GetArtwork(FullTrack track) => GetArtwork(track.Album);

        public string GetArtwork(Track track)
        {
            var albumArtUrl = track.GetAlbumArtUrl(AlbumArtSize.Size160);
            var uniqueId = GetUniqueIdForArtwork(track.TrackResource.Uri);
            
            return DownloadImage(uniqueId, albumArtUrl);
        }



        private static string GetUniqueIdForArtwork(string uri) => uri.Substring(uri.LastIndexOf(":", StringComparison.Ordinal) + 1);

        private void ConnectToSpotify()
        {
            if (!SpotifyLocalAPI.IsSpotifyRunning())
            {
                return;
            }
            if (!SpotifyLocalAPI.IsSpotifyWebHelperRunning())
            {
                return;
            }

            var successful = _localSpotify.Connect();
            if (successful)
            {
                UpdateInfos();
                _localSpotify.ListenForEvents = true;
            }
        }

        private void UpdateInfos()
        {
            var status = _localSpotify.GetStatus();

            if (status?.Track != null) //Update track infos
            {
                UpdateTrack(status.Track);
            }
        }

        private void UpdateTrack(Track track)
        {
            CurrentTrack = track;
        }
        
        private string DownloadImage(string uniqueId, string url)
        {
            // local path to the image file, located in the Cache folder
            var path = $@"{CacheFodler}\{uniqueId}.jpg";

            if (File.Exists(path))
            {
                return path;
            }

            using (var wc = new WebClient())
            {
                wc.DownloadFile(new Uri(url), path);
            }
            return path;
        }

        private void LocalSpotifyOnTrackChange(object sender, TrackChangeEventArgs e)
        {
            UpdateTrack(e.NewTrack);
        }
    }
}