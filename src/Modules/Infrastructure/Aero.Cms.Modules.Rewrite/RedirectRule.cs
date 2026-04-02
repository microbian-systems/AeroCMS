namespace Aero.Cms.Modules.Rewrite;

public record RedirectRule
{
    public long Id { get; init; }
    public required string FromPath { get; init; }
    public required string ToPath { get; init; }
    public int StatusCode { get; init; } = 301;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
