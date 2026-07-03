using System.Text;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;

namespace HouseVictoria.Services.ProjectManagement
{
    /// <summary>
    /// Persists autonomy / project work as on-disk deliverables and registers them as artifacts.
    /// </summary>
    public static class ProjectDeliverableMaterializer
    {
        private const int MinSubstantiveLength = 120;

        public static async Task<string?> SaveSessionDeliverableAsync(
            string autonomyRoot,
            IProjectManagementService projects,
            Project project,
            string markdownBody,
            string? stepLabel,
            string createdByContactId,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(markdownBody) || markdownBody.Trim().Length < MinSubstantiveLength)
                return null;

            var deliverablesDir = Path.Combine(autonomyRoot, "Deliverables", SanitizeDirSegment(project.Id));
            Directory.CreateDirectory(deliverablesDir);

            var slug = SanitizeFileSegment(stepLabel ?? project.Name);
            var fileName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{slug}.md";
            var fullPath = Path.GetFullPath(Path.Combine(deliverablesDir, fileName));

            var header = new StringBuilder();
            header.AppendLine($"# {project.Name}");
            if (!string.IsNullOrWhiteSpace(stepLabel))
                header.AppendLine($"**Step:** {stepLabel.Trim()}");
            header.AppendLine($"**Saved:** {DateTime.Now:yyyy-MM-dd HH:mm}");
            header.AppendLine();
            header.AppendLine(markdownBody.Trim());

            await File.WriteAllTextAsync(fullPath, header.ToString(), cancellationToken).ConfigureAwait(false);

            var artifact = new ProjectArtifact
            {
                ProjectId = project.Id,
                Name = string.IsNullOrWhiteSpace(stepLabel)
                    ? Path.GetFileNameWithoutExtension(fileName)
                    : stepLabel.Trim(),
                FilePath = fullPath,
                Type = ArtifactType.Document,
                Description = Truncate(markdownBody.Trim(), 240),
                FileSize = new FileInfo(fullPath).Length,
                CreatedBy = createdByContactId
            };

            await projects.AddArtifactAsync(project.Id, artifact).ConfigureAwait(false);
            return fullPath;
        }

        /// <summary>
        /// When a project has logs but no file artifacts, write a completion bundle for AAR / review.
        /// </summary>
        public static async Task<IReadOnlyList<ProjectArtifact>> EnsureCompletionBundleAsync(
            string autonomyRoot,
            IProjectManagementService projects,
            Project project,
            IReadOnlyList<ProjectLog> logs,
            string createdByContactId,
            CancellationToken cancellationToken = default)
        {
            var existing = await projects.GetArtifactsAsync(project.Id).ConfigureAwait(false);
            var withFiles = existing
                .Where(a => !string.IsNullOrWhiteSpace(a.FilePath) && File.Exists(a.FilePath))
                .ToList();
            if (withFiles.Count > 0)
                return withFiles;

            var substantive = logs
                .Where(l => !string.IsNullOrWhiteSpace(l.Details) && l.Details!.Trim().Length >= MinSubstantiveLength)
                .OrderByDescending(l => l.Timestamp)
                .Take(8)
                .ToList();

            if (substantive.Count == 0)
                return withFiles;

            var deliverablesDir = Path.Combine(autonomyRoot, "Deliverables", SanitizeDirSegment(project.Id));
            Directory.CreateDirectory(deliverablesDir);

            var fileName = $"completion-bundle-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md";
            var fullPath = Path.GetFullPath(Path.Combine(deliverablesDir, fileName));

            var sb = new StringBuilder();
            sb.AppendLine($"# {project.Name} — completion deliverables");
            sb.AppendLine();
            sb.AppendLine(project.Description);
            sb.AppendLine();
            sb.AppendLine($"Generated {DateTime.Now:yyyy-MM-dd HH:mm} from {substantive.Count} work session(s).");
            sb.AppendLine();

            foreach (var log in substantive.OrderBy(l => l.Timestamp))
            {
                sb.AppendLine("---");
                sb.AppendLine($"## {log.Action} ({log.Timestamp:yyyy-MM-dd HH:mm})");
                sb.AppendLine();
                sb.AppendLine(log.Details!.Trim());
                sb.AppendLine();
            }

            await File.WriteAllTextAsync(fullPath, sb.ToString(), cancellationToken).ConfigureAwait(false);

            var artifact = new ProjectArtifact
            {
                ProjectId = project.Id,
                Name = $"{project.Name} — completion bundle",
                FilePath = fullPath,
                Type = ArtifactType.Document,
                Description = "Compiled deliverables from project work logs",
                FileSize = new FileInfo(fullPath).Length,
                CreatedBy = createdByContactId
            };

            await projects.AddArtifactAsync(project.Id, artifact).ConfigureAwait(false);
            withFiles.Add(artifact);
            return withFiles;
        }

        public static string? PickWorkExcerpt(IReadOnlyList<ProjectLog> logs, int maxChars = 1200)
        {
            var latest = logs
                .Where(l => !string.IsNullOrWhiteSpace(l.Details) && l.Details!.Trim().Length >= MinSubstantiveLength)
                .OrderByDescending(l => l.Timestamp)
                .Select(l => l.Details!.Trim())
                .FirstOrDefault();

            return string.IsNullOrWhiteSpace(latest) ? null : Truncate(latest, maxChars);
        }

        private static string SanitizeDirSegment(string value)
        {
            var trimmed = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
            foreach (var c in Path.GetInvalidFileNameChars())
                trimmed = trimmed.Replace(c, '_');
            return trimmed.Length > 64 ? trimmed[..64] : trimmed;
        }

        private static string SanitizeFileSegment(string value)
        {
            var trimmed = string.IsNullOrWhiteSpace(value) ? "deliverable" : value.Trim();
            foreach (var c in Path.GetInvalidFileNameChars())
                trimmed = trimmed.Replace(c, '_');
            trimmed = trimmed.Replace(' ', '-');
            return trimmed.Length > 48 ? trimmed[..48] : trimmed;
        }

        private static string Truncate(string s, int max) =>
            string.IsNullOrEmpty(s) || s.Length <= max ? s : s[..max] + "…";
    }
}
