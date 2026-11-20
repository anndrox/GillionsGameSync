using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;

namespace GillionsGameSync;

public sealed partial class PluginConfiguration {
    private string deviceToken = "";
    private string? legacyDeviceToken;

    [Newtonsoft.Json.JsonIgnore]
    [System.Text.Json.Serialization.JsonIgnore]
    public string DeviceToken {
        get => deviceToken;
        set {
            var protectedValue = string.IsNullOrEmpty(value) ? null : DeviceCredential.Protect(value);
            deviceToken = value ?? "";
            ProtectedDeviceToken = protectedValue;
        }
    }

    [Newtonsoft.Json.JsonProperty(NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
    public string? ProtectedDeviceToken { get; private set; }

    // Read old configurations without allowing any serializer to write this value back.
    [Newtonsoft.Json.JsonProperty("DeviceToken")]
    private string? LegacyDeviceToken { set => legacyDeviceToken = value; }

    [Newtonsoft.Json.JsonIgnore]
    internal bool CredentialStorageChanged { get; private set; }

    [OnDeserialized]
    private void RestoreDeviceCredential(StreamingContext context) {
        try {
            if (ProtectedDeviceToken is not null) {
                deviceToken = DeviceCredential.Unprotect(ProtectedDeviceToken);
                CredentialStorageChanged = legacyDeviceToken is not null;
            } else if (!string.IsNullOrEmpty(legacyDeviceToken)) {
                DeviceToken = legacyDeviceToken;
                CredentialStorageChanged = true;
            }
        } catch (CryptographicException) {
            // A damaged blob or a different Windows account requires pairing again.
            // Never fall back to a plaintext field when an encrypted field was supplied.
            ClearDevicePairing();
            CredentialStorageChanged = true;
        } finally {
            legacyDeviceToken = null;
        }
    }

    internal bool UpgradeDevicePairing() {
        if (ActiveSession is { SchemaVersion: 1 } session && NativeProduct.RequestOrigin(session.Origin)
            && session.UpgradeLegacy(DeviceId, DeviceToken) is { } upgraded) {
            ActiveSession = upgraded;
            return true;
        }
        return false;
    }

    internal void ClearDevicePairing() {
        PairingRequired = true;
        ActiveSession = null;
        DeviceToken = "";
        DeviceId = "";
        PairingCode = "";
    }
}

internal static partial class DeviceCredential {
    private const uint UiForbidden = 1;
    private const int MaximumCredentialBytes = 1024;
    private const int MaximumEncodedBytes = 8192;

    internal static string Protect(string value) {
        if (Encoding.UTF8.GetByteCount(value) > MaximumCredentialBytes)
            throw new CryptographicException("Invalid device credential length.");
        var bytes = Encoding.UTF8.GetBytes(value);
        try { return Transform(bytes, protect: true); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }

    internal static string Unprotect(string value) {
        if (value.Length is 0 or > MaximumEncodedBytes)
            throw new CryptographicException("Invalid encrypted device credential.");
        try { return Transform(Convert.FromBase64String(value), protect: false); }
        catch (FormatException) { throw new CryptographicException("Invalid encrypted device credential."); }
    }

    private static unsafe string Transform(byte[] bytes, bool protect) {
        if (!OperatingSystem.IsWindows()) throw new CryptographicException("Windows credential protection is unavailable.");
        fixed (byte* inputBytes = bytes) {
            var input = new DataBlob { Size = bytes.Length, Data = (nint)inputBytes };
            DataBlob output = default;
            try {
                // DPAPI defaults to CurrentUser. No machine-wide flag is used.
                var success = protect
                    ? CryptProtectData(in input, 0, 0, 0, 0, UiForbidden, out output)
                    : CryptUnprotectData(in input, 0, 0, 0, 0, UiForbidden, out output);
                if (success == 0)
                    throw new CryptographicException("Windows could not open or protect the device credential.",
                        new Win32Exception(Marshal.GetLastPInvokeError()));
                if (output.Size <= 0 || (!protect && output.Size > MaximumCredentialBytes))
                    throw new CryptographicException("Invalid device credential length.");
                var data = new Span<byte>((void*)output.Data, output.Size);
                return protect ? Convert.ToBase64String(data) : Encoding.UTF8.GetString(data);
            } finally {
                if (output.Data != 0) {
                    if (!protect && output.Size > 0) CryptographicOperations.ZeroMemory(new Span<byte>((void*)output.Data, output.Size));
                    LocalFree(output.Data);
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob {
        internal int Size;
        internal nint Data;
    }

    [LibraryImport("crypt32.dll", SetLastError = true)]
    private static partial int CryptProtectData(in DataBlob input, nint description, nint entropy, nint reserved,
        nint prompt, uint flags, out DataBlob output);

    [LibraryImport("crypt32.dll", SetLastError = true)]
    private static partial int CryptUnprotectData(in DataBlob input, nint description, nint entropy, nint reserved,
        nint prompt, uint flags, out DataBlob output);

    [LibraryImport("kernel32.dll")]
    private static partial nint LocalFree(nint memory);
}
