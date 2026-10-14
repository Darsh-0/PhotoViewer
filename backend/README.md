# Photo API

A small ASP.NET Core API that stores uploaded photos in `backend/uploads` and serves them back to the React gallery.

## Run locally

Start the API from the repository root:

```powershell
dotnet run --project backend/PictureSite.Api.csproj
```

In another terminal, start the React app:

```powershell
cd my-app
npm run dev
```

Open the Vite URL shown in the terminal. The development server proxies `/api` and `/images` requests to the API at `http://localhost:5080`.

Uploads accept JPG, PNG, GIF, and WebP files up to 100 MB each, with a maximum request size of 10,000 MB. Image files are saved on disk in `backend/uploads` and remain available after the API restarts. Gallery views use cached 640-pixel WebP previews; opening or downloading a photo uses the original file.

The `/admin` page lets you sign in and delete photos. Put your password after `Admin__Password=` in `backend/.env` before starting the backend. The backend loads this ignored local file at startup; photo deletion is restricted to the signed-in admin session, stored in an HTTP-only cookie. An `Admin__Password` environment variable, if present, takes precedence over the file.
