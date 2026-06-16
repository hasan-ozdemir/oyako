// Codex developer note: Explains the purpose and flow of webapi-oyako/Infrastructure/Configuration/SqliteOptions.cs for maintainers.
namespace webapi_oyako.Infrastructure.Configuration;

// Implements the SqliteOptions component and its responsibilities in the Oyako codebase.
public sealed class SqliteOptions
{
    public const string SectionName = "Sqlite";
    // Exposes data consumed by other layers while preserving the domain or DTO shape.
    public string ConnectionString { get; set; } = "Data Source=./Data/oyako.sqlite;Cache=Shared";
}
