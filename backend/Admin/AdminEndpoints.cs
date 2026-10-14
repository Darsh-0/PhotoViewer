using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace PictureSite.Api.Admin;

public static class AdminEndpoints
{
    private const string AdminPolicy = "AdminOnly";

    public static IServiceCollection AddAdminAuthentication(
        this IServiceCollection services)
    {
        services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "PictureSite.Admin";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddAuthorization(options => options.AddPolicy(AdminPolicy, policy =>
            policy.RequireAuthenticatedUser().RequireRole("Admin")));
        return services;
    }

    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/admin/session", (HttpContext context) =>
                Results.Ok(new { authenticated = context.User.IsInRole("Admin") }))
            .AllowAnonymous();

        endpoints.MapPost("/api/admin/login", SignIn)
            .AllowAnonymous();

        endpoints.MapPost("/api/admin/logout", SignOut)
            .RequireAuthorization(AdminPolicy);

        return endpoints;
    }

    private static async Task<IResult> SignIn(
        LoginRequest request,
        HttpContext context,
        IConfiguration configuration)
    {
        var expectedPassword = configuration["Admin:Password"];
        if (string.IsNullOrEmpty(expectedPassword))
            return Results.Problem(
                "Admin sign-in is not configured. Set the Admin__Password environment variable and restart the backend.",
                statusCode: StatusCodes.Status503ServiceUnavailable);

        var suppliedBytes = Encoding.UTF8.GetBytes(request.Password ?? string.Empty);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedPassword);
        if (suppliedBytes.Length != expectedBytes.Length ||
            !CryptographicOperations.FixedTimeEquals(suppliedBytes, expectedBytes))
        {
            return Results.Json(new { error = "The password is incorrect." }, statusCode: StatusCodes.Status401Unauthorized);
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "admin"), new Claim(ClaimTypes.Role, "Admin")],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });

        return Results.Ok(new { authenticated = true });
    }

    private static async Task<IResult> SignOut(HttpContext context)
    {
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Results.Ok(new { authenticated = false });
    }

    private sealed record LoginRequest(string? Password);
}
