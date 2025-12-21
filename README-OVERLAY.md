# LLMOD Desktop Overlay - True Floating Desktop Experience

## 🌟 Overview

LLMOD Desktop Overlay transforms your web modules into **true floating desktop panels** that hover over your desktop, not in browser windows. Experience the navy and rust metal industrial aesthetic with real desktop overlay functionality.

## ✨ Key Features

### 🪟 True Desktop Overlay
- **Floating Panels**: Modules appear as floating windows on your desktop
- **Always on Top**: Keep your modules visible while working
- **Transparent Background**: See your desktop through the panels
- **Frameless Design**: Clean, modern floating window appearance
- **Draggable Windows**: Move panels anywhere on your screen

### 🎨 Industrial Design
- **Navy & Rust Metal Theme**: Professional industrial aesthetic
- **Glass Morphism**: Frosted glass effect with backdrop blur
- **Animated Elements**: Shimmer effects and smooth transitions
- **Metallic Textures**: Realistic metal appearance with gradients

### ⚡ Advanced Functionality
- **Real-time Updates**: All modules update in real-time
- **Hotkey Controls**: Keyboard shortcuts for window management
- **Auto-recovery**: Automatic retry if modules disconnect
- **Multi-monitor Support**: Works across all your displays

## 🚀 Quick Start

### Prerequisites
- ✅ Node.js v25+ 
- ✅ npm 11+
- ✅ Python 3.14+
- ✅ Ollama (running)

### Installation (One-Click)

```bash
# Run the automated installer
install-overlay.bat
```

This will:
1. Install Electron dependencies
2. Install module dependencies  
3. Start all module servers
4. Launch the desktop overlay

### Manual Installation

```bash
# 1. Install overlay dependencies
cd desktop-overlay
npm install

# 2. Install module dependencies
cd ..
npm install

# 3. Install Python MCP servers
pip install -e "Central Core/MainAccessToMcpServer"

# 4. Start modules and overlay
npm start
cd desktop-overlay
npm start
```

## 🎮 Usage & Controls

### Keyboard Shortcuts
- **Ctrl+Shift+A**: Show all floating panels
- **Ctrl+Shift+H**: Hide all floating panels  
- **Ctrl+R**: Reload all modules
- **Ctrl+Shift+R**: Restart entire overlay
- **Ctrl+Q**: Quit overlay

### Window Controls
- **Drag**: Click and hold headers to move panels
- **Minimize**: Click yellow button (－) to minimize panels
- **Close**: Click red button (×) to close individual panels
- **Resize**: Drag edges to resize (where enabled)

### Module Access
Once running, you'll see 6 floating panels:

| Panel | Port | Description |
|-------|------|-------------|
| **🔧 App Tray** | 8085 | Main control panel - manage all modules |
| **💬 Chat Module** | 8080 | AI chat with Ollama integration |
| **👥 Contacts** | 8081 | Contact and avatar management |
| **👁️ ViewPort** | 8082 | Image and avatar display |
| **⚙️ Systems** | 8083 | Real-time system monitoring |
| **📁 Context Data** | 8084 | File and data exchange |

## 🔧 Configuration

### Custom Panel Positions
Edit `desktop-overlay/main.js` to change window positions:

```javascript
{
    name: 'Chat Module',
    url: 'http://localhost:8080',
    width: 400,  // Panel width
    height: 600, // Panel height
    x: 50,       // X position
    y: 50        // Y position
}
```

### Appearance Customization
Modify `desktop-overlay/overlay-enhancements.css` to adjust:
- Transparency levels
- Colors and gradients
- Animation speeds
- Border effects

### Module URLs
Each module runs on `http://localhost:[PORT]` where:
- 8080: Chat Module
- 8081: Contacts Module
- 8082: ViewPort Module  
- 8083: Systems Module
- 8084: Context Data Exchange
- 8085: App Tray (Control Panel)

## 🐛 Troubleshooting

### Connection Issues
If you see "Connection Refused":

1. **Check if modules are running**:
   ```bash
   netstat -an | findstr :808
   ```

2. **Restart modules manually**:
   ```bash
   npm start
   ```

3. **Check Ollama status**:
   ```bash
   ollama list
   ```

### Overlay Issues
If floating panels don't appear:

1. **Install Electron dependencies**:
   ```bash
   cd desktop-overlay
   npm install
   ```

2. **Check console for errors** (Ctrl+Shift+I in overlay)

3. **Restart overlay**:
   ```bash
   cd desktop-overlay
   npm start
   ```

### Performance Issues
If overlay is slow:

1. **Close unnecessary browser tabs**
2. **Restart overlay**: Ctrl+Shift+R
3. **Check system resources** in Systems Module

## 🎨 Design System

### Color Palette
- **Navy Dark**: `#1a2332` - Deep background
- **Navy Medium**: `#2c3e50` - Panels and headers  
- **Rust Primary**: `#b7410e` - Accent and buttons
- **Rust Light**: `#d2691e` - Hover states
- **Metal Silver**: `#708090` - Borders and accents

### Effects
- **Backdrop Blur**: 20px Gaussian blur
- **Glass Transparency**: 85% opacity
- **Metal Gradients**: 135-degree metallic shine
- **Animation Timing**: 0.3s ease transitions

## 🔄 Development

### Adding New Modules
1. Create module in `Modules/YourModule/`
2. Add to `start-all.js` module list
3. Add to `desktop-overlay/main.js` overlay config
4. Update documentation

### Custom Styling
- Edit `shared-styles.css` for base styles
- Edit `overlay-enhancements.css` for overlay-specific styles
- Module-specific styles in `Modules/Module/client/`

### Build Executable
```bash
cd desktop-overlay
npm run build
```

Creates distributable `.exe`, `.dmg`, or `.AppImage` files.

## 📄 License

MIT License - See LICENSE file for details.

## 🤝 Contributing

1. Fork the repository
2. Create feature branch
3. Commit your changes  
4. Push to branch
5. Create Pull Request

---

**Enjoy your true desktop overlay experience! 🚀**

Transform your desktop with floating LLMOD panels that work seamlessly with your workflow.