// Codex developer note: Defines tenant-scoped background knowledge refresh settings.
namespace webapi_oyako.Domain.Models;

public sealed record KnowledgeRefreshSettings(
    int RefreshPeriodValue,
    string RefreshPeriodUnit,
    int RefreshPeriodMinutes,
    DateTime UpdatedAtUtc);
