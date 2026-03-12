using System.Diagnostics;
using System.IO.Compression;
using Platform.Application.Abstractions;

namespace Platform.Client.Core.Services;

public sealed class UpdateInstallerService(ClientLogService logService, ClientPathService pathService)
{
    public async Task<OperationResult> InstallAsync(string packagePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(packagePath))
        {
            return OperationResult.Failure("Пакет обновления не найден.", "package_not_found");
        }

        var extension = Path.GetExtension(packagePath).ToLowerInvariant();
        return extension switch
        {
            ".zip" => await InstallPortableZipAsync(packagePath, cancellationToken),
            ".msix" or ".msixbundle" => await InstallMsixAsync(packagePath, cancellationToken),
            _ => OperationResult.Failure("Поддерживаются пакеты ZIP, MSIX и MSIXBundle.", "unsupported_package")
        };
    }

    private async Task<OperationResult> InstallPortableZipAsync(string packagePath, CancellationToken cancellationToken)
    {
        pathService.EnsureFolders();

        var updateId = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss");
        var workspaceRoot = Path.Combine(pathService.UpdatesPath, "install", updateId);
        var stagingRoot = Path.Combine(workspaceRoot, "staging");
        var backupRoot = Path.Combine(workspaceRoot, "backup");
        var scriptPath = Path.Combine(workspaceRoot, "apply-update.cmd");
        var scriptLogPath = Path.Combine(workspaceRoot, "apply-update.log");

        Directory.CreateDirectory(workspaceRoot);
        Directory.CreateDirectory(stagingRoot);

        ZipFile.ExtractToDirectory(packagePath, stagingRoot, overwriteFiles: true);

        var appDirectory = AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var currentExecutablePath = Environment.ProcessPath ?? Path.Combine(appDirectory, "Platform.Client.Wpf.exe");
        var executableName = Path.GetFileName(currentExecutablePath);
        var payloadRoot = ResolvePayloadRoot(stagingRoot, executableName);

        if (!File.Exists(Path.Combine(payloadRoot, executableName)))
        {
            logService.Write("Error", "Обновление", "В ZIP-пакете не найден исполняемый файл клиента.", new { packagePath, executableName, stagingRoot });
            return OperationResult.Failure("В пакете обновления не найден исполняемый файл клиента.", "portable_payload_invalid");
        }

        await File.WriteAllTextAsync(
            scriptPath,
            BuildPortableUpdateScript(
                Environment.ProcessId,
                appDirectory,
                payloadRoot,
                backupRoot,
                currentExecutablePath,
                scriptLogPath),
            cancellationToken);

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c start \"\" \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process is null)
        {
            return OperationResult.Failure("Не удалось подготовить применение ZIP-обновления.", "portable_installer_failed");
        }

        logService.Write("Information", "Обновление", "Подготовлено применение portable-обновления.", new
        {
            packagePath,
            scriptPath,
            backupRoot,
            payloadRoot
        });

        return OperationResult.Success("Обновление подготовлено. Приложение перезапустится и установит новую версию.");
    }

    private async Task<OperationResult> InstallMsixAsync(string packagePath, CancellationToken cancellationToken)
    {
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"Add-AppxPackage -ForceUpdateFromAnyVersion '{packagePath}'\"",
            UseShellExecute = false,
            CreateNoWindow = true
        });

        if (process is null)
        {
            return OperationResult.Failure("Не удалось запустить установщик обновления.", "installer_failed");
        }

        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            logService.Write("Error", "Обновление", "Установка пакета MSIX завершилась ошибкой.", new { process.ExitCode, packagePath });
            return OperationResult.Failure("Установка обновления завершилась с ошибкой.", "installer_failed");
        }

        logService.Write("Information", "Обновление", "Пакет обновления установлен.", new { packagePath });
        return OperationResult.Success("Обновление установлено.");
    }

    private static string ResolvePayloadRoot(string stagingRoot, string executableName)
    {
        if (File.Exists(Path.Combine(stagingRoot, executableName)))
        {
            return stagingRoot;
        }

        var directories = Directory.GetDirectories(stagingRoot);
        if (directories.Length == 1 && File.Exists(Path.Combine(directories[0], executableName)))
        {
            return directories[0];
        }

        return stagingRoot;
    }

    private static string BuildPortableUpdateScript(
        int processId,
        string targetDirectory,
        string sourceDirectory,
        string backupDirectory,
        string executablePath,
        string logPath)
    {
        return $$"""
@echo off
setlocal enableextensions
set "WAIT_PID={{processId}}"
set "TARGET={{targetDirectory}}"
set "SOURCE={{sourceDirectory}}"
set "BACKUP={{backupDirectory}}"
set "APP={{executablePath}}"
set "LOG={{logPath}}"

echo [%DATE% %TIME%] Starting portable update>"%LOG%"

for /L %%I in (1,1,90) do (
    tasklist /FI "PID eq %WAIT_PID%" | find "%WAIT_PID%" >nul
    if errorlevel 1 goto copy
    timeout /t 1 /nobreak >nul
)

echo [%DATE% %TIME%] Timeout waiting for client process exit>>"%LOG%"
exit /b 1

:copy
mkdir "%BACKUP%" 2>nul
robocopy "%TARGET%" "%BACKUP%" /E /R:1 /W:1 /NFL /NDL /NJH /NJS /NP >>"%LOG%"
robocopy "%SOURCE%" "%TARGET%" /MIR /R:1 /W:1 /NFL /NDL /NJH /NJS /NP >>"%LOG%"
if errorlevel 8 goto rollback

echo [%DATE% %TIME%] Update applied successfully>>"%LOG%"
start "" "%APP%"
exit /b 0

:rollback
echo [%DATE% %TIME%] Update failed, restoring backup>>"%LOG%"
robocopy "%BACKUP%" "%TARGET%" /MIR /R:1 /W:1 /NFL /NDL /NJH /NJS /NP >>"%LOG%"
exit /b 1
""";
    }
}
