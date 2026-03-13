using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.Infrastructure.Extensions;
using Platform.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddPlatformInfrastructure(builder.Configuration);
builder.Services.AddHostedService<MaintenanceWorker>();
builder.Services.AddHostedService<TranslationWorker>();

var host = builder.Build();
host.Run();
