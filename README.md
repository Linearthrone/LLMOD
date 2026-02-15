# House Victoria

A modular overlay desktop application inspired by Xbox Game Bar, featuring multimodal AI chat interface, SMS/MMS communication, project management, system monitoring, and virtual environment integration.

## Features

### Core Modules

1. **Main Tray (Bottom Right)**
   - SMS/MMS chat interface activation
   - AI model management
   - Configuration settings access
   - Glass panel overlay with click-through support

2. **Top Tray (Auto-hiding)**
   - Drag-and-drop data bank context upload
   - Generated files/media gallery
   - Global knowledge event log
   - Projects/Goals button

3. **System Monitor Drawer (Left Edge)**
   - Real-time CPU/GPU metrics (usage, temperature, fan speed)
   - RAM monitoring
   - AI status indicators
   - Virtual environment status
   - Server management with restart/stop controls
   - "House Victoria" calligraphy branding

4. **SMS/MMS Window**
   - Multimodal chat interface
   - Text, image, video, and document sharing
   - AI Contact book integration
   - Video call support

5. **AI Models & Personas Window**
   - Load/unload AI models from Ollama
   - Create AI personas with custom prompts
   - AI Contact book management

6. **Settings Window**
   - SMS/MMS configuration
   - MCP server settings (autonomous behavior)
   - Persistent memory configuration
   - Avatar and locomotion settings
   - TTS configuration
   - Tools configuration
   - Virtual environment (Unreal Engine) settings
   - LLM server (Ollama) configuration

7. **Projects Board** ✅
   - Create goals/projects with priority ratings (1-10 slider)
   - Project list view with card-based display
   - Filtering by type and phase
   - Sorting by name, priority, deadline, completion
   - Project phases and transitions (Planning, InProgress, Review, Completed, etc.)
   - Roadblocks tracking (add/remove in detail dialog)
   - AI collaboration logging (timeline view with filtering)
   - Artifact management (upload, preview, download, delete files)
   - Project detail dialog with tabs (Overview, Roadblocks, Artifacts, AI Logs)
   - Full CRUD operations (create, read, update, delete)
   - AI contact assignment

## Architecture

### Solution Structure

```
HouseVictoria/
├── HouseVictoria.sln
├── HouseVictoria.Core/           # Core interfaces and models
│   ├── Interfaces/              # Service interfaces
│   ├── Models/                  # Data models
│   └── Utils/                   # Helper functions
├── HouseVictoria.Services/      # Service implementations
│   ├── AIServices/              # Ollama AI service
│   ├── Communication/           # SMS/MMS service
│   ├── Persistence/             # SQLite database
│   ├── ProjectManagement/       # Project service
│   ├── SystemMonitor/           # System metrics
│   └── VirtualEnvironment/      # Unreal Engine integration
└── HouseVictoria.App/           # WPF application
    ├── Assets/                  # Icons, images, fonts
    ├── Controls/                # Custom controls
    ├── Converters/              # Value converters
    ├── Data/                    # Data models for UI
    ├── HelperClasses/           # Helper classes
    ├── Screens/                 # UI screens
    │   ├── Trays/              # Overlay trays
    │   └── Windows/            # Popup windows
    └── Styles/                  # Resource dictionaries
```

### Technology Stack

- **Framework**: .NET 8.0 WPF
- **Language**: C# 12
- **Database**: SQLite with Dapper
- **AI**: Ollama integration
- **Virtual Environment**: Unreal Engine WebSocket
- **UI Framework**: MaterialDesignInXaml
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Logging**: Serilog

## Prerequisites

- .NET 8.0 SDK
- Visual Studio 2022 (recommended) or VS Code with C# extension
- Ollama (for AI functionality)
- Optional: Unreal Engine (for virtual environment features)

## Building the Project

### Using Visual Studio

1. Open `HouseVictoria.sln` in Visual Studio 2022
2. Restore NuGet packages (Tools > NuGet Package Manager > Manage NuGet Packages for Solution)
3. Build the solution (Build > Build Solution or Ctrl+Shift+B)
4. Run the application (F5 or Debug > Start Debugging)

### Using Command Line

```bash
# Navigate to solution directory
cd C:\Users\kurtw\Victoria\HouseVictoria

# Restore packages
dotnet restore

# Build the solution
dotnet build

# Run the application
dotnet run --project HouseVictoria.App\HouseVictoria.App.csproj
```

## Configuration

Edit `HouseVictoria.App\App.config` to configure endpoints and settings. For **Elite Dangerous** users: you can use an AI contact as your ship computer via **COVAS: Next**; see [COVAS_ELITE_DANGEROUS_SETUP.md](COVAS_ELITE_DANGEROUS_SETUP.md).

```xml
<appSettings>
  <add key="OllamaEndpoint" value="http://localhost:11434"/>
  <add key="MCPServerEndpoint" value="http://localhost:8080"/>
  <add key="UnrealEngineEndpoint" value="ws://localhost:8888"/>
  <add key="TTSEndpoint" value="http://localhost:5000"/>
  <add key="EnableOverlay" value="true"/>
  <add key="AutoHideTrays" value="true"/>
</appSettings>
```

## Module Development Plan

The project is designed to be developed module-by-module with testing at each stage:

### Phase 1: Core Infrastructure ✅
- [x] Solution and project structure
- [x] Core interfaces and models
- [x] Base service implementations
- [x] Main application shell

### Phase 2: Main Tray UI ✅
- [x] Complete MainTray UI implementation
- [x] Test tray activation/deactivation
- [x] Test glass overlay effects

### Phase 3: System Monitor ✅
- [x] Complete SystemMonitorDrawer UI
- [x] Implement real-time metrics updates (CPU/RAM/temperature)
- [x] Test server management controls
- [x] Verify auto-hide functionality
- Note: GPU metrics limited by WMI constraints (optional vendor SDKs available)

### Phase 4: Top Tray ✅
- [x] Implement TopTray UI
- [x] Add drag-and-drop functionality
- [x] Complete generated files view
- [x] Implement global knowledge log (GlobalLogDirectoryWindow)

### Phase 5: AI Services Integration ✅
- [x] Complete Ollama service integration
- [x] Implement AIContact management
- [x] Test model loading/unloading
- [x] Create AI persona forms
- [x] LLM parameter configuration

### Phase 6: SMS/MMS Communication 🚧
- [x] Complete SMS/MMS window
- [x] Implement contact management
- [x] Add message send/receive (text messages)
- [ ] Test media sharing (image/video/document attachments - pending)

### Phase 7: Project Management ✅
- [x] Complete Projects board UI
- [x] Implement project creation (CreateProjectDialog integrated)
- [x] Add project details window (ProjectDetailDialog with all tabs)
- [x] Test phase transitions
- [x] Filtering and sorting
- [x] Roadblocks and artifact management
- [x] AI collaboration logs

### Phase 8: Virtual Environment
- [ ] Complete Unreal Engine service
- [ ] Implement avatar spawning
- [ ] Add pose/movement controls
- [ ] Test scene management

## Key Design Patterns

- **MVVM Pattern**: ViewModels for all UI components
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection
- **Event Aggregator**: Loosely coupled communication between components
- **Repository Pattern**: Database abstraction layer
- **Observer Pattern**: Real-time updates for metrics and events

## Contributing

This is a modular project designed for incremental development. Each module should be:
1. Implemented independently
2. Tested thoroughly
3. Integrated with the existing system
4. Documented with inline comments

## License

This project is part of the House Victoria suite. All rights reserved.

## Hardware Monitoring

House Victoria uses **WMI (Windows Management Instrumentation)** for hardware monitoring, which:
- ✅ **No kernel drivers required** - Won't trigger Windows Defender
- ✅ **CPU/RAM monitoring** - Full support via Performance Counters
- ⚠️ **Limited temperature/fan support** - WMI has limitations (see documentation)

See [WINDOWS_DEFENDER_WINRING0.md](WINDOWS_DEFENDER_WINRING0.md) for detailed information about hardware monitoring capabilities.

## Support

For issues or questions, please refer to the module-specific documentation within each service and UI component.
