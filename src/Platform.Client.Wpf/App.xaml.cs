using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Platform.Client.Core.Services;
using Platform.Client.Wpf.ViewModels;

namespace Platform.Client.Wpf;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton<ClientPathService>();
                services.AddSingleton<ClientSettingsStore>();
                services.AddSingleton<SecureTokenStore>();
                services.AddSingleton<ClientLogService>();
                services.AddSingleton<DeviceIdentityService>();
                services.AddSingleton<PinnedHttpClientFactory>();
                services.AddSingleton<ClientApiService>();
                services.AddSingleton<UpdateVerificationService>();
                services.AddSingleton<UpdateInstallerService>();
                services.AddSingleton<AutostartService>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        await _host.StartAsync();
        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();
    }

    protected override async void OnExit(System.Windows.ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
