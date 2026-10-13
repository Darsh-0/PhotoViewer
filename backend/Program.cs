using Microsoft.AspNetCore.Http.Features;
using PictureSite.Api.Photos;

var builder = WebApplication.CreateBuilder(args);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = PhotoEndpoints.MaxRequestSize);
builder.Services.AddSingleton<IPhotoStore>(_ =>
    new LocalPhotoStore(Path.Combine(builder.Environment.ContentRootPath, "uploads")));

var app = builder.Build();
app.MapPhotoEndpoints();
app.Run();
