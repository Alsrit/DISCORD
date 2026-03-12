using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using Platform.Client.Core.Configuration;
using Platform.Client.Core.Models;

namespace Platform.Client.Core.Services;

public sealed class DeviceIdentityService(ClientSettingsStore settingsStore)
{
    public DeviceProfile GetCurrentProfile()
    {
        var settings = settingsStore.Load();
        var machineName = Environment.MachineName;
        var deviceName = Environment.UserName;
        var os = Environment.OSVersion.VersionString;
        var machineGuid = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Cryptography", "MachineGuid", null)?.ToString() ?? machineName;

        var fingerprintSource = $"{machineGuid}|{machineName}|{os}";
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintSource)));

        return new DeviceProfile(
            settings.InstallationId,
            fingerprint,
            deviceName,
            machineName,
            os);
    }
}
