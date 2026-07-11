using HouseVictoria.Core.Models;
using HouseVictoria.Services.Persistence;
using Xunit;

namespace HouseVictoria.Tests;

public class DataBankPersistenceTests
{
    [Fact]
    public async Task GetAllDataBanksAsync_ReturnsSavedBanks()
    {
        var root = Path.Combine(Path.GetTempPath(), "hv-databank-" + Guid.NewGuid().ToString("N"));
        var memoryPath = Path.Combine(root, "Memory");
        Directory.CreateDirectory(memoryPath);

        try
        {
            var config = new AppConfig { PersistentMemoryPath = memoryPath, DataBankPath = Path.Combine(root, "Databanks") };
            var service = new DatabasePersistenceService(config);

            var bank = new DataBank
            {
                Id = Guid.NewGuid().ToString(),
                Name = "Test Bank",
                Description = "integration",
                DataEntries = new List<DataBankEntry>
                {
                    new() { Title = "Entry 1", Content = "hello" }
                }
            };

            await service.AddDataBankAsync(bank);

            var all = await service.GetAllDataBanksAsync();

            Assert.Single(all);
            Assert.Equal("Test Bank", all[0].Name);
            Assert.Single(all[0].DataEntries);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
