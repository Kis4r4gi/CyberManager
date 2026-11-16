namespace CyberManager.Shared.Dtos;

public record SharedNoteDto(long Id, string Text, string AuthorName, DateTime CreatedUtc);

public record SaveSharedNoteRequest(string Text);
