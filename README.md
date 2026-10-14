# PictureSite

PictureSite is a photo gallery with a React frontend and an ASP.NET Core API. It supports multi-photo uploads, live photo updates over WebSockets, lazy gallery loading, full-size image viewing, PNG downloads, and an admin page for deleting photos.

## Requirements

- .NET 10 SDK
- Node.js and npm

## Run locally

Start the API from the repository root:

```powershell
dotnet run --project backend/PictureSite.Api.csproj
```

The API listens at `http://localhost:5080` for local development. In a second terminal, install the frontend dependencies and start Vite:

```powershell
cd my-app
npm install
npm run dev
```

Open the local URL printed by Vite. The frontend development server proxies `/api` and `/images` requests to the API.

Run only one API instance at a time. If `dotnet run` reports that address `http://127.0.0.1:5080` is already in use, the API is already running or another process owns that port. Check whether the API is responding at `http://localhost:5080/api/images?offset=0&limit=1`. To stop the instance, press `Ctrl+C` in the terminal where it was started.

If that terminal is no longer available, find the process listening on port 5080 in PowerShell:

```powershell
$listener = Get-NetTCPConnection -LocalPort 5080 -State Listen
$listener | Select-Object LocalAddress, LocalPort, OwningProcess
Get-Process -Id <PID>
```

After confirming the process is the stale API instance, stop it and start the API again:

```powershell
Stop-Process -Id <PID>
dotnet run --project backend/PictureSite.Api.csproj
```

## Admin access

The admin page is available at `/admin`. Set the admin password in `backend/.env`:

```dotenv
Admin__Password=choose-a-password
```

Restart the API after changing the password. The `.env` file is ignored by Git. An `Admin__Password` environment variable can also be used and takes precedence over the file. Admin sign-in uses an HTTP-only cookie.

## Photos and uploads

Uploaded originals and generated previews are stored in the backend's `uploads/` directory. Keep this directory backed up if you need to preserve photos; it is excluded from Git.

The upload limit is 100 MB per image and 10,000 MB per request. Supported upload formats are JPEG, PNG, GIF, and WebP. Gallery previews are compressed WebP images; opening a photo loads the original. Downloads are converted to PNG, including images in bulk downloads.

## Project layout

- `backend/` - ASP.NET Core API, image processing, authentication, and local photo storage
- `my-app/` - React and Vite frontend
- `uploads/` - runtime photo data (ignored by Git)
