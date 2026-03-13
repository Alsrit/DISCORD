using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Authentication;
using Platform.Application.Models;
using Platform.Client.Core.Configuration;
using Platform.Client.Core.Models;
using Platform.Client.Core.Services;

namespace Platform.Client.Wpf.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ClientSettingsStore _settingsStore;
    private readonly SecureTokenStore _tokenStore;
    private readonly ClientLogService _logService;
    private readonly ClientApiService _apiService;
    private readonly UpdateVerificationService _updateVerificationService;
    private readonly UpdateInstallerService _updateInstallerService;
    private readonly AutostartService _autostartService;
    private readonly ModTranslationViewModel _modTranslation;

    private readonly string _clientVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

    private string _currentSection = "ModTranslation";
    private string _activationKey = string.Empty;
    private string _statusMessage = "Готово к работе.";
    private bool _isBusy;
    private bool _isActivated;
    private string _licenseStatus = "Не активировано";
    private string _licenseType = "-";
    private string _expiryText = "-";
    private string _serverConnectionText = "Сервер недоступен";
    private string _serverHealthText = "Проверка не выполнялась";
    private string _updateStatusText = "Проверка обновлений не запускалась";
    private string _lastSyncText = "Нет данных";
    private string _customerText = "-";
    private string _devicesText = "-";
    private string _diagnosticsText = "Диагностика не выполнялась.";
    private string _serverBaseUrl = string.Empty;
    private bool _autoCheckUpdates;
    private bool _enableAutostart;
    private bool _requireCertificatePinning;
    private string _pinnedCertificatesText = string.Empty;
    private string _updatePublicKeyPath = string.Empty;
    private string _stellarisUserDataPath = string.Empty;
    private string _steamRootPath = string.Empty;
    private string _submodOutputRoot = string.Empty;
    private bool _hasPendingUpdate;
    private string _pendingUpdateVersion = "-";

    private UpdatePackageDto? _pendingUpdate;

    public MainViewModel(
        ClientSettingsStore settingsStore,
        SecureTokenStore tokenStore,
        ClientLogService logService,
        ClientApiService apiService,
        UpdateVerificationService updateVerificationService,
        UpdateInstallerService updateInstallerService,
        AutostartService autostartService,
        ModTranslationViewModel modTranslation)
    {
        _settingsStore = settingsStore;
        _tokenStore = tokenStore;
        _logService = logService;
        _apiService = apiService;
        _updateVerificationService = updateVerificationService;
        _updateInstallerService = updateInstallerService;
        _autostartService = autostartService;
        _modTranslation = modTranslation;

        NavigateCommand = new RelayCommand<string>(section => CurrentSection = section ?? "Dashboard");
        ActivateCommand = new AsyncRelayCommand(ActivateAsync, onError: ex => HandleUnhandledException(ex, "Активация"));
        SyncCommand = new AsyncRelayCommand(() => RefreshStateAsync(true), onError: ex => HandleUnhandledException(ex, "Синхронизация"));
        CheckUpdatesCommand = new AsyncRelayCommand(CheckUpdatesAsync, onError: ex => HandleUnhandledException(ex, "Проверка обновлений"));
        InstallUpdateCommand = new AsyncRelayCommand(InstallPendingUpdateAsync, () => HasPendingUpdate, ex => HandleUnhandledException(ex, "Установка обновления"));
        SaveSettingsCommand = new RelayCommand(SaveSettings);
        RefreshLogsCommand = new RelayCommand(LoadLogs);
        ResetSessionCommand = new RelayCommand(() =>
        {
            _tokenStore.Clear();
            IsActivated = false;
            LicenseStatus = "Не активировано";
            StatusMessage = "Локальная сессия удалена.";
        });

        LoadSettings();
        LoadLogs();
    }

    public ObservableCollection<ClientLogEntry> Logs { get; } = [];

    public string CurrentSection
    {
        get => _currentSection;
        set
        {
            if (SetProperty(ref _currentSection, value))
            {
                Raise(nameof(CurrentSectionTitle));
            }
        }
    }

    public string ActivationKey
    {
        get => _activationKey;
        set
        {
            if (SetProperty(ref _activationKey, value))
            {
                ActivateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                Raise(nameof(IsReady));
            }
        }
    }

    public bool IsReady => !IsBusy;

    public bool IsActivated
    {
        get => _isActivated;
        set => SetProperty(ref _isActivated, value);
    }

    public string LicenseStatus
    {
        get => _licenseStatus;
        set => SetProperty(ref _licenseStatus, value);
    }

    public string LicenseType
    {
        get => _licenseType;
        set => SetProperty(ref _licenseType, value);
    }

    public string ExpiryText
    {
        get => _expiryText;
        set => SetProperty(ref _expiryText, value);
    }

    public string ServerConnectionText
    {
        get => _serverConnectionText;
        set => SetProperty(ref _serverConnectionText, value);
    }

    public string ServerHealthText
    {
        get => _serverHealthText;
        set => SetProperty(ref _serverHealthText, value);
    }

    public string UpdateStatusText
    {
        get => _updateStatusText;
        set => SetProperty(ref _updateStatusText, value);
    }

    public string LastSyncText
    {
        get => _lastSyncText;
        set => SetProperty(ref _lastSyncText, value);
    }

    public string CustomerText
    {
        get => _customerText;
        set => SetProperty(ref _customerText, value);
    }

    public string DevicesText
    {
        get => _devicesText;
        set => SetProperty(ref _devicesText, value);
    }

    public string DiagnosticsText
    {
        get => _diagnosticsText;
        set => SetProperty(ref _diagnosticsText, value);
    }

    public string ServerBaseUrl
    {
        get => _serverBaseUrl;
        set => SetProperty(ref _serverBaseUrl, value);
    }

    public bool AutoCheckUpdates
    {
        get => _autoCheckUpdates;
        set => SetProperty(ref _autoCheckUpdates, value);
    }

    public bool EnableAutostart
    {
        get => _enableAutostart;
        set => SetProperty(ref _enableAutostart, value);
    }

    public bool RequireCertificatePinning
    {
        get => _requireCertificatePinning;
        set => SetProperty(ref _requireCertificatePinning, value);
    }

    public string PinnedCertificatesText
    {
        get => _pinnedCertificatesText;
        set => SetProperty(ref _pinnedCertificatesText, value);
    }

    public string UpdatePublicKeyPath
    {
        get => _updatePublicKeyPath;
        set => SetProperty(ref _updatePublicKeyPath, value);
    }

    public string StellarisUserDataPath
    {
        get => _stellarisUserDataPath;
        set => SetProperty(ref _stellarisUserDataPath, value);
    }

    public string SteamRootPath
    {
        get => _steamRootPath;
        set => SetProperty(ref _steamRootPath, value);
    }

    public string SubmodOutputRoot
    {
        get => _submodOutputRoot;
        set => SetProperty(ref _submodOutputRoot, value);
    }

    public bool HasPendingUpdate
    {
        get => _hasPendingUpdate;
        set
        {
            if (SetProperty(ref _hasPendingUpdate, value))
            {
                InstallUpdateCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string PendingUpdateVersion
    {
        get => _pendingUpdateVersion;
        set => SetProperty(ref _pendingUpdateVersion, value);
    }

    public string ClientVersion => _clientVersion;

    public string CurrentSectionTitle => CurrentSection switch
    {
        "Dashboard" => "Главная",
        "ModTranslation" => "Перевод модов",
        "Diagnostics" => "Перевод модов",
        "Logs" => "Журнал",
        "Settings" => "Настройки",
        "About" => "О программе",
        _ => CurrentSection
    };

    public ModTranslationViewModel ModTranslation => _modTranslation;

    public string AboutText =>
        $"Secure License Platform Client{Environment.NewLine}" +
        $"Версия: {_clientVersion}{Environment.NewLine}" +
        "Среда: Windows / .NET 8" + Environment.NewLine +
        "Режим: демонстрационный клиент лицензирования";

    public RelayCommand<string> NavigateCommand { get; }

    public AsyncRelayCommand ActivateCommand { get; }

    public AsyncRelayCommand SyncCommand { get; }

    public AsyncRelayCommand CheckUpdatesCommand { get; }

    public AsyncRelayCommand InstallUpdateCommand { get; }

    public RelayCommand SaveSettingsCommand { get; }

    public RelayCommand RefreshLogsCommand { get; }

    public RelayCommand ResetSessionCommand { get; }

    public async Task InitializeAsync()
    {
        try
        {
            IsActivated = _tokenStore.Load() is not null;
            await RefreshStateAsync(false);
            await _modTranslation.InitializeAsync(IsActivated);
            if (IsActivated && AutoCheckUpdates)
            {
                await CheckUpdatesAsync();
            }
        }
        catch (Exception ex)
        {
            HandleUnhandledException(ex, "Инициализация");
        }
    }

    private async Task ActivateAsync()
    {
        if (string.IsNullOrWhiteSpace(ActivationKey))
        {
            StatusMessage = "Введите лицензионный ключ.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Выполняется активация лицензии...";

            var response = await _apiService.ActivateAsync(ActivationKey, _clientVersion, CancellationToken.None);
            if (response is null)
            {
                StatusMessage = "Не удалось активировать лицензию.";
                return;
            }

            IsActivated = true;
            ApplyLicense(response.License);
            ApplyServer(response.Server);
            LastSyncText = DateTimeOffset.Now.ToString("g");
            StatusMessage = "Лицензия успешно активирована.";
            ActivationKey = string.Empty;
            _modTranslation.UpdateSessionAvailability(true);
            await _modTranslation.RefreshRemoteDataAsync();
            LoadLogs();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void HandleUnhandledException(Exception exception, string operation)
    {
        var message = exception switch
        {
            HttpRequestException => "Не удалось подключиться к серверу. Проверьте адрес сервера и доверие к сертификату.",
            AuthenticationException => "Сертификат сервера не прошёл проверку. Нужно доверить сертификат на этом ПК.",
            _ => $"Во время операции \"{operation}\" произошла ошибка."
        };

        StatusMessage = message;
        DiagnosticsText = $"{operation}:{Environment.NewLine}{exception.Message}";
        _logService.Write("Error", operation, message, new { exception = exception.ToString() });
        LoadLogs();
    }

    private async Task RefreshStateAsync(bool heartbeat)
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Выполняется синхронизация...";

            var server = await _apiService.GetServerInfoAsync(CancellationToken.None);
            if (server is not null)
            {
                ApplyServer(server);
            }

            if (!IsActivated)
            {
                StatusMessage = "Ожидается активация лицензии.";
                return;
            }

            var sync = heartbeat
                ? await _apiService.HeartbeatAsync(_clientVersion, null, CancellationToken.None)
                : await _apiService.GetStatusAsync(_clientVersion, CancellationToken.None);

            if (sync is null)
            {
                ApplyOfflineState();
                return;
            }

            ApplyLicense(sync.License);
            ApplyServer(sync.Server);
            LastSyncText = sync.LastSynchronizedUtc.ToLocalTime().ToString("g");
            DiagnosticsText =
                $"API: доступен{Environment.NewLine}" +
                $"Сервер: {sync.Server.ServerName}{Environment.NewLine}" +
                $"База: {(sync.Server.IsDatabaseReachable ? "доступна" : "недоступна")}{Environment.NewLine}" +
                $"Redis: {(sync.Server.IsRedisReachable ? "доступен" : "недоступен")}";
            StatusMessage = sync.LicenseValid ? "Синхронизация завершена." : "Приложение перешло в ограниченный режим.";

            var telemetry = _logService.ReadRecent(20);
            if (telemetry.Count > 0)
            {
                await _apiService.SendTelemetryAsync(telemetry, _clientVersion, CancellationToken.None);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task CheckUpdatesAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Проверяются доступные обновления...";

            var response = await _apiService.CheckUpdatesAsync(_clientVersion, CancellationToken.None);
            if (response is null)
            {
                UpdateStatusText = "Не удалось проверить обновления.";
                return;
            }

            if (!response.UpdateAvailable || response.Package is null)
            {
                _pendingUpdate = null;
                HasPendingUpdate = false;
                PendingUpdateVersion = "-";
                UpdateStatusText = response.Message;
                return;
            }

            _pendingUpdate = response.Package;
            HasPendingUpdate = true;
            PendingUpdateVersion = response.Package.Version;
            UpdateStatusText = response.Package.Mandatory
                ? $"Доступно обязательное обновление {response.Package.Version}."
                : $"Доступно обновление {response.Package.Version}.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task InstallPendingUpdateAsync()
    {
        if (_pendingUpdate is null)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = "Скачивается пакет обновления...";

            var downloadedPath = await _apiService.DownloadUpdateAsync(_clientVersion, _pendingUpdate, CancellationToken.None);
            if (string.IsNullOrWhiteSpace(downloadedPath))
            {
                StatusMessage = "Не удалось скачать пакет обновления.";
                return;
            }

            if (!_updateVerificationService.VerifyPackage(_pendingUpdate, downloadedPath))
            {
                UpdateStatusText = "Проверка подписи или целостности обновления не пройдена.";
                _logService.Write("Error", "Обновление", "Подпись обновления не прошла проверку.", new { downloadedPath });
                return;
            }

            var result = await _updateInstallerService.InstallAsync(downloadedPath, CancellationToken.None);
            UpdateStatusText = result.Message;
            StatusMessage = result.Message;

            if (result.Succeeded && string.Equals(Path.GetExtension(downloadedPath), ".zip", StringComparison.OrdinalIgnoreCase))
            {
                await Task.Delay(900);
                System.Windows.Application.Current.Shutdown();
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SaveSettings()
    {
        var current = _settingsStore.Load();
        var settings = new ClientSettings
        {
            ServerBaseUrl = ServerBaseUrl,
            AutoCheckUpdates = AutoCheckUpdates,
            EnableAutostart = EnableAutostart,
            RequireCertificatePinning = RequireCertificatePinning,
            PinnedSpkiSha256 = ParsePins(PinnedCertificatesText),
            UpdatePublicKeyPath = UpdatePublicKeyPath,
            StellarisUserDataPath = StellarisUserDataPath,
            SteamRootPath = SteamRootPath,
            SubmodOutputRoot = SubmodOutputRoot,
            PreferredChannel = current.PreferredChannel,
            InstallationId = current.InstallationId
        };

        _settingsStore.Save(settings);
        var processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            processPath = Path.Combine(AppContext.BaseDirectory, "Platform.Client.Wpf.exe");
        }

        _autostartService.Apply(processPath, EnableAutostart);
        StatusMessage = "Настройки сохранены.";
    }

    private void LoadLogs()
    {
        Logs.Clear();
        foreach (var entry in _logService.ReadRecent())
        {
            Logs.Add(entry);
        }
    }

    private void LoadSettings()
    {
        var settings = _settingsStore.Load();
        ServerBaseUrl = settings.ServerBaseUrl;
        AutoCheckUpdates = settings.AutoCheckUpdates;
        EnableAutostart = settings.EnableAutostart;
        RequireCertificatePinning = settings.RequireCertificatePinning;
        PinnedCertificatesText = string.Join(Environment.NewLine, settings.PinnedSpkiSha256);
        UpdatePublicKeyPath = settings.UpdatePublicKeyPath;
        StellarisUserDataPath = settings.StellarisUserDataPath;
        SteamRootPath = settings.SteamRootPath;
        SubmodOutputRoot = settings.SubmodOutputRoot;

        if (string.IsNullOrWhiteSpace(UpdatePublicKeyPath))
        {
            UpdatePublicKeyPath = _updateVerificationService.GetEffectivePublicKeyPath() ?? string.Empty;
        }
    }

    private void ApplyLicense(LicenseStatusDto license)
    {
        LicenseStatus = license.Status;
        LicenseType = license.Type.ToString();
        ExpiryText = license.ExpiresUtc?.ToLocalTime().ToString("g") ?? "Бессрочно";
        CustomerText = $"{license.CustomerName} / {license.CustomerEmail}";
        DevicesText = $"{license.ActiveDevices} из {license.MaxDevices}";
    }

    private void ApplyServer(ServerInfoDto server)
    {
        ServerConnectionText = server.ServerName;
        ServerHealthText = server.IsDatabaseReachable && server.IsRedisReachable
            ? "API, БД и Redis доступны"
            : $"API: OK, БД: {(server.IsDatabaseReachable ? "OK" : "Ошибка")}, Redis: {(server.IsRedisReachable ? "OK" : "Ошибка")}";
    }

    private void ApplyOfflineState()
    {
        var session = _tokenStore.Load();
        if (session?.LastKnownLicense is null)
        {
            LicenseStatus = "Нет связи";
            StatusMessage = "Сервер временно недоступен.";
            return;
        }

        var graceEnds = session.LastValidatedUtc.AddHours(session.LastKnownLicense.OfflineGracePeriodHours);
        if (DateTimeOffset.UtcNow <= graceEnds)
        {
            LicenseStatus = "Нет связи";
            StatusMessage = $"Работа в grace period до {graceEnds.ToLocalTime():g}.";
            ApplyLicense(session.LastKnownLicense);
        }
        else
        {
            LicenseStatus = "Ограниченный режим";
            StatusMessage = "Grace period истёк. Требуется соединение с сервером.";
        }
    }

    private static List<string> ParsePins(string text) =>
        text.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .ToList();
}
