using System.Diagnostics;
using Platform.Application.Abstractions;

namespace Platform.Client.Core.Services;

public sealed class UpdateInstallerService(ClientLogService logService)
{
    public async Task<OperationResult> InstallAsync(string packagePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(packagePath))
        {
            return OperationResult.Failure("Пакет обновления не найден.", "package_not_found");
        }

        var extension = Path.GetExtension(packagePath).ToLowerInvariant();
        if (extension is not ".msix" and not ".msixbundle")
        {
            return OperationResult.Failure("Поддерживается только установка MSIX/MSIXBundle.", "unsupported_package");
        }

        var process = Process.Start(new ProcessStartInfo
        {
            FileName = "powershell",
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
}
