using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using HouseVictoria.Core.Interfaces;
using HouseVictoria.Core.Models;
using HouseVictoria.Services.Memory;

namespace HouseVictoria.Services.Persistence
{
    /// <summary>
    /// SQLite-based persistence service
    /// </summary>
    public class DatabasePersistenceService : IPersistenceService, IMemoryService
    {
        private readonly string _connectionString;
        private readonly string _databasePath;
        private readonly string _dataBankFilesPath;
        private readonly AppConfig? _appConfig;
        private readonly PgVectorClient? _pgVector;

        public DatabasePersistenceService(AppConfig? appConfig = null, string basePath = "Data")
        {
            _appConfig = appConfig;
            var resolvedBasePath = appConfig?.PersistentMemoryPath ?? basePath;
            _databasePath = Path.Combine(resolvedBasePath, "HouseVictoria.db");
            Directory.CreateDirectory(resolvedBasePath);
            _dataBankFilesPath = !string.IsNullOrWhiteSpace(appConfig?.DataBankPath)
                ? appConfig.DataBankPath
                : Path.Combine(resolvedBasePath, "Databanks");
            Directory.CreateDirectory(_dataBankFilesPath);
            
            _connectionString = $"Data Source={_databasePath};Version=3;";
            _pgVector = (appConfig?.EnablePgVector == true && !string.IsNullOrWhiteSpace(appConfig.PgVectorConnectionString))
                ? new PgVectorClient(appConfig.PgVectorConnectionString)
                : null;
            InitializeDatabase();
            EnsureMemoryV2Columns();
            EnsureMemoryFts();
        }

        private static List<DataBankEntry> DeserializeDataEntries(string? dataEntriesJson)
        {
            if (string.IsNullOrWhiteSpace(dataEntriesJson))
            {
                return new List<DataBankEntry>();
            }

            try
            {
                var typedEntries = Newtonsoft.Json.JsonConvert.DeserializeObject<List<DataBankEntry>>(dataEntriesJson);
                if (typedEntries != null)
                {
                    return typedEntries;
                }
            }
            catch
            {
                // If deserialization fails, fall back to legacy string entries
            }

            try
            {
                var legacyEntries = Newtonsoft.Json.JsonConvert.DeserializeObject<List<string>>(dataEntriesJson);
                if (legacyEntries != null)
                {
                    return legacyEntries
                        .Where(entry => !string.IsNullOrWhiteSpace(entry))
                        .Select(CreateEntryFromString)
                        .ToList();
                }
            }
            catch
            {
                // Final fallback is to return an empty list
            }

            return new List<DataBankEntry>();
        }

        private static DataBankEntry CreateEntryFromString(string content)
        {
            var title = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                               .FirstOrDefault()?.Trim() ?? "Entry";

            if (title.Length > 80)
            {
                title = $"{title[..80]}...";
            }

            return new DataBankEntry
            {
                Title = title,
                Content = content,
                CreatedAt = DateTime.Now,
                LastModified = DateTime.Now
            };
        }

        private static string MakeSafeFileName(string name)
        {
            foreach (var invalidChar in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalidChar, '_');
            }
            return name;
        }

        private string GetDataBankFolder(string bankId)
        {
            var safeId = string.IsNullOrWhiteSpace(bankId) ? "default" : MakeSafeFileName(bankId);
            var folder = Path.Combine(_dataBankFilesPath, safeId);
            Directory.CreateDirectory(folder);
            return folder;
        }

        private void EnsureAttachmentCopied(string bankId, DataBankEntry entry)
        {
            if (string.IsNullOrWhiteSpace(entry.AttachmentTempPath) || !File.Exists(entry.AttachmentTempPath))
            {
                return;
            }

            var bankFolder = GetDataBankFolder(bankId);
            var extension = Path.GetExtension(entry.AttachmentTempPath);
            var baseName = !string.IsNullOrWhiteSpace(entry.AttachmentFileName)
                ? MakeSafeFileName(entry.AttachmentFileName)
                : MakeSafeFileName(Path.GetFileName(entry.AttachmentTempPath));

            var targetFileName = $"{Path.GetFileNameWithoutExtension(baseName)}_{entry.Id}{extension}";
            var targetPath = Path.Combine(bankFolder, targetFileName);

            File.Copy(entry.AttachmentTempPath, targetPath, true);

            var info = new FileInfo(targetPath);
            entry.AttachmentPath = targetPath;
            entry.AttachmentFileName = Path.GetFileName(targetPath);
            entry.AttachmentContentType = extension.Trim('.').ToLowerInvariant();
            entry.AttachmentSizeBytes = info.Length;
            entry.AttachmentTempPath = null;
        }

        private void RemoveAttachmentFile(string? path)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to remove attachment '{path}': {ex.Message}");
            }
        }

        private void InitializeDatabase()
        {
            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();

            // Key-Value table
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS KeyValueStore (
                    Key TEXT PRIMARY KEY,
                    Value TEXT NOT NULL,
                    Type TEXT NOT NULL
                )");

            // Memory table
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Memory (
                    Id TEXT PRIMARY KEY,
                    ContactId TEXT NOT NULL,
                    Content TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    Importance REAL DEFAULT 1.0,
                    AccessCount INTEGER DEFAULT 0
                )");

            // Global Knowledge table
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS GlobalKnowledge (
                    Id TEXT PRIMARY KEY,
                    Content TEXT NOT NULL,
                    Category TEXT,
                    Tags TEXT,
                    CreatedAt TEXT NOT NULL,
                    LastAccessed TEXT NOT NULL
                )");

            // DataBanks table
            connection.Execute(@" 
                CREATE TABLE IF NOT EXISTS DataBanks (
                    Id TEXT PRIMARY KEY,
                    Name TEXT NOT NULL,
                    Description TEXT,
                    DataEntries TEXT,
                    CreatedAt TEXT NOT NULL,
                    LastModified TEXT NOT NULL
                )");

            // Messages table for conversation history
            connection.Execute(@"
                CREATE TABLE IF NOT EXISTS Messages (
                    Id TEXT PRIMARY KEY,
                    ConversationId TEXT NOT NULL,
                    Content TEXT NOT NULL,
                    Direction TEXT NOT NULL,
                    Type TEXT NOT NULL,
                    MediaType TEXT,
                    FilePath TEXT,
                    Timestamp TEXT NOT NULL,
                    IsRead INTEGER DEFAULT 0
                )");

            // Create index on ConversationId for faster queries
            connection.Execute(@"
                CREATE INDEX IF NOT EXISTS idx_Messages_ConversationId ON Messages(ConversationId)");

            // Create index on Timestamp for ordering
            connection.Execute(@"
                CREATE INDEX IF NOT EXISTS idx_Messages_Timestamp ON Messages(Timestamp DESC)");
        }

        /// <summary>
        /// Adds additional columns to the Memory table if they do not already exist.
        /// This keeps legacy data intact while enabling v2 memory features.
        /// </summary>
        private void EnsureMemoryV2Columns()
        {
            var columns = new[]
            {
                "ALTER TABLE Memory ADD COLUMN Type TEXT",
                "ALTER TABLE Memory ADD COLUMN Metadata TEXT",
                "ALTER TABLE Memory ADD COLUMN TenantId TEXT",
                "ALTER TABLE Memory ADD COLUMN PersonaId TEXT",
                "ALTER TABLE Memory ADD COLUMN ProjectId TEXT",
                "ALTER TABLE Memory ADD COLUMN Pinned INTEGER DEFAULT 0",
                "ALTER TABLE Memory ADD COLUMN TtlSeconds INTEGER",
                "ALTER TABLE Memory ADD COLUMN UpdatedAt TEXT",
                "ALTER TABLE Memory ADD COLUMN LastAccessed TEXT",
                "ALTER TABLE Memory ADD COLUMN Lineage TEXT"
            };

            using var connection = new SQLiteConnection(_connectionString);
            connection.Open();
            foreach (var ddl in columns)
            {
                try
                {
                    connection.Execute(ddl);
                }
                catch
                {
                    // Ignore if column already exists
                }
            }
        }

        /// <summary>
        /// Creates FTS5 virtual table for full-text search on Memory content.
        /// </summary>
        private void EnsureMemoryFts()
        {
            try
            {
                using var connection = new SQLiteConnection(_connectionString);
                connection.Open();
                connection.Execute(@"
                    CREATE VIRTUAL TABLE IF NOT EXISTS Memory_fts USING fts5(
                        Id UNINDEXED,
                        Content,
                        tokenize='porter unicode61'
                    )");
                var ftsCount = Convert.ToInt64(connection.ExecuteScalar("SELECT COUNT(*) FROM Memory_fts") ?? 0);
                var memoryCount = Convert.ToInt64(connection.ExecuteScalar("SELECT COUNT(*) FROM Memory") ?? 0);
                if (ftsCount == 0 && memoryCount > 0)
                {
                    connection.Execute("INSERT INTO Memory_fts(Id, Content) SELECT Id, Content FROM Memory");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FTS5 setup skipped or failed: {ex.Message}");
            }
        }

        private async Task SyncMemoryFtsAsync(SQLiteConnection connection, string id, string content)
        {
            try
            {
                await connection.ExecuteAsync("DELETE FROM Memory_fts WHERE Id = @Id", new { Id = id });
                await connection.ExecuteAsync(
                    "INSERT INTO Memory_fts(Id, Content) VALUES (@Id, @Content)",
                    new { Id = id, Content = content ?? string.Empty });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FTS sync failed for {id}: {ex.Message}");
            }
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var result = await connection.QueryFirstOrDefaultAsync(
                "SELECT Value, Type FROM KeyValueStore WHERE Key = @Key", 
                new { Key = key });

            if (result == null) return null;

            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(result.Value);
        }

        public async Task SetAsync<T>(string key, T value) where T : class
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var json = Newtonsoft.Json.JsonConvert.SerializeObject(value);
            var typeName = typeof(T).Name;

            await connection.ExecuteAsync(@"
                INSERT OR REPLACE INTO KeyValueStore (Key, Value, Type)
                VALUES (@Key, @Value, @Type)",
                new { Key = key, Value = json, Type = typeName });
        }

        public async Task DeleteAsync(string key)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();
            await connection.ExecuteAsync("DELETE FROM KeyValueStore WHERE Key = @Key", new { Key = key });
        }

        public async Task<bool> ExistsAsync(string key)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();
            var result = await connection.ExecuteScalarAsync<int>(
                "SELECT COUNT(*) FROM KeyValueStore WHERE Key = @Key",
                new { Key = key });
            return result > 0;
        }

        public async Task<Dictionary<string, T>> GetAllAsync<T>() where T : class
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var results = await connection.QueryAsync(
                "SELECT Key, Value FROM KeyValueStore WHERE Type = @Type",
                new { Type = typeof(T).Name });

            var dict = new Dictionary<string, T>();
            foreach (dynamic row in results)
            {
                try
                {
                    var key = row.Key as string;
                    var valueJson = row.Value as string;
                    
                    if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(valueJson))
                    {
                        System.Diagnostics.Debug.WriteLine($"Warning: Skipping row with null key or value. Key: {key}");
                        continue;
                    }

                    var value = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(valueJson);
                    if (value != null)
                    {
                        dict[key] = value;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Warning: Failed to deserialize value for key: {key}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error processing row in GetAllAsync<{typeof(T).Name}>: {ex.Message}");
                    continue;
                }
            }
            return dict;
        }

        public async Task ClearAllAsync()
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();
            await connection.ExecuteAsync("DELETE FROM KeyValueStore");
        }

        // IMemoryService Implementation
        public async Task AddMemoryAsync(string contactId, string memory)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync(@"
                INSERT INTO Memory (Id, ContactId, Content, CreatedAt, Importance, AccessCount)
                VALUES (@Id, @ContactId, @Content, @CreatedAt, 1.0, 0)",
                new
                {
                    Id = Guid.NewGuid().ToString(),
                    ContactId = contactId,
                    Content = memory,
                    CreatedAt = DateTime.Now.ToString("O")
                });
        }

        public async Task<List<string>> GetMemoriesAsync(string contactId)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var results = await connection.QueryAsync<string>(
                "SELECT Content FROM Memory WHERE ContactId = @ContactId ORDER BY CreatedAt DESC",
                new { ContactId = contactId });

            return results.ToList();
        }

        public async Task ClearMemoriesAsync(string contactId)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync(
                "DELETE FROM Memory WHERE ContactId = @ContactId",
                new { ContactId = contactId });
        }

        public async Task AddGlobalKnowledgeAsync(string knowledge)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync(@"
                INSERT INTO GlobalKnowledge (Id, Content, CreatedAt, LastAccessed)
                VALUES (@Id, @Content, @CreatedAt, @LastAccessed)",
                new
                {
                    Id = Guid.NewGuid().ToString(),
                    Content = knowledge,
                    CreatedAt = DateTime.Now.ToString("O"),
                    LastAccessed = DateTime.Now.ToString("O")
                });
        }

        public async Task<List<string>> GetGlobalKnowledgeAsync()
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var results = await connection.QueryAsync<string>(
                "SELECT Content FROM GlobalKnowledge ORDER BY CreatedAt DESC");

            return results.ToList();
        }

        public async Task<List<string>> SearchGlobalKnowledgeAsync(string query)
        {
            // Implement full-text search or simple LIKE query
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var results = await connection.QueryAsync<string>(
                "SELECT Content FROM GlobalKnowledge WHERE Content LIKE @Query",
                new { Query = $"%{query}%" });

            List<string> result = results.ToList();
            return result;
        }

        public async Task AddDataBankAsync(DataBank dataBank)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var dataEntriesJson = Newtonsoft.Json.JsonConvert.SerializeObject(dataBank.DataEntries ?? new List<DataBankEntry>());

            await connection.ExecuteAsync(@"
                INSERT OR REPLACE INTO DataBanks (Id, Name, Description, DataEntries, CreatedAt, LastModified)
                VALUES (@Id, @Name, @Description, @DataEntries, @CreatedAt, @LastModified)",
                new
                {
                    dataBank.Id,
                    dataBank.Name,
                    dataBank.Description,
                    DataEntries = dataEntriesJson,
                    CreatedAt = dataBank.CreatedAt.ToString("O"),
                    LastModified = dataBank.LastModified.ToString("O")
                });
        }

        public async Task<DataBank?> GetDataBankAsync(string bankId)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var result = await connection.QueryFirstOrDefaultAsync(
                "SELECT * FROM DataBanks WHERE Id = @Id",
                new { Id = bankId });

            if (result == null) return null;

            var dataEntries = DeserializeDataEntries(result.DataEntries);
            return new DataBank
            {
                Id = result.Id,
                Name = result.Name,
                Description = result.Description,
                DataEntries = dataEntries,
                CreatedAt = DateTime.Parse(result.CreatedAt),
                LastModified = DateTime.Parse(result.LastModified)
            };
        }

        public async Task<List<DataBank>> GetAllDataBanksAsync()
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var results = await connection.QueryAsync("SELECT * FROM DataBanks ORDER BY CreatedAt DESC");

            var banks = new List<DataBank>();
            foreach (var row in results)
            {
                try
                {
                    var idType = row.GetType().GetProperty("Id");
                    var nameType = row.GetType().GetProperty("Name");
                    var descType = row.GetType().GetProperty("Description");
                    var dataEntryType = row.GetType().GetProperty("DataEntries");
                    var createdAtType = row.GetType().GetProperty("CreatedAt");
                    var lastModType = row.GetType().GetProperty("LastModified");

                    if (idType == null || nameType == null || createdAtType == null || lastModType == null)
                    {
                        System.Diagnostics.Debug.WriteLine("Warning: Missing required properties in DataBank row");
                        continue;
                    }

                    var idValue = idType!.GetValue(row);
                    var nameValue = nameType!.GetValue(row);
                    var createdAtValue = createdAtType!.GetValue(row);
                    var lastModValue = lastModType!.GetValue(row);

                    if (idValue == null || nameValue == null || createdAtValue == null || lastModValue == null)
                    {
                        System.Diagnostics.Debug.WriteLine("Warning: Null values in DataBank row");
                        continue;
                    }

                    var dataEntriesRaw = dataEntryType?.GetValue(row)?.ToString();
                    var dataEntries = string.IsNullOrWhiteSpace(dataEntriesRaw)
                        ? new List<DataBankEntry>()
                        : DeserializeDataEntries(dataEntriesRaw) ?? new List<DataBankEntry>();

                    DateTime createdAt = DateTime.Now;
                    DateTime lastModified = DateTime.Now;
                    // createdAtValue and lastModValue are guaranteed to be non-null after the check on line 506
                    if (DateTime.TryParse(createdAtValue!.ToString(), out DateTime parsedCreatedAt))
                    {
                        createdAt = parsedCreatedAt;
                    }
                    if (DateTime.TryParse(lastModValue!.ToString(), out DateTime parsedLastModified))
                    {
                        lastModified = parsedLastModified;
                    }

                    banks.Add(new DataBank
                    {
                        Id = idValue?.ToString() ?? Guid.NewGuid().ToString(),
                        Name = nameValue?.ToString() ?? "",
                        Description = descType?.GetValue(row)?.ToString(),
                        DataEntries = dataEntries ?? new List<DataBankEntry>(),
                        CreatedAt = createdAt,
                        LastModified = lastModified
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing DataBank row: {ex.Message}");
                    continue;
                }
            }
            return banks;
        }

        public async Task AddDataToBankAsync(string bankId, string data)
        {
            var entry = CreateEntryFromString(data);
            await AddDataToBankAsync(bankId, entry);
        }

        public async Task AddDataToBankAsync(string bankId, DataBankEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            var bank = await GetDataBankAsync(bankId);
            if (bank == null)
            {
                return;
            }

            var normalizedEntry = entry;
            if (string.IsNullOrWhiteSpace(normalizedEntry.Id))
            {
                normalizedEntry.Id = Guid.NewGuid().ToString();
            }
            if (string.IsNullOrWhiteSpace(normalizedEntry.Title))
            {
                normalizedEntry.Title = CreateEntryFromString(normalizedEntry.Content ?? string.Empty).Title;
            }

            normalizedEntry.Content ??= string.Empty;
            normalizedEntry.CreatedAt = normalizedEntry.CreatedAt == default ? DateTime.Now : normalizedEntry.CreatedAt;
            normalizedEntry.LastModified = DateTime.Now;

            if (normalizedEntry.AttachmentMarkedForRemoval)
            {
                normalizedEntry.AttachmentPath = null;
                normalizedEntry.AttachmentFileName = null;
                normalizedEntry.AttachmentContentType = null;
                normalizedEntry.AttachmentSizeBytes = null;
            }

            if (!string.IsNullOrWhiteSpace(normalizedEntry.AttachmentTempPath))
            {
                EnsureAttachmentCopied(bank.Id, normalizedEntry);
            }

            bank.DataEntries ??= new List<DataBankEntry>();
            bank.DataEntries.Add(normalizedEntry);
            bank.LastModified = DateTime.Now;
            await AddDataBankAsync(bank);
        }

        public async Task UpdateDataBankEntryAsync(string bankId, DataBankEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            var bank = await GetDataBankAsync(bankId);
            if (bank == null || bank.DataEntries == null)
            {
                return;
            }

            var existing = bank.DataEntries.FirstOrDefault(e => e.Id == entry.Id);
            if (existing == null)
            {
                return;
            }

            existing.Title = entry.Title;
            existing.Content = entry.Content;
            existing.Category = entry.Category;
            existing.Tags = entry.Tags ?? new List<string>();
            existing.Importance = entry.Importance;
            existing.LastModified = DateTime.Now;

            if (entry.AttachmentMarkedForRemoval)
            {
                if (!string.IsNullOrWhiteSpace(existing.AttachmentPath))
                {
                    RemoveAttachmentFile(existing.AttachmentPath);
                }
                existing.AttachmentPath = null;
                existing.AttachmentFileName = null;
                existing.AttachmentContentType = null;
                existing.AttachmentSizeBytes = null;
                existing.AttachmentTempPath = null;
            }
            else if (!string.IsNullOrWhiteSpace(entry.AttachmentTempPath) && File.Exists(entry.AttachmentTempPath))
            {
                if (!string.IsNullOrWhiteSpace(existing.AttachmentPath))
                {
                    RemoveAttachmentFile(existing.AttachmentPath);
                }

                existing.AttachmentTempPath = entry.AttachmentTempPath;
                existing.AttachmentFileName = entry.AttachmentFileName;
                existing.AttachmentContentType = entry.AttachmentContentType;
                existing.AttachmentSizeBytes = entry.AttachmentSizeBytes;
                EnsureAttachmentCopied(bankId, existing);
            }
            else if (!string.IsNullOrWhiteSpace(entry.AttachmentPath))
            {
                existing.AttachmentPath = entry.AttachmentPath;
                existing.AttachmentFileName = entry.AttachmentFileName;
                existing.AttachmentContentType = entry.AttachmentContentType;
                existing.AttachmentSizeBytes = entry.AttachmentSizeBytes;
            }

            bank.LastModified = DateTime.Now;
            await AddDataBankAsync(bank);
        }

        public async Task DeleteDataBankEntryAsync(string bankId, string entryId)
        {
            if (string.IsNullOrWhiteSpace(entryId))
            {
                return;
            }

            var bank = await GetDataBankAsync(bankId);
            if (bank == null || bank.DataEntries == null)
            {
                return;
            }

            var existing = bank.DataEntries.FirstOrDefault(e => e.Id == entryId);
            if (existing == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(existing.AttachmentPath))
            {
                RemoveAttachmentFile(existing.AttachmentPath);
            }

            bank.DataEntries.Remove(existing);
            bank.LastModified = DateTime.Now;
            await AddDataBankAsync(bank);
        }

        public async Task DeleteDataBankAsync(string bankId)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync(
                "DELETE FROM DataBanks WHERE Id = @Id",
                new { Id = bankId });

            try
            {
                var bankFolder = Path.Combine(_dataBankFilesPath, MakeSafeFileName(bankId));
                if (Directory.Exists(bankFolder))
                {
                    Directory.Delete(bankFolder, true);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to delete data bank folder for {bankId}: {ex.Message}");
            }
        }

        // Message persistence methods
        public async Task SaveMessageAsync(ConversationMessage message)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync(@"
                INSERT OR REPLACE INTO Messages (Id, ConversationId, Content, Direction, Type, MediaType, FilePath, Timestamp, IsRead)
                VALUES (@Id, @ConversationId, @Content, @Direction, @Type, @MediaType, @FilePath, @Timestamp, @IsRead)",
                new
                {
                    message.Id,
                    message.ConversationId,
                    message.Content,
                    Direction = message.Direction.ToString(),
                    Type = message.Type.ToString(),
                    message.MediaType,
                    message.FilePath,
                    Timestamp = message.Timestamp.ToString("O"),
                    IsRead = message.IsRead ? 1 : 0
                });
        }

        public async Task<List<ConversationMessage>> GetMessagesAsync(string conversationId, int limit = 100)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var results = await connection.QueryAsync(@"
                SELECT Id, ConversationId, Content, Direction, Type, MediaType, FilePath, Timestamp, IsRead
                FROM Messages
                WHERE ConversationId = @ConversationId
                ORDER BY Timestamp DESC
                LIMIT @Limit",
                new { ConversationId = conversationId, Limit = limit });

            var messages = new List<ConversationMessage>();
            foreach (dynamic row in results)
            {
                try
                {
                    var message = new ConversationMessage
                    {
                        Id = row.Id as string ?? Guid.NewGuid().ToString(),
                        ConversationId = row.ConversationId as string ?? conversationId,
                        Content = row.Content as string ?? string.Empty,
                        Direction = Enum.TryParse<MessageDirection>(row.Direction as string, out var direction) ? direction : MessageDirection.Incoming,
                        Type = Enum.TryParse<MessageType>(row.Type as string, out var type) ? type : MessageType.Text,
                        MediaType = row.MediaType as string,
                        FilePath = row.FilePath as string,
                        IsRead = (row.IsRead as int? ?? 0) == 1
                    };

                    if (DateTime.TryParse(row.Timestamp as string, out var timestamp))
                    {
                        message.Timestamp = timestamp;
                    }

                    messages.Add(message);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error parsing message row: {ex.Message}");
                    continue;
                }
            }

            // Return in chronological order (oldest first)
            return messages.OrderBy(m => m.Timestamp).ToList();
        }

        public async Task DeleteMessagesAsync(string conversationId)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync(
                "DELETE FROM Messages WHERE ConversationId = @ConversationId",
                new { ConversationId = conversationId });
        }

        public async Task<ConversationMessage?> GetLastMessageAsync(string conversationId)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var result = await connection.QueryFirstOrDefaultAsync(@"
                SELECT Id, ConversationId, Content, Direction, Type, MediaType, FilePath, Timestamp, IsRead
                FROM Messages
                WHERE ConversationId = @ConversationId
                ORDER BY Timestamp DESC
                LIMIT 1",
                new { ConversationId = conversationId });

            if (result == null) return null;

            try
            {
                var message = new ConversationMessage
                {
                    Id = result.Id as string ?? Guid.NewGuid().ToString(),
                    ConversationId = result.ConversationId as string ?? conversationId,
                    Content = result.Content as string ?? string.Empty,
                    Direction = Enum.TryParse<MessageDirection>(result.Direction as string, out var direction) ? direction : MessageDirection.Incoming,
                    Type = Enum.TryParse<MessageType>(result.Type as string, out var type) ? type : MessageType.Text,
                    MediaType = result.MediaType as string,
                    FilePath = result.FilePath as string,
                    IsRead = (result.IsRead as int? ?? 0) == 1
                };

                if (DateTime.TryParse(result.Timestamp as string, out var timestamp))
                {
                    message.Timestamp = timestamp;
                }

                return message;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error parsing last message: {ex.Message}");
                return null;
            }
        }

        // --- IMemoryService v2 ---

        public async Task UpsertMemoryAsync(MemoryItem item)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var now = DateTime.UtcNow.ToString("O");

            // Try to keep original CreatedAt if the record exists
            var existingCreated = await connection.ExecuteScalarAsync<string?>(
                "SELECT CreatedAt FROM Memory WHERE Id = @Id",
                new { item.Id });

            var createdAt = string.IsNullOrWhiteSpace(existingCreated) ? now : existingCreated!;

            var metadataJson = item.Metadata != null && item.Metadata.Count > 0
                ? Newtonsoft.Json.JsonConvert.SerializeObject(item.Metadata)
                : null;

            var lineageJson = item.Lineage != null && item.Lineage.Count > 0
                ? Newtonsoft.Json.JsonConvert.SerializeObject(item.Lineage)
                : null;

            await connection.ExecuteAsync(@"
                INSERT OR REPLACE INTO Memory 
                (Id, ContactId, Content, CreatedAt, Importance, AccessCount, Type, Metadata, TenantId, PersonaId, ProjectId, Pinned, TtlSeconds, UpdatedAt, LastAccessed, Lineage)
                VALUES (@Id, @ContactId, @Content, @CreatedAt, @Importance, 
                        (SELECT COALESCE((SELECT AccessCount FROM Memory WHERE Id = @Id), 0)), 
                        @Type, @Metadata, @TenantId, @PersonaId, @ProjectId, @Pinned, @TtlSeconds, @UpdatedAt, @LastAccessed, @Lineage)",
                new
                {
                    item.Id,
                    ContactId = item.ContactId ?? string.Empty,
                    item.Content,
                    CreatedAt = createdAt,
                    Importance = item.Importance,
                    Type = item.Type,
                    Metadata = metadataJson,
                    item.TenantId,
                    item.PersonaId,
                    item.ProjectId,
                    Pinned = item.Pinned ? 1 : 0,
                    TtlSeconds = item.TtlSeconds,
                    UpdatedAt = now,
                    LastAccessed = now,
                    Lineage = lineageJson
                });

            await SyncMemoryFtsAsync(connection, item.Id, item.Content);

            if (_pgVector?.IsEnabled == true && !string.IsNullOrEmpty(item.Content))
            {
                try
                {
                    var embedding = await EmbeddingHelper.CreateEmbeddingAsync(item.Content);
                    await _pgVector.UpsertAsync(item.Id, embedding);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"PgVector upsert failed for {item.Id}: {ex.Message}");
                }
            }
        }

        public async Task<MemoryItem?> GetMemoryAsync(string id)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var row = await connection.QueryFirstOrDefaultAsync(@"
                SELECT * FROM Memory WHERE Id = @Id",
                new { Id = id });

            if (row == null) return null;

            return MapMemoryRow(row);
        }

        public async Task<IReadOnlyList<MemorySearchResult>> SearchMemoryAsync(MemorySearchRequest request)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            var parameters = new DynamicParameters();
            parameters.Add("@Limit", request.Limit);

            var filterClauses = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.TenantId))
            {
                filterClauses.Add("m.TenantId = @TenantId");
                parameters.Add("@TenantId", request.TenantId);
            }
            if (!string.IsNullOrWhiteSpace(request.PersonaId))
            {
                filterClauses.Add("m.PersonaId = @PersonaId");
                parameters.Add("@PersonaId", request.PersonaId);
            }
            if (!string.IsNullOrWhiteSpace(request.ProjectId))
            {
                filterClauses.Add("m.ProjectId = @ProjectId");
                parameters.Add("@ProjectId", request.ProjectId);
            }
            if (!string.IsNullOrWhiteSpace(request.ContactId))
            {
                filterClauses.Add("m.ContactId = @ContactId");
                parameters.Add("@ContactId", request.ContactId);
            }
            if (!string.IsNullOrWhiteSpace(request.Type))
            {
                filterClauses.Add("m.Type = @Type");
                parameters.Add("@Type", request.Type);
            }
            var filterWhere = filterClauses.Count > 0 ? $"AND {string.Join(" AND ", filterClauses)}" : string.Empty;

            IEnumerable<dynamic> rows;
            if (!string.IsNullOrWhiteSpace(request.Query))
            {
                if (_pgVector?.IsEnabled == true && _appConfig != null)
                {
                    return await HybridSearchAsync(connection, request, parameters, filterClauses, filterWhere);
                }

                try
                {
                    var ftsQuery = "\"" + (request.Query ?? "").Replace("\"", "\"\"") + "\"";
                    parameters.Add("@FtsQuery", ftsQuery);

                    rows = await connection.QueryAsync<dynamic>($@"
                        SELECT m.*, f.rank AS FtsRank
                        FROM Memory_fts f
                        JOIN Memory m ON m.Id = f.Id
                        WHERE f MATCH @FtsQuery {filterWhere}
                        ORDER BY f.rank ASC, m.UpdatedAt DESC
                        LIMIT @Limit", parameters);
                }
                catch
                {
                    rows = await FallbackLexicalSearchAsync(connection, request, parameters, filterWhere);
                }
            }
            else
            {
                var where = filterClauses.Count > 0 ? $"WHERE {string.Join(" AND ", filterClauses)}" : string.Empty;
                rows = await connection.QueryAsync($@"
                    SELECT m.*, NULL AS FtsRank FROM Memory m
                    {where}
                    ORDER BY m.UpdatedAt DESC
                    LIMIT @Limit", parameters);
            }

            var results = new List<MemorySearchResult>();
            foreach (var row in rows)
            {
                var item = MapMemoryRow(row);
                if (item == null) continue;

                var ftsRank = row.FtsRank as double?;
                var score = ftsRank.HasValue ? (ftsRank.Value <= 0 ? 1.0 : 1.0 / (1.0 + Math.Abs(ftsRank.Value))) : 1.0;

                results.Add(new MemorySearchResult
                {
                    Id = item.Id,
                    Content = item.Content,
                    Type = item.Type,
                    Metadata = item.Metadata,
                    Score = score,
                    LastAccessed = item.LastAccessed
                });
            }

            return results;
        }

        private async Task<IReadOnlyList<MemorySearchResult>> HybridSearchAsync(
            SQLiteConnection connection, MemorySearchRequest request, DynamicParameters parameters,
            List<string> filterClauses, string filterWhere)
        {
            var lexicalWeight = Math.Clamp(_appConfig!.HybridLexicalWeight, 0, 1);
            var vectorWeight = 1.0 - lexicalWeight;
            var fetchLimit = Math.Max(request.Limit * 2, 50);

            var idToResult = new Dictionary<string, (MemoryItem Item, double LexicalScore, double VectorScore)>();

            try
            {
                var ftsQuery = "\"" + (request.Query ?? "").Replace("\"", "\"\"") + "\"";
                var ftsParams = new DynamicParameters(parameters);
                ftsParams.Add("@FtsQuery", ftsQuery);
                ftsParams.Add("@FetchLimit", fetchLimit);

                var ftsRows = await connection.QueryAsync<dynamic>($@"
                    SELECT m.*, f.rank AS FtsRank
                    FROM Memory_fts f
                    JOIN Memory m ON m.Id = f.Id
                    WHERE f MATCH @FtsQuery {filterWhere}
                    ORDER BY f.rank ASC, m.UpdatedAt DESC
                    LIMIT @FetchLimit", ftsParams);

                foreach (var row in ftsRows)
                {
                    var item = MapMemoryRow(row);
                    if (item == null) continue;

                    var ftsRank = row.FtsRank as double?;
                    var lexScore = ftsRank.HasValue
                        ? (ftsRank.Value <= 0 ? 1.0 : 1.0 / (1.0 + Math.Abs(ftsRank.Value)))
                        : 1.0;

                    idToResult[item.Id] = (item, lexScore, 0);
                }
            }
            catch
            {
                return Array.Empty<MemorySearchResult>();
            }

            if (_pgVector?.IsEnabled == true)
            {
                try
                {
                    var embedding = await EmbeddingHelper.CreateEmbeddingAsync(request.Query ?? string.Empty);
                    var vectorResults = await _pgVector.SearchAsync(embedding, fetchLimit);

                    foreach (var (id, vecScore) in vectorResults)
                    {
                        if (idToResult.TryGetValue(id, out var existing))
                        {
                            idToResult[id] = (existing.Item, existing.LexicalScore, vecScore);
                        }
                        else
                        {
                            var row = await connection.QueryFirstOrDefaultAsync<dynamic>(
                                "SELECT * FROM Memory WHERE Id = @Id", new { Id = id });
                            if (row == null) continue;

                            var filterMatch = true;
                            if (!string.IsNullOrWhiteSpace(request.TenantId) && (row.TenantId as string) != request.TenantId)
                                filterMatch = false;
                            if (filterMatch && !string.IsNullOrWhiteSpace(request.PersonaId) && (row.PersonaId as string) != request.PersonaId)
                                filterMatch = false;
                            if (filterMatch && !string.IsNullOrWhiteSpace(request.ProjectId) && (row.ProjectId as string) != request.ProjectId)
                                filterMatch = false;
                            if (filterMatch && !string.IsNullOrWhiteSpace(request.ContactId) && (row.ContactId as string) != request.ContactId)
                                filterMatch = false;
                            if (filterMatch && !string.IsNullOrWhiteSpace(request.Type) && (row.Type as string) != request.Type)
                                filterMatch = false;
                            if (!filterMatch) continue;

                            var item = MapMemoryRow(row);
                            if (item != null)
                                idToResult[id] = (item, 0, vecScore);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Hybrid vector search failed: {ex.Message}");
                }
            }

            var results = idToResult.Values
                .Select(x =>
                {
                    var hybridScore = lexicalWeight * x.LexicalScore + vectorWeight * x.VectorScore;
                    return new MemorySearchResult
                    {
                        Id = x.Item.Id,
                        Content = x.Item.Content,
                        Type = x.Item.Type,
                        Metadata = x.Item.Metadata,
                        Score = hybridScore,
                        LastAccessed = x.Item.LastAccessed
                    };
                })
                .OrderByDescending(r => r.Score)
                .Take(request.Limit)
                .ToList();

            return results;
        }

        private static async Task<IEnumerable<dynamic>> FallbackLexicalSearchAsync(
            SQLiteConnection connection, MemorySearchRequest request, DynamicParameters parameters, string filterWhere)
        {
            var clauses = new List<string>();
            if (!string.IsNullOrWhiteSpace(request.TenantId)) clauses.Add("TenantId = @TenantId");
            if (!string.IsNullOrWhiteSpace(request.PersonaId)) clauses.Add("PersonaId = @PersonaId");
            if (!string.IsNullOrWhiteSpace(request.ProjectId)) clauses.Add("ProjectId = @ProjectId");
            if (!string.IsNullOrWhiteSpace(request.ContactId)) clauses.Add("ContactId = @ContactId");
            if (!string.IsNullOrWhiteSpace(request.Type)) clauses.Add("Type = @Type");
            clauses.Add("(Content LIKE @Query OR Metadata LIKE @Query)");
            parameters.Add("@Query", $"%{request.Query}%");

            var where = string.Join(" AND ", clauses);
            return await connection.QueryAsync($@"
                SELECT * FROM Memory
                WHERE {where}
                ORDER BY UpdatedAt DESC
                LIMIT @Limit", parameters);
        }

        public async Task<bool> DeleteMemoryAsync(string id)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            try { await connection.ExecuteAsync("DELETE FROM Memory_fts WHERE Id = @Id", new { Id = id }); }
            catch { /* FTS table may not exist */ }
            if (_pgVector?.IsEnabled == true)
            {
                try { await _pgVector.DeleteAsync(id); }
                catch (Exception ex) { Debug.WriteLine($"PgVector delete failed for {id}: {ex.Message}"); }
            }
            var rows = await connection.ExecuteAsync("DELETE FROM Memory WHERE Id = @Id", new { Id = id });
            return rows > 0;
        }

        public async Task PinMemoryAsync(string id, bool pinned)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync(
                "UPDATE Memory SET Pinned = @Pinned, UpdatedAt = @UpdatedAt WHERE Id = @Id",
                new { Pinned = pinned ? 1 : 0, UpdatedAt = DateTime.UtcNow.ToString("O"), Id = id });
        }

        public async Task TouchMemoryAsync(string id)
        {
            using var connection = new SQLiteConnection(_connectionString);
            await connection.OpenAsync();

            await connection.ExecuteAsync(
                "UPDATE Memory SET LastAccessed = @LastAccessed, AccessCount = AccessCount + 1 WHERE Id = @Id",
                new { LastAccessed = DateTime.UtcNow.ToString("O"), Id = id });
        }

        private static MemoryItem? MapMemoryRow(dynamic row)
        {
            try
            {
                var metadataRaw = row.Metadata as string;
                Dictionary<string, string>? metadata = null;
                if (!string.IsNullOrWhiteSpace(metadataRaw))
                {
                    metadata = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(metadataRaw);
                }

                DateTime TryParseDate(string? input)
                {
                    if (DateTime.TryParse(input, out var dt))
                    {
                        return dt;
                    }
                    return DateTime.UtcNow;
                }

                var result = new MemoryItem
                {
                    Id = row.Id as string ?? Guid.NewGuid().ToString(),
                    ContactId = row.ContactId as string,
                    Content = row.Content as string ?? string.Empty,
                    TenantId = row.TenantId as string,
                    PersonaId = row.PersonaId as string,
                    ProjectId = row.ProjectId as string,
                    Type = row.Type as string ?? "memory",
                    Metadata = metadata,
                    Importance = row.Importance as double? ?? 1.0,
                    Pinned = (row.Pinned as long? ?? row.Pinned as int? ?? 0) == 1,
                    CreatedAt = TryParseDate(row.CreatedAt as string),
                    UpdatedAt = TryParseDate(row.UpdatedAt as string),
                    LastAccessed = TryParseDate(row.LastAccessed as string),
                    TtlSeconds = row.TtlSeconds as long?
                };

                var lineageRaw = row.Lineage as string;
                if (!string.IsNullOrWhiteSpace(lineageRaw))
                {
                    result.Lineage = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(lineageRaw);
                }

                return result;
            }
            catch
            {
                return null;
            }
        }
    }
}
