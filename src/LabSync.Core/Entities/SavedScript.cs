namespace LabSync.Core.Entities;

public class SavedScript
{
    public Guid Id { get; init; }
    public string Title { get; private set; }
    public string? Description { get; private set; }
    public string Content { get; private set; }
    public string Interpreter { get; private set; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; private set; }

    protected SavedScript() { }

    public SavedScript(string title, string? description, string content, string interpreter)
    {
        Id = Guid.NewGuid();
        Title = title;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Content = content;
        Interpreter = interpreter;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void Update(string title, string? description, string content, string interpreter)
    {
        Title = title;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Content = content;
        Interpreter = interpreter;
        UpdatedAt = DateTime.UtcNow;
    }
}
