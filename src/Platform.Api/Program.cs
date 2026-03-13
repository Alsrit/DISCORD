using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.HttpOverrides;
using FluentValidation;
using FluentValidation.AspNetCore;
using Platform.Api.Authentication;
using Platform.Api.Middleware;
using Platform.Application.Validators;
using Platform.Infrastructure.Extensions;
using Platform.Infrastructure.Persistence;
using Platform.Application.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["correlationId"] = context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddControllers();
builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddPlatformInfrastructure(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<AnalyzeModRequestValidator>();
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddAuthentication("ClientBearer")
    .AddScheme<AuthenticationSchemeOptions, OpaqueTokenAuthenticationHandler>("ClientBearer", _ => { });
builder.Services.AddAuthorization();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(System.Net.IPAddress.Parse("127.0.0.1"));
    options.KnownProxies.Add(System.Net.IPAddress.IPv6Loopback);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHsts();
}

app.UseExceptionHandler();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health/live", () => Results.Ok(new { status = "ok", mode = "live", utcNow = DateTimeOffset.UtcNow }));
app.MapGet("/health/ready", async (PlatformDbContext db, CancellationToken cancellationToken) =>
{
    var dbReady = await db.Database.CanConnectAsync(cancellationToken);
    return dbReady
        ? Results.Ok(new { status = "ok", mode = "ready", utcNow = DateTimeOffset.UtcNow })
        : Results.Problem("База данных недоступна.", statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<IPlatformSeeder>();
    await seeder.SeedAsync(CancellationToken.None);
}

app.Run();
