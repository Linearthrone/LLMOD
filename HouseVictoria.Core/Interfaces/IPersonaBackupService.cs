using HouseVictoria.Core.Models;

namespace HouseVictoria.Core.Interfaces
{
    /// <summary>
    /// Export and restore a persona's memories, databanks, conversations, and media.
    /// </summary>
    public interface IPersonaBackupService
    {
        Task<PersonaBackupResult> ExportAsync(AIContact persona, string outputZipPath);
        Task<PersonaBackupResult> ImportAsync(string zipPath, PersonaImportMode mode);
        Task<PersonaBackupPayload?> PreviewAsync(string zipPath);
    }
}
