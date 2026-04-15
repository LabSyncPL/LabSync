namespace LabSync.Core.Dto;

public sealed class SavedScriptDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Content { get; set; } = "";
    public string Interpreter { get; set; } = "powershell";
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class CreateSavedScriptRequest
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Content { get; set; } = "";
    public string Interpreter { get; set; } = "powershell";
}

public sealed class UpdateSavedScriptRequest
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Content { get; set; } = "";
    public string Interpreter { get; set; } = "powershell";
}
