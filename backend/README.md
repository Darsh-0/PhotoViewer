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

Uploads accept JPG, PNG, GIF, and WebP files up to 10 MB. Image files are saved on disk in `backend/uploads` and remain available after the API restarts.
