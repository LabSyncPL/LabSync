using System.Diagnostics;
using LabSync.Modules.ScriptExecutor.Models;

namespace LabSync.Modules.ScriptExecutor.Interpreters;

internal sealed class BashInterpreter : IScriptInterpreter
{
    public InterpreterType Type => InterpreterType.Bash;

    public bool IsSupportedOnCurrentPlatform() => OperatingSystem.IsLinux() || OperatingSystem.IsMacOS();

    /// <summary>
    /// Linux/macOS defaults are UTF-8; do not inject shell commands that could alter behavior.
    /// </summary>
    public string ApplyEncodingPreamble(string scriptContent) => scriptContent;

    public ProcessStartInfo CreateStartInfo(string scriptPath, string[] arguments)
    {
        var escapedArgs = string.Join(" ", arguments.Select(EscapeArgument));
        return new ProcessStartInfo
        {
            FileName = "/bin/bash",
            Arguments = $"\"{scriptPath}\" {escapedArgs}".Trim(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
    }

    private static string EscapeArgument(string arg)
    {
        if (string.IsNullOrEmpty(arg))
        {
            return "''";
        }

        return "'" + arg.Replace("'", "'\"'\"'") + "'";
    }
}

