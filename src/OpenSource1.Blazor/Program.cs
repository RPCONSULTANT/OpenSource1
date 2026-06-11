using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using OpenSource1.Application.Security;
using OpenSource1.Blazor.Components;
using OpenSource1.Blazor.Security;
using OpenSource1.Blazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = "OpenSource1.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.IdleTimeout = TimeSpan.FromMinutes(30);
});

builder.Services.Configure<ApiClientOptions>(builder.Configuration.GetSection(ApiClientOptions.SectionName));
builder.Services.AddScoped<BearerTokenHandler>();
builder.Services.AddHttpClient<IAuthApiClient, AuthApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiClientOptions>>().Value;
    client.BaseAddress = options.BaseAddress;
}).AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddHttpClient<IAppSettingsApiClient, AppSettingsApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiClientOptions>>().Value;
    client.BaseAddress = options.BaseAddress;
}).AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddHttpClient<IUserAdminApiClient, UserAdminApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiClientOptions>>().Value;
    client.BaseAddress = options.BaseAddress;
}).AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddHttpClient<IEntradaApiClient, EntradaApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiClientOptions>>().Value;
    client.BaseAddress = options.BaseAddress;
}).AddHttpMessageHandler<BearerTokenHandler>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "OpenSource1.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/access-denied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(ApplicationPolicies.CanAdd, policy => policy.RequireClaim("permission", ApplicationPolicies.CanAdd));
    options.AddPolicy(ApplicationPolicies.CanModify, policy => policy.RequireClaim("permission", ApplicationPolicies.CanModify));
    options.AddPolicy(ApplicationPolicies.CanDelete, policy => policy.RequireClaim("permission", ApplicationPolicies.CanDelete));
    options.AddPolicy(ApplicationPolicies.CanConsult, policy => policy.RequireClaim("permission", ApplicationPolicies.CanConsult));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true || context.Request.Path.StartsWithSegments("/account"))
    {
        context.Response.Headers.CacheControl = "no-store, no-cache, max-age=0, must-revalidate";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
    }

    await next();
});
app.UseAntiforgery();

app.MapPost("/account/logout", async (HttpContext httpContext) =>
{
    httpContext.Session.Clear();
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    httpContext.Response.Headers["Clear-Site-Data"] = "\"cache\", \"storage\"";
    return Results.Redirect("/account/login?loggedOut=true");
}).RequireAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>();

app.Run();
