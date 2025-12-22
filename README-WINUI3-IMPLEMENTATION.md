# LLMOD - WinUI 3 Overlay Implementation Guide

## Architecture Overview

LLMOD now uses a **pure JavaScript module + WinUI 3 overlay** architecture:

### Backend (Node.js)
- **Pure JavaScript modules** - No HTML, no web servers, just logic
- **WebSocket server** - Broadcasts module events to overlay
- **Event-driven** - Modules communicate via events

### Frontend (WinUI 3)
- **Native Windows overlay** - True desktop integration
- **Acrylic/Mica materials** - Frosted glass effects
- **WebSocket client** - Receives module data in real-time
- **Navy & Rust theme** - Industrial design in XAML

## Current Implementation Status

### ✅ Completed
- Pure JavaScript ChatModule with event emitter
- ChatState for persistent state management
- WebSocket server for module-overlay communication
- Module launcher script
- Core infrastructure (logger, config, bus)

### 🚧 To Be Implemented
1. **Remaining JavaScript Modules**
   - ContactsModule
   - ViewPortModule
   - SystemsModule
   - ContextDataExchangeModule
   - AppTrayModule

2. **WinUI 3 Overlay Application**
   - C# project setup
   - XAML layouts
   - WebSocket client
   - Navy & Rust theme
   - Module views

## Quick Start (JavaScript Modules Only)

### 1. Install Dependencies
```bash
npm install
```

### 2. Start Module Server
```bash
node start-modules-pure.js
```

You should see:
```
[INFO] Starting LLMOD Module Server...
[INFO] WebSocket Server started on port 9001
[INFO] ✓ Chat Module initialized
[INFO] LLMOD Module Server Started!
[INFO] WebSocket Server: ws://localhost:9001
[INFO] Waiting for WinUI 3 overlay to connect...
```

### 3. Test WebSocket Connection
You can test the WebSocket server using a simple client:

```javascript
const WebSocket = require('ws');
const ws = new WebSocket('ws://localhost:9001');

ws.on('open', () => {
    console.log('Connected to LLMOD');
    
    // Send a chat message
    ws.send(JSON.stringify({
        type: 'module:command',
        module: 'ChatModule',
        command: 'sendMessage',
        args: ['Hello from test client!']
    }));
});

ws.on('message', (data) => {
    console.log('Received:', JSON.parse(data));
});
```

## Building the WinUI 3 Overlay

### Prerequisites
- Visual Studio 2022
- Windows App SDK
- .NET 6.0 or later

### Step 1: Create WinUI 3 Project

1. Open Visual Studio 2022
2. Create new project → "Blank App, Packaged (WinUI 3 in Desktop)"
3. Name it "LLMODOverlay"
4. Target Windows 10, version 1809 or later

### Step 2: Install NuGet Packages

```powershell
Install-Package System.Net.WebSockets.Client
Install-Package Newtonsoft.Json
```

### Step 3: Create WebSocket Client Service

**Services/ModuleWebSocketClient.cs:**
```csharp
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace LLMODOverlay.Services
{
    public class ModuleWebSocketClient
    {
        private ClientWebSocket _ws;
        private readonly Uri _serverUri = new Uri("ws://localhost:9001");
        
        public event EventHandler<ModuleEventArgs> ModuleEventReceived;
        
        public async Task ConnectAsync()
        {
            _ws = new ClientWebSocket();
            await _ws.ConnectAsync(_serverUri, CancellationToken.None);
            _ = ReceiveLoop();
        }
        
        private async Task ReceiveLoop()
        {
            var buffer = new byte[4096];
            while (_ws.State == WebSocketState.Open)
            {
                var result = await _ws.ReceiveAsync(
                    new ArraySegment<byte>(buffer), 
                    CancellationToken.None
                );
                
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var data = JsonConvert.DeserializeObject<dynamic>(message);
                    ModuleEventReceived?.Invoke(this, new ModuleEventArgs(data));
                }
            }
        }
        
        public async Task SendCommandAsync(string module, string command, params object[] args)
        {
            var message = new
            {
                type = "module:command",
                module = module,
                command = command,
                args = args
            };
            
            var json = JsonConvert.SerializeObject(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _ws.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None
            );
        }
    }
    
    public class ModuleEventArgs : EventArgs
    {
        public dynamic Data { get; }
        public ModuleEventArgs(dynamic data) => Data = data;
    }
}
```

### Step 4: Create Navy & Rust Theme

**Styles/NavyRustTheme.xaml:**
```xml
<ResourceDictionary
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    
    <!-- Colors -->
    <Color x:Key="NavyDark">#1a2332</Color>
    <Color x:Key="NavyMedium">#2c3e50</Color>
    <Color x:Key="RustPrimary">#b7410e</Color>
    <Color x:Key="RustLight">#d2691e</Color>
    <Color x:Key="TextPrimary">#e8e8e8</Color>
    
    <!-- Brushes -->
    <SolidColorBrush x:Key="NavyDarkBrush" Color="{StaticResource NavyDark}"/>
    <SolidColorBrush x:Key="NavyMediumBrush" Color="{StaticResource NavyMedium}"/>
    <SolidColorBrush x:Key="RustPrimaryBrush" Color="{StaticResource RustPrimary}"/>
    <SolidColorBrush x:Key="RustLightBrush" Color="{StaticResource RustLight}"/>
    <SolidColorBrush x:Key="TextPrimaryBrush" Color="{StaticResource TextPrimary}"/>
    
    <!-- Acrylic Brush -->
    <AcrylicBrush x:Key="NavyAcrylicBrush"
                  TintColor="{StaticResource NavyDark}"
                  TintOpacity="0.8"
                  FallbackColor="{StaticResource NavyMedium}"/>
</ResourceDictionary>
```

### Step 5: Create Main Overlay Window

**MainWindow.xaml:**
```xml
<Window
    x:Class="LLMODOverlay.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    Title="LLMOD Overlay"
    Width="400"
    Height="600">
    
    <Window.SystemBackdrop>
        <MicaBackdrop Kind="BaseAlt"/>
    </Window.SystemBackdrop>
    
    <Grid Background="{StaticResource NavyAcrylicBrush}">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="*"/>
        </Grid.RowDefinitions>
        
        <!-- Header -->
        <Border Grid.Row="0" 
                Background="{StaticResource NavyMediumBrush}"
                BorderBrush="{StaticResource RustPrimaryBrush}"
                BorderThickness="0,0,0,2"
                Padding="16">
            <TextBlock Text="LLMOD Chat" 
                       Foreground="{StaticResource TextPrimaryBrush}"
                       FontSize="18"
                       FontWeight="SemiBold"/>
        </Border>
        
        <!-- Content -->
        <ScrollViewer Grid.Row="1" Padding="16">
            <ItemsControl x:Name="MessagesPanel">
                <ItemsControl.ItemTemplate>
                    <DataTemplate>
                        <Border Background="{StaticResource RustPrimaryBrush}"
                                CornerRadius="8"
                                Padding="12"
                                Margin="0,0,0,8">
                            <TextBlock Text="{Binding Text}"
                                       Foreground="{StaticResource TextPrimaryBrush}"
                                       TextWrapping="Wrap"/>
                        </Border>
                    </DataTemplate>
                </ItemsControl.ItemTemplate>
            </ItemsControl>
        </ScrollViewer>
    </Grid>
</Window>
```

### Step 6: Connect to Modules

**MainWindow.xaml.cs:**
```csharp
using LLMODOverlay.Services;
using Microsoft.UI.Xaml;
using System.Collections.ObjectModel;

namespace LLMODOverlay
{
    public sealed partial class MainWindow : Window
    {
        private ModuleWebSocketClient _wsClient;
        private ObservableCollection<ChatMessage> _messages;
        
        public MainWindow()
        {
            this.InitializeComponent();
            InitializeAsync();
        }
        
        private async void InitializeAsync()
        {
            _messages = new ObservableCollection<ChatMessage>();
            MessagesPanel.ItemsSource = _messages;
            
            _wsClient = new ModuleWebSocketClient();
            _wsClient.ModuleEventReceived += OnModuleEvent;
            
            await _wsClient.ConnectAsync();
        }
        
        private void OnModuleEvent(object sender, ModuleEventArgs e)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (e.Data.type == "chat:message" || e.Data.type == "chat:response")
                {
                    _messages.Add(new ChatMessage
                    {
                        Text = e.Data.data.text,
                        User = e.Data.data.user,
                        Timestamp = e.Data.data.timestamp
                    });
                }
            });
        }
    }
    
    public class ChatMessage
    {
        public string Text { get; set; }
        public string User { get; set; }
        public string Timestamp { get; set; }
    }
}
```

## Running the Complete System

### Terminal 1: Start JavaScript Modules
```bash
node start-modules-pure.js
```

### Terminal 2: Start WinUI 3 Overlay
```bash
# In Visual Studio
F5 (Debug) or Ctrl+F5 (Run without debugging)
```

The overlay will connect to the WebSocket server and display real-time module data!

## Next Steps

1. **Implement remaining modules** (Contacts, ViewPort, Systems, etc.)
2. **Complete WinUI 3 overlay** with all module views
3. **Add drag/resize** functionality to overlay
4. **Implement system tray** integration
5. **Add auto-hide** and always-on-top features
6. **Polish navy & rust theme** with animations

## Benefits of This Architecture

✅ **True native performance** - WinUI 3 is compiled, not interpreted  
✅ **Proper Windows integration** - Acrylic, Mica, system tray  
✅ **Separation of concerns** - Logic in JS, UI in C#/XAML  
✅ **Real-time updates** - WebSocket push notifications  
✅ **Scalable** - Easy to add new modules  
✅ **No HTML/CSS** - Pure native Windows UI  

This is the proper UWP/WinUI 3 overlay pattern! 🚀