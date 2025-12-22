# LLMOD Complete Startup Guide

## 🎯 Current Status

✅ **Desktop Overlay Working!** - You successfully created 6 floating windows!

The connection errors you're seeing are **expected and normal** because the backend module servers aren't running yet.

## 🚀 Complete Startup Process

### Step 1: Pull Latest Fixes
```bash
cd "C:\Users\kurtw\source\repos\Linearthrone\LLMOD"
git pull origin master
```

### Step 2: Start Backend Module Servers

Open a **new terminal** and run:
```bash
cd "C:\Users\kurtw\source\repos\Linearthrone\LLMOD"
npm start
```

This will start all 6 backend servers on ports 8080-8085.

**Wait for this output:**
```
Starting Chat Module on port 8080...
Starting Contacts Module on port 8081...
Starting ViewPort Module on port 8082...
Starting Systems Module on port 8083...
Starting Context Data Exchange Module on port 8084...
Starting App Tray Module on port 8085...
```

### Step 3: Start Desktop Overlay

Open a **second terminal** and run:
```bash
cd "C:\Users\kurtw\source\repos\Linearthrone\LLMOD\desktop-overlay"
npm start
```

Now you should see:
```
Initializing LLMOD Desktop Overlay...
Module Status:
  [ONLINE] Chat Module: online
  [ONLINE] Contacts Module: online
  [ONLINE] ViewPort Module: online
  [ONLINE] Systems Module: online
  [ONLINE] Context Data Exchange: online
  [ONLINE] App Tray: online
Created 6 overlay windows
```

### Step 4: Enjoy Your Floating Desktop Panels! 🎉

You should now see 6 beautiful floating panels with:
- ✅ Navy and rust metal styling
- ✅ Glass morphism effects
- ✅ Draggable windows
- ✅ Minimize/close buttons
- ✅ Real-time module content

## 🎮 Using the Overlay

### Window Controls
Each floating panel has controls in the top-right corner:
- **Yellow button (－)**: Minimize the panel
- **Red button (×)**: Close the panel

### Keyboard Shortcuts
- **Ctrl+Shift+A**: Show all panels
- **Ctrl+Shift+H**: Hide all panels
- **Ctrl+R**: Reload all panels
- **Ctrl+Shift+R**: Restart entire overlay
- **Ctrl+Q**: Quit overlay

### Dragging Windows
Click and hold on any panel's header to drag it around your screen.

## 📊 Module Overview

| Module | Port | Description |
|--------|------|-------------|
| **Chat Module** | 8080 | AI chat with Ollama integration |
| **Contacts Module** | 8081 | Contact and avatar management |
| **ViewPort Module** | 8082 | Image and avatar display |
| **Systems Module** | 8083 | Real-time system monitoring |
| **Context Data Exchange** | 8084 | File and data management |
| **App Tray** | 8085 | Main control panel |

## 🔧 Troubleshooting

### If You See Connection Errors
This means the backend servers aren't running. Start them with:
```bash
npm start
```

### If Panels Don't Appear
1. Check if Electron is running (you should see it in Task Manager)
2. Try restarting the overlay: Ctrl+Shift+R
3. Check console for errors

### If Panels Appear But Are Empty
1. Make sure backend servers are running
2. Check that ports 8080-8085 aren't blocked by firewall
3. Try reloading panels: Ctrl+R

### If You Get Script Errors
Pull the latest fixes:
```bash
git pull origin master
```

## 🎨 What You Should See

When everything is working, you'll have:

1. **6 Floating Panels** positioned on your desktop
2. **Navy and Rust Metal Theme** with industrial styling
3. **Glass Morphism Effects** with transparent backgrounds
4. **Animated Elements** with shimmer and glow effects
5. **Interactive Controls** on each panel
6. **Real-time Updates** from all modules

## 📝 Quick Start Commands

**Terminal 1 (Backend Servers):**
```bash
cd "C:\Users\kurtw\source\repos\Linearthrone\LLMOD"
npm start
```

**Terminal 2 (Desktop Overlay):**
```bash
cd "C:\Users\kurtw\source\repos\Linearthrone\LLMOD\desktop-overlay"
npm start
```

## ✨ Success Indicators

You'll know everything is working when:
- ✅ No error messages in console
- ✅ 6 floating panels visible on desktop
- ✅ Panels show actual module content (not connection errors)
- ✅ Panels can be dragged and minimized
- ✅ Module content updates in real-time

## 🎯 Next Steps

Once everything is running:
1. Try the Chat Module with Ollama
2. Add contacts in the Contacts Module
3. Monitor your system in the Systems Module
4. Upload files in the Context Data Exchange
5. Control everything from the App Tray

Enjoy your true desktop overlay experience! 🚀