using CallbackListener.Domain;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;

namespace CallbackListener.Web.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth");

        group.MapGet("/me", async (HttpContext ctx, UserManager<AppUser> userMgr) =>
        {
            if (ctx.User.Identity?.IsAuthenticated != true) return Results.Unauthorized();
            var user = await userMgr.GetUserAsync(ctx.User);
            if (user is null) return Results.Unauthorized();
            return Results.Ok(new
            {
                id          = user.Id,
                email       = user.Email,
                displayName = user.DisplayName,
                initials    = Initials(user.DisplayName.Length > 0 ? user.DisplayName : user.Email ?? "")
            });
        });

        group.MapPost("/register", async (RegisterRequest req, UserManager<AppUser> userMgr, SignInManager<AppUser> signInMgr) =>
        {
            var user = new AppUser
            {
                UserName    = req.Email,
                Email       = req.Email,
                DisplayName = req.DisplayName?.Trim() ?? req.Email.Split('@')[0],
            };

            var result = await userMgr.CreateAsync(user, req.Password);
            if (!result.Succeeded)
                return Results.BadRequest(new { errors = result.Errors.Select(e => e.Description) });

            await signInMgr.SignInAsync(user, isPersistent: true);
            return Results.Ok(new
            {
                id          = user.Id,
                email       = user.Email,
                displayName = user.DisplayName,
                initials    = Initials(user.DisplayName)
            });
        });

        group.MapPost("/login", async (LoginRequest req, SignInManager<AppUser> signInMgr, UserManager<AppUser> userMgr) =>
        {
            var user = await userMgr.FindByEmailAsync(req.Email);
            if (user is null) return Results.Unauthorized();

            var result = await signInMgr.PasswordSignInAsync(user, req.Password, isPersistent: true, lockoutOnFailure: false);
            if (!result.Succeeded) return Results.Unauthorized();

            return Results.Ok(new
            {
                id          = user.Id,
                email       = user.Email,
                displayName = user.DisplayName,
                initials    = Initials(user.DisplayName.Length > 0 ? user.DisplayName : user.Email ?? "")
            });
        });

        group.MapPost("/logout", async (SignInManager<AppUser> signInMgr) =>
        {
            await signInMgr.SignOutAsync();
            return Results.Ok();
        });

        group.MapGet("/google", (HttpContext ctx, SignInManager<AppUser> signInMgr) =>
        {
            // ConfigureExternalAuthenticationProperties sets Items["LoginProvider"] = "Google",
            // which GetExternalLoginInfoAsync requires to parse the external cookie.
            var props = signInMgr.ConfigureExternalAuthenticationProperties("Google", "/auth/google/complete");
            return Results.Challenge(props, ["Google"]);
        });

        // This endpoint is called by the middleware after it has exchanged the OAuth code.
        // /auth/google/callback is handled by the middleware itself (opt.CallbackPath).
        group.MapGet("/google/complete", async (HttpContext ctx, SignInManager<AppUser> signInMgr, UserManager<AppUser> userMgr) =>
        {
            var info = await signInMgr.GetExternalLoginInfoAsync();
            if (info is null) return Results.Redirect("/account/login.html?error=1");

            var result = await signInMgr.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: true);
            if (result.Succeeded) return Results.Redirect("/");

            var email = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? "";
            var name  = info.Principal.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? email;

            var user = new AppUser { UserName = email, Email = email, EmailConfirmed = true, DisplayName = name };
            var create = await userMgr.CreateAsync(user);
            if (!create.Succeeded) return Results.Redirect("/account/login.html?error=1");

            await userMgr.AddLoginAsync(user, info);
            await signInMgr.SignInAsync(user, isPersistent: true);
            return Results.Redirect("/");
        });
    }

    private static string Initials(string name)
    {
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant(),
            _ => $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant()
        };
    }
}

record RegisterRequest(string Email, string Password, string? DisplayName);
record LoginRequest(string Email, string Password);
