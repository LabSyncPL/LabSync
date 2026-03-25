using System.Diagnostics;
using LabSync.Modules.ScriptExecutor.Models;

namespace LabSync.Modules.ScriptExecutor.Interpreters;

internal sealed class PowerShellInterpreter : IScriptInterpreter
{
    public InterpreterType Type => InterpreterType.PowerShell;

    public bool IsSupportedOnCurrentPlatform() => OperatingSystem.IsWindows();

    /// <summary>
    /// Forces PowerShell session and .NET string pipeline to UTF-8 on Windows (fixes CP852/1250 garbling).
    /// </summary>
    public string ApplyEncodingPreamble(string scriptContent)
    {
        if (!OperatingSystem.IsWindows())
            return scriptContent;

        const string preamble =
            """
            $OutputEncoding = [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
            [Console]::InputEncoding = [System.Text.Encoding]::UTF8
            chcp 65001 | Out-Null
            """;

        return preamble.Trim() + Environment.NewLine + scriptContent;
    }

    public ProcessStartInfo CreateStartInfo(string scriptPath, string[] arguments)
    {
        var escapedArguments = string.Join(" ", arguments.Select(EscapeArgument));
        var argSuffix = string.IsNullOrWhiteSpace(escapedArguments) ? "" : " " + escapedArguments;

        return new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"{scriptPath}\"{argSuffix}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
    }

    /// <summary>
    /// Escapes arguments for the Windows process command line (passed after -File).
    /// </summary>
    private static string EscapeArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg))
        {
            return "\"\"";
        }

        return "\"" + arg.Replace("\"", "\\\"") + "\"";
    }
}

