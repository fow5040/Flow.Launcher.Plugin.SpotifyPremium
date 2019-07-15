Wox.Plugin.Spotify
==================

Spotify plugin for the [Wox launcher](https://github.com/Wox-launcher/Wox)

### About

Control your Spotify client from Wox. Search for tracks, artists, or albums and launch the results directly to your client.

![image](http://i.imgur.com/AfUkPvd.gif)

### Usage
| Keyword                            | Description                   |
| ---------------------------------- | ----------------------------- |
| `` sp ``                           | Show currently playing track  |
| `` sp {track, artist, or album} `` | Search for tracks             |
| `` sp artist {artist name} ``      | Search for an artist          |
| `` sp album {album name} ``        | Search for an album           |
| `` sp playlist {playlist name} ``  | Browse or Search Playlists    |
| `` sp next ``                      | Play next track               |
| `` sp last ``                      | Play previous track           |
| `` sp play ``                      | Resume currently playing track|
| `` sp pause ``                     | Pause currently playing track |
| `` sp mute ``                      | Toggle Mute                   |
| `` sp volume ``                    | Set Volume (1-100)            |
| `` sp shuffle ``                   | Toggle Shuffle Mode           |

### Notice
- Spotify relies on Web Authentication Calls to Connect to the API remotely - you will need to authorize it to use this plugin
- To get started:
    - Restore all NuGet dependencies (nuget restore packages.config -PackagesDirectory ..\packages)
    - Run build script (visual studio code)

### Third-Party Libraries

- [SpotifyAPI-NET](https://github.com/JohnnyCrazy/SpotifyAPI-NET) : Spotify API wrapper
- [Json.Net](https://github.com/JamesNK/Newtonsoft.Json) : High performance json library

## To Do - Ideas

- Better solution to [#6](https://github.com/JohnTheGr8/Wox.Plugin.Spotify/issues/6)
- Search user content (playlists, saved music)
- Configurable default search type (now track search is default, album/playlist search might be more suitable to some)
- User Configuration (#1)
- Clear Cache folder