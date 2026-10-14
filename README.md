# PictureSite

PictureSite is a photo gallery with a React frontend and an ASP.NET Core API. It supports multi-photo uploads, lazy gallery loading, full-size image viewing, PNG downloads, and an admin page for deleting photos.

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
