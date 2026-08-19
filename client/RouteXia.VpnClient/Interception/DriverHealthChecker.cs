using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;

namespace RouteXia.VpnClient.Interception;

public enum DriverHealthStatus
{
    Healthy,
    RequiresElevation,
    MissingDriverFiles,
    ServiceBlockedOrDisabled,
    InvalidDriverSignature,
    UnknownError
}

public static class DriverHealthChecker
{
    public static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    public static DriverHealthStatus CheckDriverHealth(out string diagnosticDetail)
    {
        if (!IsRunningAsAdmin())
        {
            diagnosticDetail = "Administrator privileges are required to run kernel-level packet filter drivers.";
            return DriverHealthStatus.RequiresElevation;
        }

        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string dllPath = Path.Combine(baseDir, "WinDivert.dll");
        string sysPath = Path.Combine(baseDir, "WinDivert64.sys");
        string nativeDllPath = Path.Combine(baseDir, "Native", "WinDivert.dll");
        string nativeSysPath = Path.Combine(baseDir, "Native", "WinDivert64.sys");

        bool hasDll = File.Exists(dllPath) || File.Exists(nativeDllPath);
        bool hasSys = File.Exists(sysPath) || File.Exists(nativeSysPath);

        if (!hasDll || !hasSys)
        {
            diagnosticDetail = $"Missing WinDivert driver files. WinDivert.dll: {(hasDll ? "Found" : "Missing")}, WinDivert64.sys: {(hasSys ? "Found" : "Missing")}";
            return DriverHealthStatus.MissingDriverFiles;
        }

        diagnosticDetail = "All driver files present and administrative privileges verified.";
        return DriverHealthStatus.Healthy;
    }

    public static string TranslateWin32Error(int win32Error)
    {
        return win32Error switch
        {
            2 => "WinDivert driver file (WinDivert64.sys) was not found in the application directory.",
            5 => "Access Denied. Administrator rights are required, or Windows Filtering Platform is blocked.",
            577 => "Windows Driver Signature Enforcement blocked the driver. Ensure Windows is updated and Secure Boot is standard.",
            1058 => "The Base Filtering Engine (BFE) or Windows Filtering Platform service is disabled.",
            1060 => "The WinDivert driver service could not be registered. An antivirus or anti-cheat may be blocking it.",
            _ => $"WinDivert error code: {win32Error} (0x{win32Error:X8})."
        };
    }

    public static bool RelaunchElevated(string? arguments = null)
    {
        try
        {
            string exePath = Process.GetCurrentProcess().MainModule?.FileName ?? string.Empty;
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                return false;
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = arguments ?? string.Empty,
                Verb = "runas",
                UseShellExecute = true
            };

            Process.Start(startInfo);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
