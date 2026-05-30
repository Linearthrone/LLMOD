using HouseVictoria.Services.Trading;
using Xunit;

namespace HouseVictoria.Tests
{
    public class Mt4PathResolverTests
    {
        [Fact]
        public void Resolve_MapsInstallPathToTerminalDataFolder()
        {
            const string installPath = @"C:\Program Files (x86)\MetaTrader 4 FOREX.com US";
            if (!Directory.Exists(installPath))
            {
                return;
            }

            var resolved = Mt4PathResolver.Resolve(installPath);

            Assert.NotEqual(installPath, resolved, StringComparer.OrdinalIgnoreCase);
            Assert.True(Directory.Exists(Path.Combine(resolved, "MQL4", "Files")));
            Assert.True(Mt4PathResolver.IsWritableTerminalDataPath(resolved));
        }

        [Fact]
        public void FindTerminalByOrigin_ReturnsTerminalWhenInstallPathMatches()
        {
            const string installPath = @"C:\Program Files (x86)\MetaTrader 4 FOREX.com US";
            if (!Directory.Exists(installPath))
            {
                return;
            }

            var terminal = Mt4PathResolver.FindTerminalByOrigin(installPath);

            Assert.NotNull(terminal);
            Assert.True(Directory.Exists(Path.Combine(terminal!, "MQL4")));
        }

        [Fact]
        public async Task ConnectAsync_UsesResolvedTerminalPath()
        {
            const string installPath = @"C:\Program Files (x86)\MetaTrader 4 FOREX.com US";
            if (!Directory.Exists(installPath))
            {
                return;
            }

            var service = new MetaTrader4Service();
            var connected = await service.ConnectAsync(installPath);
            var status = await service.GetStatusAsync();

            Assert.True(connected);
            Assert.True(status.IsConnected);
            Assert.Contains("MetaQuotes", status.MT4DataPath ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.True(Directory.Exists(Path.Combine(status.MT4DataPath!, "MQL4", "Files", "HouseVictoria")));

            await service.DisconnectAsync();
        }

        [Fact]
        public void IsWritableTerminalDataPath_ReturnsTrueForTempMql4Layout()
        {
            var temp = Path.Combine(Path.GetTempPath(), "hv-mt4-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(temp, "MQL4", "Files"));

            try
            {
                Assert.True(Mt4PathResolver.IsWritableTerminalDataPath(temp));
            }
            finally
            {
                Directory.Delete(temp, recursive: true);
            }
        }
    }
}
