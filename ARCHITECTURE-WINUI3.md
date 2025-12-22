# LLMOD - WinUI 3 Desktop Overlay Architecture

## Overview
LLMOD uses a **UWP/WinUI 3 overlay pattern** where:
- Pure JavaScript modules run as background Node.js processes
- A native WinUI 3 overlay displays module data on the desktop
- Modules communicate via WebSocket/IPC with the overlay
- True Windows desktop integration with Acrylic/Mica materials

## Architecture Components

### 1. JavaScript Modules (Background Services)
Each module is a pure Node.js service that:
- Runs independently as a background process
- Exposes data via WebSocket or IPC
- Has NO UI of its own
- Communicates with the overlay via events

**Example Module Structure:**
```javascript
// Modules/ChatModule/chatindex.js
class ChatModule {
    constructor(bus) {
        this.bus = bus;
        this.messages = [];
    }
    
    sendMessage(message) {
        this.messages.push(message);
        this.bus.emit('chat:message', message);
    }
    
    getMessages() {
        return this.messages;
    }
}

module.exports = ChatModule;
```

### 2. WinUI 3 Desktop Overlay (C#/XAML)
A native Windows application that:
- Displays as a transparent overlay on the desktop
- Uses Acrylic/Mica materials for blur effects
- Connects to JavaScript modules via WebSocket
- Shows real-time data from all modules

**Technology Stack:**
- WinUI 3 (Windows App SDK)
- C# for backend logic
- XAML for UI layout
- WebSocket client for module communication

### 3. Communication Layer
Modules and overlay communicate via:
- **WebSocket Server** (in Node.js) - Broadcasts module events
- **WebSocket Client** (in WinUI 3) - Receives and displays data
- **Event Bus** - Coordinates inter-module communication

## Module Types

### Chat Module
- Background service managing chat state
- Integrates with Ollama for AI responses
- Emits events: `chat:message`, `chat:response`

### Contacts Module  
- Manages contact data in memory/database
- Emits events: `contacts:added`, `contacts:updated`

### ViewPort Module
- Handles avatar/image data
- Emits events: `viewport:image`, `viewport:avatar`

### Systems Module
- Monitors system resources
- Emits events: `system:stats`, `system:alert`

### Context Data Exchange
- Manages file operations
- Emits events: `data:uploaded`, `data:processed`

### App Tray Module
- Coordinates other modules
- Provides system tray integration

## WinUI 3 Overlay Features

### Visual Design
- **Acrylic Background** - Frosted glass effect
- **Mica Material** - Dynamic backdrop blur
- **Navy & Rust Theme** - Industrial color scheme
- **Floating Panels** - Draggable, resizable windows

### Layout
- Multiple overlay panels (one per module)
- Dockable to screen edges
- Always-on-top mode
- Auto-hide when not in use

### Interactions
- Click-through when inactive
- Drag to reposition
- Resize handles
- Minimize to system tray

## Implementation Plan

### Phase 1: Pure JavaScript Modules
1. Implement each module as a pure JS class
2. Add WebSocket server for communication
3. Create event bus for inter-module messaging
4. Test modules independently

### Phase 2: WinUI 3 Overlay
1. Create WinUI 3 project
2. Implement WebSocket client
3. Design XAML layouts for each module view
4. Add Acrylic/Mica materials
5. Implement drag/resize functionality

### Phase 3: Integration
1. Connect overlay to module WebSocket server
2. Map module events to UI updates
3. Add bidirectional communication
4. Test full system integration

## File Structure

```
LLMOD/
├── Modules/                    # Pure JavaScript modules
│   ├── ChatModule/
│   │   ├── chatindex.js       # Main module class
│   │   └── chatstate.js       # State management
│   ├── ContactsModule/
│   ├── ViewPortModule/
│   ├── SystemsModule/
│   ├── ContextDataExchangeModule/
│   └── AppTray/
├── Central Core/
│   ├── bus.js                 # Event bus
│   ├── logger.js              # Logging
│   ├── config.js              # Configuration
│   └── websocket-server.js    # WebSocket server
├── WinUI3Overlay/             # WinUI 3 desktop overlay
│   ├── App.xaml               # Application entry
│   ├── MainWindow.xaml        # Main overlay window
│   ├── Views/                 # Module-specific views
│   │   ├── ChatView.xaml
│   │   ├── ContactsView.xaml
│   │   └── ...
│   ├── Services/
│   │   └── WebSocketClient.cs # Connects to modules
│   └── Styles/
│       └── NavyRustTheme.xaml # Theme resources
└── start-modules.js           # Starts all JS modules
```

## Benefits of This Architecture

1. **Native Performance** - WinUI 3 is native Windows, faster than Electron
2. **True Overlay** - Proper desktop integration with Windows compositor
3. **Separation of Concerns** - Modules are pure logic, UI is separate
4. **Scalability** - Easy to add new modules
5. **Windows Integration** - System tray, notifications, Acrylic materials

## Next Steps

1. Refactor existing modules to pure JavaScript classes
2. Create WebSocket server for module communication
3. Build WinUI 3 overlay application
4. Implement navy & rust metal theme in XAML
5. Connect overlay to modules via WebSocket

This architecture provides true Windows desktop overlay functionality with native performance and proper separation between business logic (JS modules) and presentation (WinUI 3 overlay).