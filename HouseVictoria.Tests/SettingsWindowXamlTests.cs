using System.Threading;
using System.Windows;
using HouseVictoria.App;
using HouseVictoria.App.Screens.Windows;
using HouseVictoria.App.Services;
using HouseVictoria.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace HouseVictoria.Tests;

public class SettingsWindowXamlTests
{
    [Fact]
    public void SettingsWindow_InitializeComponent_DoesNotThrow()
    {
        Exception? caught = null;

        var thread = new Thread(() =>
        {
            try
            {
                var app = new global::HouseVictoria.App.App();
                app.InitializeComponent();
                ThemeManager.ApplyTheme("ObsidianFieldDark");
                _ = new SettingsWindow();
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join(TimeSpan.FromSeconds(30));

        Assert.True(caught == null, caught?.ToString() ?? "SettingsWindow failed to load");
    }
}
