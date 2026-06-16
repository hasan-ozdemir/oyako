// Codex developer note: Explains the purpose and flow of webapi-oyako/Domain/Repositories/IKnowledgeStoreMaintenanceRepository.cs for maintainers.
namespace webapi_oyako.Domain.Repositories;

// Declares the IKnowledgeStoreMaintenanceRepository contract used to decouple Oyako layers.
public interface IKnowledgeStoreMaintenanceRepository
{
    Task BackupAsync(string backupSetId, CancellationToken cancellationToken);
    Task ClearKnowledgeTablesAsync(CancellationToken cancellationToken);
    Task RestoreAsync(string backupSetId, CancellationToken cancellationToken);
    Task CleanupBackupsExceptAsync(string backupSetIdToKeep, CancellationToken cancellationToken);
}
