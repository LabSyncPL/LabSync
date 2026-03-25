using System.Diagnostics;
using LabSync.Modules.ScriptExecutor.Models;

namespace LabSync.Modules.ScriptExecutor.Interpreters;

internal sealed class CmdInterpreter : IScriptInterpreter
{
    public InterpreterType Type => InterpreterType.Cmd;

    public bool IsSupportedOnCurrentPlatform() => OperatingSystem.IsWindows();

    /// <summary>
    /// Switch Windows console to UTF-8 code page before batch body (complements UTF-8 stream decoding in Process).
    /// </summary>
    public string ApplyEncodingPreamble(string scriptContent)
    {
        if (!OperatingSystem.IsWindows())
            return scriptContent;

        return "chcp 65001>nul" + Environment.NewLine + scriptContent;
    }

    public ProcessStartInfo CreateStartInfo(string scriptPath, string[] arguments)
    {
        var escapedArgs = string.Join(" ", arguments.Select(EscapeCmdArgument));
        return new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/d /s /c \"\"{scriptPath}\" {escapedArgs}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
    }

    private static string EscapeCmdArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg))
        {
            return "\"\"";
        }

        return $"\"{arg.Replace("\"", "\\\"")}\"";
    }
}

