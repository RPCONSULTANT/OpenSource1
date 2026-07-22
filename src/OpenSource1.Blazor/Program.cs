using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Mvc;
using OpenSource1.Application.Services.Auth.Dtos;
using OpenSource1.Application.Security;
using OpenSource1.Blazor.Components;
using OpenSource1.Blazor.Reporting;
using OpenSource1.Blazor.Security;
using OpenSource1.Blazor.Services;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

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
builder.Services.AddSingleton<IFileStorageService, LocalFileStorageService>();
builder.Services.AddSingleton<IReportDocumentService, QuestPdfReportDocumentService>();
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
builder.Services.AddHttpClient<IClienteApiClient, ClienteApiClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApiClientOptions>>().Value;
    client.BaseAddress = options.BaseAddress;
}).AddHttpMessageHandler<BearerTokenHandler>();
builder.Services.AddHttpClient<IProductoApiClient, ProductoApiClient>((serviceProvider, client) =>
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
var uploadsRoot = Path.Combine(app.Environment.ContentRootPath, "storage", "uploads");
Directory.CreateDirectory(uploadsRoot);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(uploadsRoot),
    RequestPath = "/uploads"
});

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
    httpContext.Response.Cookies.Delete("OpenSource1.Session", new CookieOptions { Path = "/" });
    httpContext.Response.Headers["Clear-Site-Data"] = "\"cache\", \"cookies\", \"storage\"";
    return Results.Redirect("/");
}).RequireAuthorization(ApplicationPolicies.CanConsult);

app.MapPost("/account/profile/image", async (
    HttpContext httpContext,
    IAuthApiClient authApiClient,
    IFileStorageService fileStorageService) =>
{
    var currentUser = await authApiClient.GetCurrentUserAsync();
    if (currentUser is null)
    {
        return Results.Redirect("/account/login");
    }

    try
    {
        var path = await fileStorageService.SaveProfileImageAsync(httpContext, "ProfileImage", currentUser.ProfileImagePath);
        var (success, _) = await authApiClient.UpdateProfileImageAsync(new UpdateProfileImageRequest(path));
        return success
            ? Results.Redirect("/account/profile")
            : Results.Redirect("/account/profile");
    }
    catch
    {
        return Results.Redirect("/account/profile");
    }
}).RequireAuthorization(ApplicationPolicies.CanConsult);

app.MapPost("/reports/clientes/selected", async (
    [FromForm] ClienteSelectionReportForm form,
    IClienteApiClient clienteApiClient,
    IReportDocumentService reportDocumentService) =>
{
    var selectedIds = form.SelectedIds.Where(id => id != Guid.Empty).Distinct().ToArray();
    if (selectedIds.Length == 0)
    {
        return Results.Redirect("/clientes?ok=report-select-required");
    }

    var clientes = new List<OpenSource1.Application.Features.Clientes.Dtos.ClienteResponse>();
    foreach (var id in selectedIds)
    {
        var item = await clienteApiClient.GetByIdAsync(id);
        if (item is not null)
        {
            clientes.Add(item);
        }
    }

    if (clientes.Count == 0)
    {
        return Results.Redirect("/clientes?ok=report-no-data");
    }

    var file = reportDocumentService.GenerateClientesReport(clientes, "Reporte de Clientes Seleccionados");
    return Results.File(file.Content, file.ContentType, file.FileName);
}).RequireAuthorization(ApplicationPolicies.CanConsult);

app.MapPost("/reports/clientes/selected.xlsx", async (
    [FromForm] ClienteSelectionReportForm form,
    IClienteApiClient clienteApiClient,
    IReportDocumentService reportDocumentService) =>
{
    var selectedIds = form.SelectedIds.Where(id => id != Guid.Empty).Distinct().ToArray();
    if (selectedIds.Length == 0)
    {
        return Results.Redirect("/clientes?ok=report-select-required");
    }

    var clientes = new List<OpenSource1.Application.Features.Clientes.Dtos.ClienteResponse>();
    foreach (var id in selectedIds)
    {
        var item = await clienteApiClient.GetByIdAsync(id);
        if (item is not null)
        {
            clientes.Add(item);
        }
    }

    if (clientes.Count == 0)
    {
        return Results.Redirect("/clientes?ok=report-no-data");
    }

    var file = reportDocumentService.GenerateClientesExcel(clientes, "Reporte de Clientes Seleccionados");
    return Results.File(file.Content, file.ContentType, file.FileName);
}).RequireAuthorization(ApplicationPolicies.CanConsult);

app.MapPost("/reports/productos/selected", async (
    [FromForm] ProductoSelectionReportForm form,
    IProductoApiClient productoApiClient,
    IReportDocumentService reportDocumentService) =>
{
    var selectedIds = form.SelectedIds.Where(id => id != Guid.Empty).Distinct().ToArray();
    if (selectedIds.Length == 0)
    {
        return Results.Redirect("/productos?ok=report-select-required");
    }

    var productos = new List<OpenSource1.Application.Features.Productos.Dtos.ProductoResponse>();
    foreach (var id in selectedIds)
    {
        var item = await productoApiClient.GetByIdAsync(id);
        if (item is not null)
        {
            productos.Add(item);
        }
    }

    if (productos.Count == 0)
    {
        return Results.Redirect("/productos?ok=report-no-data");
    }

    productos = form.StockState switch
    {
        "with-stock" => productos.Where(x => x.Stock > 0).ToList(),
        "without-stock" => productos.Where(x => x.Stock <= 0).ToList(),
        _ => productos
    };

    if (productos.Count == 0)
    {
        return Results.Redirect("/productos?ok=report-no-data");
    }

    var file = reportDocumentService.GenerateProductosReport(productos, "Reporte de Productos Seleccionados");
    return Results.File(file.Content, file.ContentType, file.FileName);
}).RequireAuthorization(ApplicationPolicies.CanConsult);

app.MapPost("/reports/productos/selected.xlsx", async (
    [FromForm] ProductoSelectionReportForm form,
    IProductoApiClient productoApiClient,
    IReportDocumentService reportDocumentService) =>
{
    var selectedIds = form.SelectedIds.Where(id => id != Guid.Empty).Distinct().ToArray();
    if (selectedIds.Length == 0)
    {
        return Results.Redirect("/productos?ok=report-select-required");
    }

    var productos = new List<OpenSource1.Application.Features.Productos.Dtos.ProductoResponse>();
    foreach (var id in selectedIds)
    {
        var item = await productoApiClient.GetByIdAsync(id);
        if (item is not null)
        {
            productos.Add(item);
        }
    }

    if (productos.Count == 0)
    {
        return Results.Redirect("/productos?ok=report-no-data");
    }

    productos = form.StockState switch
    {
        "with-stock" => productos.Where(x => x.Stock > 0).ToList(),
        "without-stock" => productos.Where(x => x.Stock <= 0).ToList(),
        _ => productos
    };

    if (productos.Count == 0)
    {
        return Results.Redirect("/productos?ok=report-no-data");
    }

    var file = reportDocumentService.GenerateProductosExcel(productos, "Reporte de Productos Seleccionados");
    return Results.File(file.Content, file.ContentType, file.FileName);
}).RequireAuthorization(ApplicationPolicies.CanConsult);

app.MapPost("/reports/clientes/history", async (
    [FromForm] ClienteHistoricalReportForm form,
    IClienteApiClient clienteApiClient,
    IReportDocumentService reportDocumentService) =>
{
    var clientes = await clienteApiClient.ListAsync();
    if (form.Status == "inactive")
    {
        clientes = [];
    }
    if (clientes.Count == 0)
    {
        return Results.Redirect("/reporteria?entity=clientes&ok=report-no-data");
    }

    var file = reportDocumentService.GenerateClientesReport(clientes, "Reporte Histórico de Clientes");
    return Results.File(file.Content, file.ContentType, file.FileName);
}).RequireAuthorization(ApplicationPolicies.CanConsult);

app.MapPost("/reports/clientes/history.xlsx", async (
    [FromForm] ClienteHistoricalReportForm form,
    IClienteApiClient clienteApiClient,
    IReportDocumentService reportDocumentService) =>
{
    var clientes = await clienteApiClient.ListAsync();
    if (form.Status == "inactive")
    {
        clientes = [];
    }
    if (clientes.Count == 0)
    {
        return Results.Redirect("/reporteria?entity=clientes&ok=report-no-data");
    }

    var file = reportDocumentService.GenerateClientesExcel(clientes, "Reporte Histórico de Clientes");
    return Results.File(file.Content, file.ContentType, file.FileName);
}).RequireAuthorization(ApplicationPolicies.CanConsult);

app.MapPost("/reports/productos/history", async (
    [FromForm] ProductoHistoricalReportForm form,
    IProductoApiClient productoApiClient,
    IReportDocumentService reportDocumentService) =>
{
    var productos = await productoApiClient.ListAsync();
    productos = form.StockState switch
    {
        "with-stock" => productos.Where(x => x.Stock > 0).ToList(),
        "without-stock" => productos.Where(x => x.Stock <= 0).ToList(),
        _ => productos
    };
    if (productos.Count == 0)
    {
        return Results.Redirect("/reporteria?entity=productos&ok=report-no-data");
    }

    var file = reportDocumentService.GenerateProductosReport(productos, "Reporte Histórico de Productos");
    return Results.File(file.Content, file.ContentType, file.FileName);
}).RequireAuthorization(ApplicationPolicies.CanConsult);

app.MapPost("/reports/productos/history.xlsx", async (
    [FromForm] ProductoHistoricalReportForm form,
    IProductoApiClient productoApiClient,
    IReportDocumentService reportDocumentService) =>
{
    var productos = await productoApiClient.ListAsync();
    productos = form.StockState switch
    {
        "with-stock" => productos.Where(x => x.Stock > 0).ToList(),
        "without-stock" => productos.Where(x => x.Stock <= 0).ToList(),
        _ => productos
    };
    if (productos.Count == 0)
    {
        return Results.Redirect("/reporteria?entity=productos&ok=report-no-data");
    }

    var file = reportDocumentService.GenerateProductosExcel(productos, "Reporte Histórico de Productos");
    return Results.File(file.Content, file.ContentType, file.FileName);
}).RequireAuthorization(ApplicationPolicies.CanConsult);

app.MapGet("/reports/clientes/raw.xlsx", async (
    IClienteApiClient clienteApiClient,
    IReportDocumentService reportDocumentService) =>
{
    var clientes = await clienteApiClient.ListAsync();
    var file = reportDocumentService.GenerateClientesRawExcel(clientes);
    return Results.File(file.Content, file.ContentType, file.FileName);
}).RequireAuthorization();

app.MapGet("/reports/productos/raw.xlsx", async (
    IProductoApiClient productoApiClient,
    IReportDocumentService reportDocumentService) =>
{
    var productos = await productoApiClient.ListAsync();
    var file = reportDocumentService.GenerateProductosRawExcel(productos);
    return Results.File(file.Content, file.ContentType, file.FileName);
}).RequireAuthorization();

app.MapStaticAssets();
app.MapRazorComponents<App>();

app.Run();
