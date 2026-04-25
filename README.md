<p align="center">
  <img src="logosmall.png" width="64" alt="WinScrobb" />
</p>

# WinScrobb

A Last.fm scrobbler for Windows. Sits in your system tray and scrobbles music from any app — Spotify, Apple Music, iTunes, browsers, and more.

![Windows 11](https://img.shields.io/badge/Windows%2011-0078D4?style=flat&logo=windows11&logoColor=white)
![.NET 10](https://img.shields.io/badge/.NET%2010-512BD4?style=flat&logo=dotnet&logoColor=white)

## Download

Grab the latest `WinScrobb-Setup.exe` from [Releases](../../releases).

## Setup

1. Run the installer
2. Get a free API key at [last.fm/api/account/create](https://www.last.fm/api/account/create)
3. Enter your API Key and Secret in Settings — WinScrobb will open a browser to authorize your account

That's it. WinScrobb starts with Windows automatically.

## Features

- Scrobbles from any SMTC-aware app (Spotify, Apple Music, WMP, foobar2000, browsers…)
- iTunes fallback via COM for legacy iTunes
- Filters out videos and podcasts — music only
- Love / unlove tracks from the tray popup
- Windows 11 Fluent design with dark mode support

## Building

Requires [.NET 10 SDK](https://dot.net).

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```
