using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Activation;

namespace Yukari.Helpers;

public static class FileActivationHelper
{
    public static string? TryGetChapterFilePath(AppActivationArguments activationArgs)
    {
        if (
            activationArgs.Kind != ExtendedActivationKind.Launch
            || activationArgs.Data is not ILaunchActivatedEventArgs launchArgs
        )
            return null;

        return TryGetChapterFilePath(launchArgs.Arguments);
    }

    public static string? TryGetChapterFilePath(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
            return null;

        foreach (var arg in SplitCommandLine(commandLine))
            if (LocalComicHelper.IsSupportedChapterFile(arg) && File.Exists(arg))
                return arg;

        return null;
    }

    private static string[] SplitCommandLine(string commandLine)
    {
        var argv = CommandLineToArgvW(commandLine, out var argc);
        if (argv == IntPtr.Zero)
            return [];

        try
        {
            var args = new string[argc];
            for (var i = 0; i < argc; i++)
                args[i] = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size))!;

            return args;
        }
        finally
        {
            LocalFree(argv);
        }
    }

    [DllImport("shell32.dll", SetLastError = true)]
    private static extern IntPtr CommandLineToArgvW(
        [MarshalAs(UnmanagedType.LPWStr)] string lpCmdLine,
        out int pNumArgs
    );

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr hMem);
}
