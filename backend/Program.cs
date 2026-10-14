using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.DataProtection;
using PictureSite.Api.Admin;
using PictureSite.Api.Photos;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
LocalAdminEnvironment.Load(builder.Environment.ContentRootPath);
builder.Configuration.AddEnvironmentVariables();
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = PhotoEndpoints.MaxRequestSize);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = PhotoEndpoints.MaxRequestSize);
var dataProtectionKeysPath = Path.Combine(builder.Environment.ContentRootPath, "uploads", ".data-protection-keys");
Directory.CreateDirectory(dataProtectionKeysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
builder.Services.AddAdminAuthentication();
builder.Services.AddSingleton<IPhotoStore>(_ =>
    new LocalPhotoStore(Path.Combine(builder.Environment.ContentRootPath, "uploads")));
builder.Services.AddSingleton<PhotoUpdateHub>();

var app = builder.Build();
app.UseWebSockets();
app.UseAuthentication();
app.UseAuthorization();
app.MapPhotoEndpoints();
app.MapAdminEndpoints();
app.Run();
