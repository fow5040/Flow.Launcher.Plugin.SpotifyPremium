# Create Development Build
dotnet build

# Kill Flow Launcher
Do {  
    $ProcessesFound = Get-Process | Where-Object -Property ProcessName -EQ 'Flow.Launcher'
    If ($ProcessesFound) {
        Stop-Process -Name Flow.Launcher
        Start-Sleep 1
    }
} Until (!$ProcessesFound)
## Uncomment this if you need to clear out everything
# del $env:APPDATA\FlowLauncher\Plugins\Flow.Launcher.Plugin.SpotifyPremium\
Copy-Item -Recurse .\Output\Debug\Flow.Launcher.Plugin.SpotifyPremium $env:APPDATA\FlowLauncher\Plugins\ -Force
Start-Process $env:LOCALAPPDATA\FlowLauncher\Flow.Launcher.exe