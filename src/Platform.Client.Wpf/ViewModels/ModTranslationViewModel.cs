using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Platform.Application.Models;
using Platform.Client.Core.Models;
using Platform.Client.Core.Services;

namespace Platform.Client.Wpf.ViewModels;

public sealed class ModTranslationViewModel : ObservableObject
{
    private readonly StellarisModDiscoveryService _modDiscoveryService;
    private readonly ClientTranslationApiService _translationApiService;
    private readonly SubmodBuildService _submodBuildService;
    private readonly ClientLogService _logService;

    private readonly string _clientVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

    private CancellationTokenSource? _pollingCts;
    private bool _isSessionAvailable;
    private bool _isCatalogBusy;
    private bool _isAnalyzeBusy;
    private bool _isBuildBusy;
    private string _catalogStatus = "Каталог модов ещё не сканировался.";
    private string _quotaSummary = "Квота перевода не загружена.";
    private string _glossarySummary = "Глоссарии ещё не загружены.";
    private string _translationStatus = "Задание на перевод не запускалось.";
    private string _buildStatus = "Сабмод ещё не собирался.";
    private string _selectedModSummary = "Выберите мод Stellaris для анализа и перевода.";
    private string _buildPreviewText = "После анализа появится предварительный план сборки сабмода.";
    private string _requestedSubmodName = string.Empty;
    private string _sourceLanguage = "en";
    private string _targetLanguage = "ru";
    private bool _createBackup = true;
    private bool _dryRunBuild = true;
    private double _jobProgressPercent;
    private string _jobStatusLabel = "Нет активного задания.";
    private string _downloadedArtifactPath = string.Empty;
    private string _outputFolderPath = string.Empty;
    private string _backupPath = string.Empty;
    private StellarisModDescriptor? _selectedMod;
    private AnalyzeModResponse? _latestAnalysis;
    private TranslationJobStatusDto? _latestJob;

    public ModTranslationViewModel(
        StellarisModDiscoveryService modDiscoveryService,
        ClientTranslationApiService translationApiService,
        SubmodBuildService submodBuildService,
        ClientLogService logService)
    {
        _modDiscoveryService = modDiscoveryService;
        _translationApiService = translationApiService;
        _submodBuildService = submodBuildService;
        _logService = logService;

        ScanModsCommand = new AsyncRelayCommand(ScanModsAsync, () => !IsCatalogBusy, HandleException);
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, () => SelectedMod is not null && IsSessionAvailable && !IsAnalyzeBusy, HandleException);
        StartTranslationCommand = new AsyncRelayCommand(StartTranslationAsync, () => SelectedMod is not null && IsSessionAvailable && !IsAnalyzeBusy, HandleException);
        RefreshJobCommand = new AsyncRelayCommand(RefreshJobAsync, () => LatestJobId is not null && IsSessionAvailable, HandleException);
        CancelJobCommand = new AsyncRelayCommand(CancelJobAsync, () => CanCancelJob, HandleException);
        DownloadResultCommand = new AsyncRelayCommand(DownloadResultAsync, () => CanDownloadResult, HandleException);
        BuildSubmodCommand = new AsyncRelayCommand(BuildSubmodAsync, () => CanBuildSubmod, HandleException);
        OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder, () => Directory.Exists(OutputFolderPath));
        OpenSelectedModFolderCommand = new RelayCommand(OpenSelectedModFolder, () => SelectedMod is not null && Directory.Exists(SelectedMod.RootPath));
    }

    public ObservableCollection<StellarisModDescriptor> AvailableMods { get; } = [];

    public ObservableCollection<StellarisLocalizationFile> SelectedLocalizationFiles { get; } = [];

    public ObservableCollection<LanguageOptionDto> AvailableLanguages { get; } = [];

    public ObservableCollection<ActiveGlossaryDto> ActiveGlossaries { get; } = [];

    public ObservableCollection<TranslationFileResultDto> ResultFiles { get; } = [];

    public AsyncRelayCommand ScanModsCommand { get; }

    public AsyncRelayCommand AnalyzeCommand { get; }

    public AsyncRelayCommand StartTranslationCommand { get; }

    public AsyncRelayCommand RefreshJobCommand { get; }

    public AsyncRelayCommand CancelJobCommand { get; }

    public AsyncRelayCommand DownloadResultCommand { get; }

    public AsyncRelayCommand BuildSubmodCommand { get; }

    public RelayCommand OpenOutputFolderCommand { get; }

    public RelayCommand OpenSelectedModFolderCommand { get; }

    public bool IsSessionAvailable
    {
        get => _isSessionAvailable;
        private set
        {
            if (SetProperty(ref _isSessionAvailable, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public bool IsCatalogBusy
    {
        get => _isCatalogBusy;
        private set
        {
            if (SetProperty(ref _isCatalogBusy, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public bool IsAnalyzeBusy
    {
        get => _isAnalyzeBusy;
        private set
        {
            if (SetProperty(ref _isAnalyzeBusy, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public bool IsBuildBusy
    {
        get => _isBuildBusy;
        private set
        {
            if (SetProperty(ref _isBuildBusy, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public string CatalogStatus
    {
        get => _catalogStatus;
        private set => SetProperty(ref _catalogStatus, value);
    }

    public string QuotaSummary
    {
        get => _quotaSummary;
        private set => SetProperty(ref _quotaSummary, value);
    }

    public string GlossarySummary
    {
        get => _glossarySummary;
        private set => SetProperty(ref _glossarySummary, value);
    }

    public string TranslationStatus
    {
        get => _translationStatus;
        private set => SetProperty(ref _translationStatus, value);
    }

    public string BuildStatus
    {
        get => _buildStatus;
        private set => SetProperty(ref _buildStatus, value);
    }

    public string SelectedModSummary
    {
        get => _selectedModSummary;
        private set => SetProperty(ref _selectedModSummary, value);
    }

    public string BuildPreviewText
    {
        get => _buildPreviewText;
        private set => SetProperty(ref _buildPreviewText, value);
    }

    public string RequestedSubmodName
    {
        get => _requestedSubmodName;
        set
        {
            if (SetProperty(ref _requestedSubmodName, value))
            {
                RebuildPreview();
            }
        }
    }

    public string SourceLanguage
    {
        get => _sourceLanguage;
        set => SetProperty(ref _sourceLanguage, value);
    }

    public string TargetLanguage
    {
        get => _targetLanguage;
        set
        {
            if (SetProperty(ref _targetLanguage, value))
            {
                RebuildPreview();
            }
        }
    }

    public bool CreateBackup
    {
        get => _createBackup;
        set
        {
            if (SetProperty(ref _createBackup, value))
            {
                RebuildPreview();
            }
        }
    }

    public bool DryRunBuild
    {
        get => _dryRunBuild;
        set
        {
            if (SetProperty(ref _dryRunBuild, value))
            {
                RebuildPreview();
            }
        }
    }

    public double JobProgressPercent
    {
        get => _jobProgressPercent;
        private set => SetProperty(ref _jobProgressPercent, value);
    }

    public string JobStatusLabel
    {
        get => _jobStatusLabel;
        private set => SetProperty(ref _jobStatusLabel, value);
    }

    public string DownloadedArtifactPath
    {
        get => _downloadedArtifactPath;
        private set
        {
            if (SetProperty(ref _downloadedArtifactPath, value))
            {
                UpdateCommandStates();
            }
        }
    }

    public string OutputFolderPath
    {
        get => _outputFolderPath;
        private set
        {
            if (SetProperty(ref _outputFolderPath, value))
            {
                OpenOutputFolderCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string BackupPath
    {
        get => _backupPath;
        private set => SetProperty(ref _backupPath, value);
    }

    public Guid? LatestJobId => _latestJob?.JobId;

    public bool CanCancelJob =>
        _latestJob is not null &&
        _latestJob.Status is not "Completed" and not "Failed" and not "Cancelled";

    public bool CanDownloadResult => _latestJob?.DownloadAvailable == true;

    public bool CanBuildSubmod => !string.IsNullOrWhiteSpace(DownloadedArtifactPath) || CanDownloadResult;

    public StellarisModDescriptor? SelectedMod
    {
        get => _selectedMod;
        set
        {
            if (!SetProperty(ref _selectedMod, value))
            {
                return;
            }

            _latestAnalysis = null;
            _latestJob = null;
            DownloadedArtifactPath = string.Empty;
            OutputFolderPath = string.Empty;
            BackupPath = string.Empty;
            JobProgressPercent = 0;
            JobStatusLabel = "Нет активного задания.";
            TranslationStatus = "Задание на перевод не запускалось.";

            SelectedLocalizationFiles.Clear();
            if (value is not null)
            {
                foreach (var file in value.LocalizationFiles)
                {
                    SelectedLocalizationFiles.Add(file);
                }

                SourceLanguage = value.LocalizationFiles.FirstOrDefault()?.SourceLanguage ?? "en";
                RequestedSubmodName = $"[RU] {value.Name} (Auto Translation)";
                SelectedModSummary =
                    $"{value.Name}{Environment.NewLine}" +
                    $"Источник: {value.SourceLabel}{Environment.NewLine}" +
                    $"Файлов локализации: {value.LocalizationFiles.Count}{Environment.NewLine}" +
                    $"Поддерживаемая версия: {value.SupportedVersion ?? "не указана"}{Environment.NewLine}" +
                    $"Путь: {value.RootPath}";
            }
            else
            {
                RequestedSubmodName = string.Empty;
                SelectedModSummary = "Выберите мод Stellaris для анализа и перевода.";
            }

            RebuildPreview();
            UpdateCommandStates();
            OpenSelectedModFolderCommand.RaiseCanExecuteChanged();
        }
    }

    public async Task InitializeAsync(bool sessionAvailable)
    {
        IsSessionAvailable = sessionAvailable;
        await ScanModsAsync();
        if (sessionAvailable)
        {
            await RefreshRemoteDataAsync();
        }
    }

    public void UpdateSessionAvailability(bool sessionAvailable)
    {
        IsSessionAvailable = sessionAvailable;
        if (!sessionAvailable)
        {
            CancelPolling();
            QuotaSummary = "Для перевода нужна активная клиентская сессия.";
            GlossarySummary = "Словари станут доступны после активации лицензии.";
        }
    }

    public async Task RefreshRemoteDataAsync()
    {
        if (!IsSessionAvailable)
        {
            return;
        }

        var quota = await _translationApiService.GetCurrentQuotaAsync(_clientVersion, CancellationToken.None);
        QuotaSummary = quota.Succeeded && quota.Data is not null
            ? $"Символов на сутки: {quota.Data.RemainingCharactersToday} из {quota.Data.MaxCharactersPerDay}; заданий в час: {quota.Data.RemainingJobsThisHour} из {quota.Data.MaxJobsPerHour}."
            : quota.Message;

        var languages = await _translationApiService.GetLanguagesAsync(_clientVersion, CancellationToken.None);
        AvailableLanguages.Clear();
        if (languages.Data is not null)
        {
            foreach (var language in languages.Data)
            {
                AvailableLanguages.Add(language);
            }
        }

        var glossaries = await _translationApiService.GetActiveGlossariesAsync(_clientVersion, CancellationToken.None);
        ActiveGlossaries.Clear();
        if (glossaries.Data is not null)
        {
            foreach (var glossary in glossaries.Data)
            {
                ActiveGlossaries.Add(glossary);
            }
        }

        GlossarySummary = ActiveGlossaries.Count == 0
            ? "Активные словари не назначены."
            : string.Join(", ", ActiveGlossaries.Select(x => $"{x.Name} ({x.Scope})"));
    }

    private async Task ScanModsAsync()
    {
        try
        {
            IsCatalogBusy = true;
            CatalogStatus = "Сканирую локальные и workshop-моды Stellaris...";

            var mods = await _modDiscoveryService.DiscoverAsync(CancellationToken.None);
            AvailableMods.Clear();
            foreach (var mod in mods)
            {
                AvailableMods.Add(mod);
            }

            CatalogStatus = mods.Count == 0
                ? "Моды Stellaris не найдены. Проверьте путь к каталогу модов или Steam."
                : $"Найдено модов: {mods.Count}. Выберите мод для анализа.";

            if (SelectedMod is null && AvailableMods.Count > 0)
            {
                SelectedMod = AvailableMods[0];
            }
        }
        finally
        {
            IsCatalogBusy = false;
        }
    }

    private async Task AnalyzeAsync()
    {
        if (SelectedMod is null)
        {
            return;
        }

        try
        {
            IsAnalyzeBusy = true;
            TranslationStatus = "Отправляю описание мода на сервер для анализа...";

            var request = BuildAnalyzeRequest(SelectedMod);
            var result = await _translationApiService.AnalyzeAsync(request, _clientVersion, CancellationToken.None);
            if (!result.Succeeded || result.Data is null)
            {
                TranslationStatus = result.Message;
                return;
            }

            _latestAnalysis = result.Data;
            TranslationStatus =
                $"Анализ завершён: файлов {result.Data.FileCount}, сегментов {result.Data.SegmentCount}, символов {result.Data.CharacterCount}.";
            RebuildPreview();
        }
        finally
        {
            IsAnalyzeBusy = false;
        }
    }

    private async Task StartTranslationAsync()
    {
        if (SelectedMod is null)
        {
            return;
        }

        if (_latestAnalysis is null)
        {
            await AnalyzeAsync();
            if (_latestAnalysis is null)
            {
                return;
            }
        }

        try
        {
            IsAnalyzeBusy = true;
            TranslationStatus = "Создаю задание перевода на сервере...";

            var request = new CreateTranslationJobRequest(
                _latestAnalysis.SnapshotId,
                SelectedMod.Name,
                SelectedMod.OriginalReference,
                SourceLanguage,
                TargetLanguage,
                RequestedSubmodName,
                "yandex",
                BuildUploadedFiles(SelectedMod));

            var result = await _translationApiService.CreateJobAsync(
                request,
                _clientVersion,
                Guid.NewGuid().ToString("N"),
                CancellationToken.None);

            if (!result.Succeeded || result.Data is null)
            {
                TranslationStatus = result.Message;
                return;
            }

            TranslationStatus = result.Data.Message;
            JobStatusLabel = result.Data.Status;
            await RefreshRemoteDataAsync();
            await RefreshJobCoreAsync(result.Data.JobId);
            StartPolling(result.Data.JobId);
        }
        finally
        {
            IsAnalyzeBusy = false;
        }
    }

    private async Task RefreshJobAsync()
    {
        if (LatestJobId is Guid jobId)
        {
            await RefreshJobCoreAsync(jobId);
        }
    }

    private async Task CancelJobAsync()
    {
        if (LatestJobId is not Guid jobId)
        {
            return;
        }

        var result = await _translationApiService.CancelJobAsync(jobId, _clientVersion, CancellationToken.None);
        TranslationStatus = result.Message;
        if (result.Succeeded)
        {
            await RefreshJobCoreAsync(jobId);
        }
    }

    private async Task DownloadResultAsync()
    {
        if (LatestJobId is not Guid jobId)
        {
            return;
        }

        var result = await _translationApiService.DownloadJobResultAsync(jobId, _clientVersion, CancellationToken.None);
        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.Data))
        {
            BuildStatus = result.Message;
            return;
        }

        DownloadedArtifactPath = result.Data;
        BuildStatus = $"Архив результата сохранён: {DownloadedArtifactPath}";
        UpdateCommandStates();
    }

    private async Task BuildSubmodAsync()
    {
        if (SelectedMod is null)
        {
            return;
        }

        try
        {
            IsBuildBusy = true;
            if (string.IsNullOrWhiteSpace(DownloadedArtifactPath))
            {
                await DownloadResultAsync();
            }

            if (string.IsNullOrWhiteSpace(DownloadedArtifactPath))
            {
                return;
            }

            var result = await _submodBuildService.BuildFromPackageAsync(
                SelectedMod,
                DownloadedArtifactPath,
                RequestedSubmodName,
                DryRunBuild,
                CreateBackup,
                CancellationToken.None);

            BuildStatus = result.Message;
            if (result.Succeeded)
            {
                OutputFolderPath = result.OutputFolderPath;
                BackupPath = result.BackupPath ?? string.Empty;
            }
        }
        finally
        {
            IsBuildBusy = false;
        }
    }

    private async Task RefreshJobCoreAsync(Guid jobId)
    {
        var status = await _translationApiService.GetJobAsync(jobId, _clientVersion, CancellationToken.None);
        if (!status.Succeeded || status.Data is null)
        {
            TranslationStatus = status.Message;
            return;
        }

        _latestJob = status.Data;
        JobStatusLabel = status.Data.Status;
        JobProgressPercent = status.Data.TotalSegments == 0
            ? 0
            : Math.Round(status.Data.ProcessedSegments * 100d / status.Data.TotalSegments, 1);
        TranslationStatus =
            $"Статус: {status.Data.Status}. Выполнено сегментов: {status.Data.ProcessedSegments} из {status.Data.TotalSegments}.";

        if (status.Data.ManifestPreview is not null)
        {
            var preview = _submodBuildService.CreatePreview(SelectedMod!, status.Data.ManifestPreview, RequestedSubmodName, DryRunBuild, CreateBackup);
            BuildPreviewText =
                $"{preview.SubmodName}{Environment.NewLine}" +
                $"Каталог: {preview.OutputFolderPath}{Environment.NewLine}" +
                $"Файлов для записи: {preview.OutputFiles.Count}{Environment.NewLine}" +
                $"{string.Join(Environment.NewLine, preview.Notes)}";
        }

        var files = await _translationApiService.GetJobFilesAsync(jobId, _clientVersion, CancellationToken.None);
        ResultFiles.Clear();
        if (files.Data is not null)
        {
            foreach (var file in files.Data)
            {
                ResultFiles.Add(file);
            }
        }

        UpdateCommandStates();

        if (status.Data.Status is "Completed" or "Failed" or "Cancelled")
        {
            CancelPolling();
        }
    }

    private void StartPolling(Guid jobId)
    {
        CancelPolling();
        _pollingCts = new CancellationTokenSource();
        _ = PollJobLoopAsync(jobId, _pollingCts.Token);
    }

    private async Task PollJobLoopAsync(Guid jobId, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(4), cancellationToken);
                await RefreshJobCoreAsync(jobId);

                if (_latestJob?.Status is "Completed" or "Failed" or "Cancelled")
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal flow.
        }
        catch (Exception ex)
        {
            HandleException(ex);
        }
    }

    private void CancelPolling()
    {
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        _pollingCts = null;
    }

    private AnalyzeModRequest BuildAnalyzeRequest(StellarisModDescriptor mod) =>
        new(
            mod.Name,
            mod.Version ?? string.Empty,
            mod.OriginalReference,
            SourceLanguage,
            BuildUploadedFiles(mod));

    private IReadOnlyCollection<UploadedLocalizationFileDto> BuildUploadedFiles(StellarisModDescriptor mod) =>
        mod.LocalizationFiles
            .Select(file => new UploadedLocalizationFileDto(
                file.RelativePath,
                File.ReadAllText(file.FullPath),
                file.SourceLanguage,
                file.Sha256,
                file.SizeBytes))
            .ToArray();

    private void OpenOutputFolder()
    {
        if (!Directory.Exists(OutputFolderPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{OutputFolderPath}\"") { UseShellExecute = true });
    }

    private void OpenSelectedModFolder()
    {
        var selectedMod = SelectedMod;
        if (selectedMod is null || !Directory.Exists(selectedMod.RootPath))
        {
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{selectedMod.RootPath}\"") { UseShellExecute = true });
    }

    private void RebuildPreview()
    {
        if (SelectedMod is null)
        {
            BuildPreviewText = "После выбора мода здесь появится план сборки сабмода.";
            return;
        }

        var manifest = _latestJob?.ManifestPreview
            ?? new SubmodManifestPreviewDto(
                RequestedSubmodName,
                "descriptor.mod",
                TargetLanguage,
                SelectedMod.LocalizationFiles.Select(x => x.RelativePath).ToArray(),
                new[]
                {
                    "Оригинальный мод не изменяется.",
                    "Сабмод будет создан отдельно и сможет обновляться независимо.",
                    "Перед записью можно выполнить dry-run и проверить целевые пути."
                });

        var preview = _submodBuildService.CreatePreview(SelectedMod, manifest, RequestedSubmodName, DryRunBuild, CreateBackup);
        BuildPreviewText =
            $"{preview.SubmodName}{Environment.NewLine}" +
            $"Каталог: {preview.OutputFolderPath}{Environment.NewLine}" +
            $"Файлов для записи: {preview.OutputFiles.Count}{Environment.NewLine}" +
            $"Режим: {(preview.DryRun ? "dry-run" : "запись на диск")}{Environment.NewLine}" +
            $"{string.Join(Environment.NewLine, preview.Notes)}";
    }

    private void UpdateCommandStates()
    {
        ScanModsCommand.RaiseCanExecuteChanged();
        AnalyzeCommand.RaiseCanExecuteChanged();
        StartTranslationCommand.RaiseCanExecuteChanged();
        RefreshJobCommand.RaiseCanExecuteChanged();
        CancelJobCommand.RaiseCanExecuteChanged();
        DownloadResultCommand.RaiseCanExecuteChanged();
        BuildSubmodCommand.RaiseCanExecuteChanged();
        OpenOutputFolderCommand.RaiseCanExecuteChanged();
    }

    private void HandleException(Exception exception)
    {
        var message = $"Во время работы мастера перевода произошла ошибка: {exception.Message}";
        TranslationStatus = message;
        BuildStatus = message;
        _logService.Write("Error", "Перевод модов", message, new { exception = exception.ToString() });
    }
}
