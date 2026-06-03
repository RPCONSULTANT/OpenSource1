using OpenSource1.Application.Security;
using OpenSource1.Application.Services;
using OpenSource1.Infrastructure.Data;
using OpenSource1.Infrastructure.Identity;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplicationData(builder.Configuration);
builder.Services.AddApplicationIdentity(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.Configure<CorsOptions>(builder.Configuration.GetSection(CorsOptions.SectionName));
builder.Services.AddCors(options =>
{
    var corsOptions = builder.Configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>() ?? new CorsOptions();

    options.AddPolicy(CorsOptions.PolicyName, policy =>
    {
        if (corsOptions.AllowedOrigins.Length > 0)
        {
            policy.WithOrigins(corsOptions.AllowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseStatusCodePages(async statusCodeContext =>
{
    var response = statusCodeContext.HttpContext.Response;

    if (response.HasStarted || response.StatusCode is not (StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden))
    {
        return;
    }

    await response.WriteAsJsonAsync(new
    {
        status = response.StatusCode,
        message = response.StatusCode == StatusCodes.Status403Forbidden
            ? "Permisos insuficientes para realizar esta operación."
            : "Debe autenticarse para acceder a este recurso."
    });
});
app.UseRouting();
app.UseCors(CorsOptions.PolicyName);
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
