using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Services.Persistence;

namespace HouseVictoria.Services.Persona
{
    public class PersonaBackupService : IPersonaBackupService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly DatabasePersistenceService _database;
        private readonly IMemoryService _memoryService;
        private readonly AppConfig _appConfig;
        private readonly string _appBaseDirectory;

        public PersonaBackupService(
            DatabasePersistenceService database,
            IMemoryService memoryService,
            AppConfig appConfig)
        {
            _database = database ?? throw new ArgumentNullException(nameof(database));
            _memoryService = memoryService ?? throw new ArgumentNullException(nameof(memoryService));
            _appConfig = appConfig ?? throw new ArgumentNullException(nameof(appConfig));
            _appBaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
        }

        public async Task<PersonaBackupResult> ExportAsync(AIContact persona, string outputZipPath)
        {
            if (persona == null) throw new ArgumentNullException(nameof(persona));
            if (string.IsNullOrWhiteSpace(outputZipPath)) throw new ArgumentException("Output path is required.", nameof(outputZipPath));

            try
            {
                var payload = await BuildPayloadAsync(persona).ConfigureAwait(false);
                var directory = Path.GetDirectoryName(outputZipPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(outputZipPath))
                {
                    File.Delete(outputZipPath);
                }

                using (var archive = ZipFile.Open(outputZipPath, ZipArchiveMode.Create))
                {
                    WriteJsonEntry(archive, "manifest.json", payload.Manifest);
                    WriteJsonEntry(archive, "persona.json", payload.Persona);
                    WriteJsonEntry(archive, "memories.json", payload.Memories);
                    WriteJsonEntry(archive, "databanks.json", payload.DataBanks);
                    WriteJsonEntry(archive, "conversations.json", payload.Conversations);

                    foreach (var pair in payload.MessagesByConversation)
                    {
                        var safeName = MakeArchiveSegment(pair.Key);
                        WriteJsonEntry(archive, $"messages/{safeName}.json", pair.Value);
                    }

                    foreach (var fileEntry in payload.Manifest.Files)
                    {
                        if (string.IsNullOrWhiteSpace(fileEntry.OriginalPath) || !File.Exists(fileEntry.OriginalPath))
                        {
                            continue;
                        }

                        archive.CreateEntryFromFile(fileEntry.OriginalPath, fileEntry.ArchivePath, CompressionLevel.Optimal);
                    }
                }

                return new PersonaBackupResult
                {
                    Success = true,
                    Message = $"Backed up {persona.Name}: {payload.Manifest.MemoryCount} memories, {payload.Manifest.MessageCount} messages, {payload.Manifest.FileCount} files.",
                    OutputPath = outputZipPath,
                    PersonaId = persona.Id,
                    PersonaName = persona.Name
                };
            }
            catch (Exception ex)
            {
                return new PersonaBackupResult
                {
                    Success = false,
                    Message = $"Backup failed: {ex.Message}",
                    PersonaId = persona.Id,
                    PersonaName = persona.Name
                };
            }
        }

        public async Task<PersonaBackupPayload?> PreviewAsync(string zipPath)
        {
            if (!File.Exists(zipPath)) return null;

            using var archive = ZipFile.OpenRead(zipPath);
            var manifest = ReadJsonEntry<PersonaBackupManifest>(archive, "manifest.json");
            var persona = ReadJsonEntry<AIContact>(archive, "persona.json");
            if (manifest == null || persona == null) return null;

            return new PersonaBackupPayload
            {
                Manifest = manifest,
                Persona = persona,
                Memories = ReadJsonEntry<List<PersonaMemoryRecord>>(archive, "memories.json") ?? new(),
                DataBanks = ReadJsonEntry<List<DataBank>>(archive, "databanks.json") ?? new(),
                Conversations = ReadJsonEntry<List<PersonaBackupConversation>>(archive, "conversations.json") ?? new()
            };
        }

        public async Task<PersonaBackupResult> ImportAsync(string zipPath, PersonaImportMode mode)
        {
            if (!File.Exists(zipPath))
            {
                return new PersonaBackupResult { Success = false, Message = "Backup file not found." };
            }

            try
            {
                using var archive = ZipFile.OpenRead(zipPath);
                var manifest = ReadJsonEntry<PersonaBackupManifest>(archive, "manifest.json");
                var persona = ReadJsonEntry<AIContact>(archive, "persona.json");
                var memories = ReadJsonEntry<List<PersonaMemoryRecord>>(archive, "memories.json") ?? new();
                var dataBanks = ReadJsonEntry<List<DataBank>>(archive, "databanks.json") ?? new();
                var conversations = ReadJsonEntry<List<PersonaBackupConversation>>(archive, "conversations.json") ?? new();

                if (manifest == null || persona == null)
                {
                    return new PersonaBackupResult { Success = false, Message = "Invalid backup: missing manifest or persona." };
                }

                if (manifest.Version > PersonaBackupManifest.CurrentVersion)
                {
                    return new PersonaBackupResult
                    {
                        Success = false,
                        Message = $"Backup version {manifest.Version} is newer than this app supports (v{PersonaBackupManifest.CurrentVersion})."
                    };
                }

                var sourcePersonaId = persona.Id;
                var idMap = BuildIdMap(sourcePersonaId, mode);

                persona.Id = idMap[sourcePersonaId];
                persona.DataPath = Path.Combine(_appConfig.DataBankPath, persona.Id);
                persona.IsLoaded = false;
                persona.LastUsedAt = DateTime.Now;

                if (mode == PersonaImportMode.NewCopy)
                {
                    persona.Name = await EnsureUniquePersonaNameAsync(persona.Name).ConfigureAwait(false);
                    persona.IsPrimaryAI = false;
                    persona.Role = PersonaRole.Companion;
                }

                var existing = await _database.GetAsync<AIContact>($"AIContact_{persona.Id}").ConfigureAwait(false);
                if (existing != null && mode == PersonaImportMode.PreserveId)
                {
                    await _database.DeleteMemoriesForPersonaAsync(persona.Id).ConfigureAwait(false);
                }

                Directory.CreateDirectory(persona.DataPath);

                foreach (var fileEntry in manifest.Files)
                {
                    var entry = archive.GetEntry(fileEntry.ArchivePath);
                    if (entry == null) continue;

                    var restoredPath = ResolveRestorePath(fileEntry, persona, idMap);
                    if (string.IsNullOrWhiteSpace(restoredPath)) continue;

                    var parent = Path.GetDirectoryName(restoredPath);
                    if (!string.IsNullOrWhiteSpace(parent))
                    {
                        Directory.CreateDirectory(parent);
                    }

                    entry.ExtractToFile(restoredPath, overwrite: true);
                }

                RemapMemoryRecords(memories, idMap);
                foreach (var record in memories)
                {
                    await _database.RestoreMemoryRecordAsync(record).ConfigureAwait(false);
                }

                foreach (var bank in dataBanks)
                {
                    RemapDataBankAttachments(bank, persona.Id);
                    await _memoryService.AddDataBankAsync(bank).ConfigureAwait(false);
                }

                foreach (var conversation in conversations)
                {
                    var newConversationId = RemapConversationId(conversation.Id, idMap);
                    var messageEntry = archive.GetEntry($"messages/{MakeArchiveSegment(conversation.Id)}.json");
                    if (messageEntry == null) continue;

                    using var stream = messageEntry.Open();
                    var messages = await JsonSerializer.DeserializeAsync<List<ConversationMessage>>(stream, JsonOptions).ConfigureAwait(false)
                        ?? new List<ConversationMessage>();

                    foreach (var message in messages)
                    {
                        message.ConversationId = newConversationId;
                        message.FilePath = RemapFilePath(message.FilePath, idMap, persona);
                        await _database.SaveMessageAsync(message).ConfigureAwait(false);
                    }
                }

                if (!string.IsNullOrWhiteSpace(persona.AvatarUrl) && persona.AvatarUrl.StartsWith("files/", StringComparison.OrdinalIgnoreCase))
                {
                    persona.AvatarUrl = Path.Combine(persona.DataPath, Path.GetFileName(persona.AvatarUrl));
                }
                else
                {
                    persona.AvatarUrl = RemapFilePath(persona.AvatarUrl, idMap, persona);
                }

                persona.AvatarModelPath = RemapFilePath(persona.AvatarModelPath, idMap, persona);

                await _database.SetAsync($"AIContact_{persona.Id}", persona).ConfigureAwait(false);

                var configPath = Path.Combine(persona.DataPath, "config.json");
                var config = new
                {
                    persona.Id,
                    persona.Name,
                    persona.ModelName,
                    persona.MCPServerEndpoint,
                    RestoredAt = DateTime.Now,
                    SourceBackup = manifest.ExportedAt
                };
                await File.WriteAllTextAsync(configPath, JsonSerializer.Serialize(config, JsonOptions)).ConfigureAwait(false);

                return new PersonaBackupResult
                {
                    Success = true,
                    Message = $"Restored {persona.Name}: {memories.Count} memories, {dataBanks.Count} databanks.",
                    PersonaId = persona.Id,
                    PersonaName = persona.Name
                };
            }
            catch (Exception ex)
            {
                return new PersonaBackupResult { Success = false, Message = $"Restore failed: {ex.Message}" };
            }
        }

        private async Task<PersonaBackupPayload> BuildPayloadAsync(AIContact persona)
        {
            var memories = await _database.GetMemoryRecordsForPersonaAsync(persona.Id).ConfigureAwait(false);
            var dataBanks = await FindPersonaDataBanksAsync(persona).ConfigureAwait(false);
            var conversationIds = await _database.GetConversationIdsForContactAsync(persona.Id).ConfigureAwait(false);

            var conversations = new List<PersonaBackupConversation>();
            var messagesByConversation = new Dictionary<string, List<ConversationMessage>>();
            var totalMessages = 0;

            foreach (var conversationId in conversationIds)
            {
                var messages = await _database.GetAllMessagesForConversationAsync(conversationId).ConfigureAwait(false);
                if (messages.Count == 0 && conversationId != $"conv-{persona.Id}")
                {
                    continue;
                }

                conversations.Add(new PersonaBackupConversation
                {
                    Id = conversationId,
                    ContactId = persona.Id,
                    LastMessageAt = messages.LastOrDefault()?.Timestamp ?? DateTime.UtcNow
                });
                messagesByConversation[conversationId] = messages;
                totalMessages += messages.Count;
            }

            var manifest = new PersonaBackupManifest
            {
                ExportedAt = DateTime.UtcNow,
                PersonaId = persona.Id,
                PersonaName = persona.Name,
                SourceMachine = Environment.MachineName,
                MemoryCount = memories.Count,
                DataBankCount = dataBanks.Count,
                ConversationCount = conversations.Count,
                MessageCount = totalMessages
            };

            CollectPersonaFolderFiles(persona, manifest);
            CollectDataBankFiles(dataBanks, manifest);
            CollectMessageMediaFiles(messagesByConversation, manifest);
            CollectLocalFile(persona.AvatarUrl, manifest, "avatar", $"files/avatar{Path.GetExtension(persona.AvatarUrl ?? ".png")}");
            CollectLocalFile(persona.AvatarModelPath, manifest, "avatar-model", null);

            manifest.FileCount = manifest.Files.Count;

            return new PersonaBackupPayload
            {
                Manifest = manifest,
                Persona = persona,
                Memories = memories,
                DataBanks = dataBanks,
                Conversations = conversations,
                MessagesByConversation = messagesByConversation
            };
        }

        private async Task<List<DataBank>> FindPersonaDataBanksAsync(AIContact persona)
        {
            var allBanks = await _memoryService.GetAllDataBanksAsync().ConfigureAwait(false);
            var personalPrefix = $"{persona.Name} -";
            return allBanks
                .Where(b => b.Name.StartsWith(personalPrefix, StringComparison.OrdinalIgnoreCase)
                            || b.Name.Contains(persona.Name, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static void CollectPersonaFolderFiles(AIContact persona, PersonaBackupManifest manifest)
        {
            if (string.IsNullOrWhiteSpace(persona.DataPath) || !Directory.Exists(persona.DataPath))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(persona.DataPath, "*", SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(persona.DataPath, file).Replace('\\', '/');
                manifest.Files.Add(new PersonaBackupFileEntry
                {
                    ArchivePath = $"files/persona/{relative}",
                    OriginalPath = file,
                    Category = "persona-folder"
                });
            }
        }

        private void CollectDataBankFiles(IEnumerable<DataBank> dataBanks, PersonaBackupManifest manifest)
        {
            foreach (var bank in dataBanks)
            {
                foreach (var entry in bank.DataEntries)
                {
                    if (string.IsNullOrWhiteSpace(entry.AttachmentPath) || !File.Exists(entry.AttachmentPath))
                    {
                        continue;
                    }

                    var fileName = $"{bank.Id}_{entry.Id}_{Path.GetFileName(entry.AttachmentPath)}";
                    manifest.Files.Add(new PersonaBackupFileEntry
                    {
                        ArchivePath = $"files/databank/{fileName}",
                        OriginalPath = entry.AttachmentPath,
                        Category = "databank-attachment"
                    });
                }
            }
        }

        private void CollectMessageMediaFiles(
            Dictionary<string, List<ConversationMessage>> messagesByConversation,
            PersonaBackupManifest manifest)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var pair in messagesByConversation)
            {
                var conversationMediaDir = Path.Combine(_appBaseDirectory, "Data", "Media", pair.Key);
                if (Directory.Exists(conversationMediaDir))
                {
                    foreach (var file in Directory.EnumerateFiles(conversationMediaDir, "*", SearchOption.AllDirectories))
                    {
                        if (!seen.Add(file)) continue;
                        var relative = Path.GetRelativePath(_appBaseDirectory, file).Replace('\\', '/');
                        manifest.Files.Add(new PersonaBackupFileEntry
                        {
                            ArchivePath = $"files/media/{relative}",
                            OriginalPath = file,
                            Category = "media"
                        });
                    }
                }

                foreach (var message in pair.Value)
                {
                    if (string.IsNullOrWhiteSpace(message.FilePath) || !File.Exists(message.FilePath) || !seen.Add(message.FilePath))
                    {
                        continue;
                    }

                    var relative = Path.GetRelativePath(_appBaseDirectory, message.FilePath).Replace('\\', '/');
                    manifest.Files.Add(new PersonaBackupFileEntry
                    {
                        ArchivePath = $"files/media/{relative}",
                        OriginalPath = message.FilePath,
                        Category = "media"
                    });
                }
            }
        }

        private static void CollectLocalFile(string? path, PersonaBackupManifest manifest, string category, string? archivePath)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return;
            }

            manifest.Files.Add(new PersonaBackupFileEntry
            {
                ArchivePath = archivePath ?? $"files/{category}/{Path.GetFileName(path)}",
                OriginalPath = path,
                Category = category
            });
        }

        private async Task<string> EnsureUniquePersonaNameAsync(string baseName)
        {
            var contacts = await _database.GetAllAsync<AIContact>().ConfigureAwait(false);
            var names = contacts
                .Where(kvp => kvp.Key.StartsWith("AIContact_", StringComparison.Ordinal))
                .Select(kvp => kvp.Value.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!names.Contains(baseName))
            {
                return baseName;
            }

            for (var i = 2; i < 100; i++)
            {
                var candidate = $"{baseName} ({i})";
                if (!names.Contains(candidate))
                {
                    return candidate;
                }
            }

            return $"{baseName} (restored)";
        }

        private Dictionary<string, string> BuildIdMap(string sourcePersonaId, PersonaImportMode mode)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [sourcePersonaId] = mode == PersonaImportMode.NewCopy ? Guid.NewGuid().ToString() : sourcePersonaId
            };
            return map;
        }

        private static void RemapMemoryRecords(List<PersonaMemoryRecord> memories, Dictionary<string, string> idMap)
        {
            var sourceId = idMap.Keys.First();
            var targetId = idMap[sourceId];

            foreach (var record in memories)
            {
                if (string.Equals(record.ContactId, sourceId, StringComparison.Ordinal))
                {
                    record.ContactId = targetId;
                }

                if (string.Equals(record.PersonaId, sourceId, StringComparison.Ordinal))
                {
                    record.PersonaId = targetId;
                }
            }
        }

        private void RemapDataBankAttachments(DataBank bank, string personaId)
        {
            var bankFolder = Path.Combine(_appConfig.DataBankPath, bank.Id);
            Directory.CreateDirectory(bankFolder);

            foreach (var entry in bank.DataEntries)
            {
                if (string.IsNullOrWhiteSpace(entry.AttachmentPath))
                {
                    continue;
                }

                var originalName = Path.GetFileName(entry.AttachmentPath);
                var archiveName = $"{bank.Id}_{entry.Id}_{originalName}";
                var restored = Path.Combine(bankFolder, archiveName);
                entry.AttachmentPath = File.Exists(restored) ? restored : Path.Combine(bankFolder, originalName);
            }
        }

        private static string RemapConversationId(string conversationId, Dictionary<string, string> idMap)
        {
            var sourceId = idMap.Keys.First();
            var targetId = idMap[sourceId];
            return conversationId.Replace(sourceId, targetId, StringComparison.Ordinal);
        }

        private string? RemapFilePath(string? filePath, Dictionary<string, string> idMap, AIContact persona)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                return filePath;
            }

            var sourceId = idMap.Keys.First();
            var targetId = idMap[sourceId];
            var remapped = filePath.Replace(sourceId, targetId, StringComparison.Ordinal);

            if (File.Exists(remapped))
            {
                return remapped;
            }

            if (Path.IsPathRooted(filePath) && File.Exists(filePath))
            {
                return filePath;
            }

            var fileName = Path.GetFileName(filePath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return remapped;
            }

            var mediaCandidate = Path.Combine(_appBaseDirectory, "Data", "Media", $"conv-{targetId}", fileName);
            return File.Exists(mediaCandidate) ? mediaCandidate : remapped;
        }

        private string? ResolveRestorePath(PersonaBackupFileEntry fileEntry, AIContact persona, Dictionary<string, string> idMap)
        {
            var sourceId = idMap.Keys.First();
            var targetId = idMap[sourceId];

            return fileEntry.Category switch
            {
                "persona-folder" => Path.Combine(
                    persona.DataPath!,
                    fileEntry.ArchivePath.Replace("files/persona/", "", StringComparison.OrdinalIgnoreCase).Replace('/', Path.DirectorySeparatorChar)),
                "databank-attachment" => ResolveDatabankAttachmentPath(fileEntry.ArchivePath),
                "avatar" => Path.Combine(persona.DataPath!, Path.GetFileName(fileEntry.ArchivePath)),
                "avatar-model" => Path.Combine(persona.DataPath!, Path.GetFileName(fileEntry.ArchivePath)),
                "media" => ResolveMediaRestorePath(fileEntry.ArchivePath, targetId),
                _ => null
            };
        }

        private string? ResolveDatabankAttachmentPath(string archivePath)
        {
            var fileName = Path.GetFileName(archivePath);
            var parts = fileName.Split('_', 2);
            if (parts.Length < 2)
            {
                return null;
            }

            var bankId = parts[0];
            var bankFolder = Path.Combine(_appConfig.DataBankPath, bankId);
            Directory.CreateDirectory(bankFolder);
            return Path.Combine(bankFolder, fileName);
        }

        private string ResolveMediaRestorePath(string archivePath, string targetPersonaId)
        {
            var relative = archivePath
                .Replace("files/media/", "", StringComparison.OrdinalIgnoreCase)
                .Replace('/', Path.DirectorySeparatorChar);

            if (relative.Contains("Data", StringComparison.OrdinalIgnoreCase))
            {
                relative = relative.Replace(targetPersonaId, targetPersonaId, StringComparison.Ordinal);
                return Path.Combine(_appBaseDirectory, relative);
            }

            return Path.Combine(_appBaseDirectory, "Data", "Media", $"conv-{targetPersonaId}", Path.GetFileName(relative));
        }

        private static void WriteJsonEntry<T>(ZipArchive archive, string entryName, T value)
        {
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
            using var stream = entry.Open();
            JsonSerializer.Serialize(stream, value, JsonOptions);
        }

        private static T? ReadJsonEntry<T>(ZipArchive archive, string entryName)
        {
            var entry = archive.GetEntry(entryName);
            if (entry == null) return default;

            using var stream = entry.Open();
            return JsonSerializer.Deserialize<T>(stream, JsonOptions);
        }

        private static string MakeArchiveSegment(string value)
        {
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value;
        }
    }
}
