using Microsoft.AspNetCore.Http.Features;
using PictureSite.Api.Admin;
using PictureSite.Api.Photos;

var builder = WebApplication.CreateBuilder(args);
LocalAdminEnvironment.Load(builder.Environment.ContentRootPath);
builder.Configuration.AddEnvironmentVariables();
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = PhotoEndpoints.MaxRequestSize);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = PhotoEndpoints.MaxRequestSize);
builder.Services.AddAdminAuthentication();
builder.Services.AddSingleton<IPhotoStore>(_ =>
    new LocalPhotoStore(Path.Combine(builder.Environment.ContentRootPath, "uploads")));

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.MapPhotoEndpoints();
app.MapAdminEndpoints();
app.Run();
