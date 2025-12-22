# WinUI 3 Overlay - Troubleshooting Guide

## Common Issues and Solutions

### Issue: "MainWindow.xaml and MainWindow.xaml.cs naming error"

This typically happens due to one of these reasons:

#### Solution 1: Check File Naming (Case Sensitive)
WinUI 3 is case-sensitive. Ensure files are named exactly:
- `MainWindow.xaml` (capital M, capital W)
- `MainWindow.xaml.cs` (capital M, capital W)

NOT:
- ❌ `mainwindow.xaml`
- ❌ `Mainwindow.xaml`
- ❌ `mainWindow.xaml`

#### Solution 2: Check x:Class Declaration
In `MainWindow.xaml`, the x:Class must match exactly:

```xml
<Window
    x:Class="LLMODOverlay.MainWindow"
    ...>
```

The namespace and class name must match your C# file:
```csharp
namespace LLMODOverlay
{
    public sealed partial class MainWindow : Window
    {
        ...
    }
}
```

#### Solution 3: Verify Project Structure
Your WinUI 3 project should have this structure:

```
LLMODOverlay/
├── LLMODOverlay.csproj
├── App.xaml
├── App.xaml.cs
├── MainWindow.xaml          ← Must be here
├── MainWindow.xaml.cs       ← Must be here
├── Package.appxmanifest
└── Services/
    └── ModuleWebSocketClient.cs
```

#### Solution 4: Clean and Rebuild
1. Close Visual Studio
2. Delete `bin` and `obj` folders
3. Reopen Visual Studio
4. Clean Solution (Build → Clean Solution)
5. Rebuild Solution (Build → Rebuild Solution)

#### Solution 5: Check .csproj File
Open `LLMODOverlay.csproj` and ensure MainWindow is included:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net6.0-windows10.0.19041.0</TargetFramework>
    <TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
    <UseWinUI>true</UseWinUI>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.WindowsAppSDK" Version="1.4.231008000" />
  </ItemGroup>

  <ItemGroup>
    <Page Include="MainWindow.xaml" />
  </ItemGroup>
</Project>
```

### Issue: "Cannot find MainWindow in namespace"

#### Solution: Ensure Partial Class
Both files must use `partial` keyword:

**MainWindow.xaml.cs:**
```csharp
namespace LLMODOverlay
{
    public sealed partial class MainWindow : Window  // ← partial is required
    {
        public MainWindow()
        {
            this.InitializeComponent();
        }
    }
}
```

**Generated MainWindow.g.cs** (auto-generated):
```csharp
namespace LLMODOverlay
{
    partial class MainWindow  // ← partial is required
    {
        // Auto-generated code
    }
}
```

### Issue: "InitializeComponent does not exist"

This means the XAML file isn't being compiled properly.

#### Solution:
1. Right-click `MainWindow.xaml` in Solution Explorer
2. Properties → Build Action → Set to "Page"
3. Clean and Rebuild

### Complete Working Example

Here's a minimal working WinUI 3 window:

**MainWindow.xaml:**
```xml
<?xml version="1.0" encoding="utf-8"?>
<Window
    x:Class="LLMODOverlay.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    mc:Ignorable="d">

    <Grid>
        <TextBlock Text="LLMOD Overlay" 
                   HorizontalAlignment="Center" 
                   VerticalAlignment="Center"
                   FontSize="24"/>
    </Grid>
</Window>
```

**MainWindow.xaml.cs:**
```csharp
using Microsoft.UI.Xaml;

namespace LLMODOverlay
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "LLMOD Overlay";
        }
    }
}
```

**App.xaml.cs:**
```csharp
using Microsoft.UI.Xaml;

namespace LLMODOverlay
{
    public partial class App : Application
    {
        private Window m_window;

        public App()
        {
            this.InitializeComponent();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();
            m_window.Activate();
        }
    }
}
```

### Step-by-Step: Create WinUI 3 Project Correctly

1. **Open Visual Studio 2022**

2. **Create New Project**
   - Search for "WinUI"
   - Select "Blank App, Packaged (WinUI 3 in Desktop)"
   - Click Next

3. **Configure Project**
   - Project name: `LLMODOverlay`
   - Location: Choose your location
   - Solution name: `LLMODOverlay`
   - Click Create

4. **Select Target Version**
   - Target version: Windows 10, version 2004 (build 19041)
   - Minimum version: Windows 10, version 1809 (build 17763)
   - Click OK

5. **Verify Files Created**
   - ✅ App.xaml
   - ✅ App.xaml.cs
   - ✅ MainWindow.xaml (should exist by default)
   - ✅ MainWindow.xaml.cs (should exist by default)

6. **Test Build**
   - Press F5 to build and run
   - You should see a blank window

7. **Add WebSocket Support**
   ```powershell
   Install-Package System.Net.WebSockets.Client
   Install-Package Newtonsoft.Json
   ```

### Common Error Messages and Fixes

| Error | Cause | Fix |
|-------|-------|-----|
| "The name 'InitializeComponent' does not exist" | XAML not compiling | Set Build Action to "Page" |
| "MainWindow does not exist in namespace" | Wrong namespace | Check x:Class matches namespace |
| "partial class expected" | Missing partial keyword | Add `partial` to class declaration |
| "Cannot find MainWindow.g.cs" | Build failed | Clean and rebuild solution |
| "XAML parse error" | Invalid XAML syntax | Check XML syntax, closing tags |

### Still Having Issues?

If you're still getting errors, please share:
1. The exact error message
2. Your MainWindow.xaml content
3. Your MainWindow.xaml.cs content
4. Your project structure (screenshot of Solution Explorer)

I can then provide specific guidance for your situation.

## Alternative: Start with Template

If you're having persistent issues, try this:

1. Create a new WinUI 3 project from template
2. Don't modify MainWindow initially
3. Build and run to verify it works
4. Then gradually add your custom code
5. Build after each change to catch issues early

This helps isolate whether the issue is with project setup or your custom code.