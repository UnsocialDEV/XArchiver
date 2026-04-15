# XArchiver

XArchiver is a WinUI 3 desktop application for building a local archive of public X content. It supports two archive paths:

- X API sync for profile-based incremental archiving
- Playwright-driven web capture as a browser-based fallback

The app stores archived post text, media, metadata, and an index you can browse later from the built-in viewer.

## What It Does

- Saves public posts, images, videos, and metadata into a user-selected archive folder
- Supports profile-based archive rules for original posts, replies, quote posts, and reposts
- Queues and tracks API sync sessions with pause, restart, and stop controls
- Lets you review recent posts manually before archiving selected items
- Includes a dedicated scraper browser session for website-based capture when the API path is not the right fit
- Browses saved archives inside the app with search, gallery, metadata, and media preview views
- Schedules future archive runs and persists app state locally

## Solution Layout

- `XArchiver/`
  WinUI 3 desktop application, app shell, pages, view models, and platform services
- `XArchiver.Core/`
  Core models, interfaces, archive services, storage logic, and sync orchestration
- `XArchiver.Tests/`
  MSTest coverage for core services and utilities

## Requirements

- Windows 10 build 17763 or later
- .NET 8 SDK
- Visual Studio 2022 is the most practical way to run the desktop app
- An X Bearer Token if you want to use the API sync and review flows
- A validated dedicated browser session if you want to use the Playwright scraper flow

## Running The App

1. Restore and build the solution:

```powershell
dotnet restore .\XArchiver.slnx
dotnet build .\XArchiver.slnx
```

2. Open the solution in Visual Studio and start one of the configured launch profiles:

- `XArchiver (Unpackaged)`
- `XArchiver (Package)`

3. Inside the app:

- Save your X Bearer Token in `Settings` for API-based archiving
- Create one or more archive profiles in `Profiles`
- Run a sync, use `Review`, or configure the `Scraper` flow
- Open `Viewer` to inspect the saved archive

## Testing

Run the automated tests with:

```powershell
dotnet test .\XArchiver.Tests\XArchiver.Tests.csproj
```

## Local Storage

XArchiver uses two storage locations:

- Your chosen archive folder for saved posts, media, text, and metadata
- `%LOCALAPPDATA%\XArchiver` for app state such as profiles, scheduled runs, scraper session data, thumbnails, and app settings

The API credential is stored in Windows Credential Locker, not in source control or the repo.

## Notes

- The X API path is incremental and keeps a checkpoint for later syncs when possible.
- The web scraper uses a dedicated Playwright-backed browser session that must be signed in and validated before scraping.
- The viewer can search archived text and inspect saved metadata, raw payloads, and media files after archiving.
