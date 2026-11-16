namespace CyberManager.Shared.Domain;

/// <summary>Nota breve que comparten todos los usuarios de la consola.</summary>
public class SharedNote
{
    public long Id { get; set; }
    public string Text { get; set; } = string.Empty;
    /// <summary>Usuario que publicó la nota; nulo si la cuenta se eliminó.</summary>
    public int? UserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
