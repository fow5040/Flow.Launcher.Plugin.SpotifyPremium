dotnet publish -c Release -r win-x64
$rootPath = './Output/Release'
$buildPath = "${rootPath}/Flow.Launcher.Plugin.SpotifyPremium/win-x64/publish"
mkdir "${rootPath}/SpotifyPremium/"
Copy-Item "${buildPath}/*" "${rootPath}/SpotifyPremium/"
Compress-Archive -LiteralPath ./Output/Release/SpotifyPremium/ -DestinationPath Output/Release/Flow.Launcher.Plugin.SpotifyPremium.zip -Force
Remove-Item -Recurse "${rootPath}/SpotifyPremium/"