using System.Diagnostics;
using LabSync.Modules.ScriptExecutor.Models;

namespace LabSync.Modules.ScriptExecutor.Interpreters;

internal interface IScriptInterpreter
{
    InterpreterType Type { get; }
    bool IsSupportedOnCurrentPlatform();
    ProcessStartInfo CreateStartInfo(string scriptPath, string[] arguments);

    /// <summary>
    /// Prepends OS/shell-specific commands so child output matches LabSync UTF-8 (Windows OEM/ANSI → UTF-8).
    /// Bash on Linux/macOS: return unchanged (already UTF-8).
    /// </summary>
    string ApplyEncodingPreamble(string scriptContent);
}

