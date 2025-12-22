const { app, BrowserWindow, screen, ipcMain, Menu } = require('electron');
const path = require('path');
const axios = require('axios');

// Module configurations
const modules = [
    {
        name: 'Chat Module',
        url: 'http://localhost:8080',
        width: 400,
        height: 600,
        x: 50,
        y: 50,
        alwaysOnTop: true,
        frame: false,
        transparent: true,
        resizable: true,
        skipTaskbar: true
    },
    {
        name: 'Contacts Module', 
        url: 'http://localhost:8081',
        width: 400,
        height: 600,
        x: 500,
        y: 50,
        alwaysOnTop: true,
        frame: false,
        transparent: true,
        resizable: true,
        skipTaskbar: true
    },
    {
        name: 'ViewPort Module',
        url: 'http://localhost:8082', 
        width: 400,
        height: 600,
        x: 950,
        y: 50,
        alwaysOnTop: true,
        frame: false,
        transparent: true,
        resizable: true,
        skipTaskbar: true
    },
    {
        name: 'Systems Module',
        url: 'http://localhost:8083',
        width: 400,
        height: 600,
        x: 50,
        y: 700,
        alwaysOnTop: true,
        frame: false,
        transparent: true,
        resizable: true,
        skipTaskbar: true
    },
    {
        name: 'Context Data Exchange',
        url: 'http://localhost:8084',
        width: 400,
        height: 600,
        x: 500,
        y: 700,
        alwaysOnTop: true,
        frame: false,
        transparent: true,
        resizable: true,
        skipTaskbar: true
    },
    {
        name: 'App Tray',
        url: 'http://localhost:8085',
        width: 400,
        height: 600,
        x: 950,
        y: 700,
        alwaysOnTop: true,
        frame: false,
        transparent: true,
        resizable: true,
        skipTaskbar: true
    }
];

let overlayWindows = [];
let isInitialized = false;

function createOverlayWindow(moduleConfig) {
    const primaryDisplay = screen.getPrimaryDisplay();
    const { width: screenWidth, height: screenHeight } = primaryDisplay.workAreaSize;
    
    // Ensure window stays within screen bounds
    const x = Math.max(0, Math.min(moduleConfig.x, screenWidth - moduleConfig.width));
    const y = Math.max(0, Math.min(moduleConfig.y, screenHeight - moduleConfig.height));

    const window = new BrowserWindow({
        width: moduleConfig.width,
        height: moduleConfig.height,
        x: x,
        y: y,
        webPreferences: {
            nodeIntegration: false,
            contextIsolation: true,
            enableRemoteModule: false,
            webSecurity: true
        },
        ...moduleConfig,
        titleBarStyle: 'hiddenInset',
        vibrancy: 'under-window', // macOS blur effect
        visualEffectState: 'active', // macOS visual effects
        backgroundMaterial: 'mica', // Windows 11 blur effect
        type: 'toolbar' // Linux dock type
    });

    // Make window click-through when not in focus
    window.setIgnoreMouseEvents(false);
    
    // Load the module URL
    window.loadURL(moduleConfig.url);

    // Handle window close
    window.on('closed', () => {
        const index = overlayWindows.findIndex(w => w.window === window);
        if (index > -1) {
            overlayWindows.splice(index, 1);
        }
    });

    // Handle navigation errors
    window.webContents.on('did-fail-load', (event, errorCode, errorDescription) => {
        console.error(`Failed to load ${moduleConfig.name}: ${errorDescription}`);
        
        // Load a simple error page
        window.loadURL(`data:text/html;charset=utf-8,
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body { 
                        margin: 0; 
                        padding: 20px; 
                        font-family: Arial, sans-serif;
                        background: linear-gradient(135deg, #2c3e50, #1a2332);
                        color: #e8e8e8;
                        height: 100vh;
                        display: flex;
                        align-items: center;
                        justify-content: center;
                        text-align: center;
                    }
                    .error-container {
                        background: rgba(26, 35, 50, 0.8);
                        border: 2px solid #b7410e;
                        border-radius: 12px;
                        padding: 30px;
                        backdrop-filter: blur(10px);
                    }
                    h1 { color: #b7410e; margin-bottom: 15px; }
                    .retry-btn {
                        background: linear-gradient(135deg, #b7410e, #8b2f0a);
                        color: white;
                        border: none;
                        padding: 10px 20px;
                        border-radius: 6px;
                        cursor: pointer;
                        margin-top: 15px;
                    }
                    .retry-btn:hover {
                        background: linear-gradient(135deg, #d2691e, #b7410e);
                    }
                </style>
            </head>
            <body>
                <div class="error-container">
                    <h1>Connection Error</h1>
                    <p>Could not connect to ${moduleConfig.name}</p>
                    <p>Make sure the server is running on ${moduleConfig.url}</p>
                    <button class="retry-btn" onclick="location.reload()">Retry</button>
                </div>
            </body>
            </html>
        `);
    });

    // Inject overlay control script
    window.webContents.on('did-finish-load', () => {
        window.webContents.executeJavaScript(`
            // Add window controls to the page
            if (!document.getElementById('overlay-controls')) {
                const controls = document.createElement('div');
                controls.id = 'overlay-controls';
                controls.style.cssText = \`
                    position: fixed;
                    top: 5px;
                    right: 5px;
                    z-index: 10000;
                    display: flex;
                    gap: 5px;
                    background: rgba(26, 35, 50, 0.8);
                    border-radius: 8px;
                    padding: 5px;
                    backdrop-filter: blur(10px);
                \`;
                
                // Minimize button
                const minimizeBtn = document.createElement('button');
                minimizeBtn.innerHTML = '－';
                minimizeBtn.style.cssText = \`
                    width: 20px;
                    height: 20px;
                    border: none;
                    border-radius: 4px;
                    background: rgba(255, 193, 7, 0.8);
                    color: white;
                    cursor: pointer;
                    font-size: 12px;
                \`;
                minimizeBtn.onclick = () => {
                    window.electronAPI.minimize();
                };
                
                // Close button
                const closeBtn = document.createElement('button');
                closeBtn.innerHTML = '×';
                closeBtn.style.cssText = \`
                    width: 20px;
                    height: 20px;
                    border: none;
                    border-radius: 4px;
                    background: rgba(220, 53, 69, 0.8);
                    color: white;
                    cursor: pointer;
                    font-size: 12px;
                \`;
                closeBtn.onclick = () => {
                    window.electronAPI.close();
                };
                
                controls.appendChild(minimizeBtn);
                controls.appendChild(closeBtn);
                document.body.appendChild(controls);
            }
            
            // Make window draggable from header
            const headers = document.querySelectorAll('.module-header, h1, .header');
            headers.forEach(header => {
                header.style.cursor = 'move';
                header.addEventListener('mousedown', (e) => {
                    window.electronAPI.startDrag(e.clientX, e.clientY);
                });
            });
        `);
    });

    return window;
}

async function checkModuleStatus(moduleConfig) {
    try {
        const response = await axios.get(moduleConfig.url + '/health', { timeout: 2000 });
        return { status: 'online', ...moduleConfig };
    } catch (error) {
        return { status: 'offline', ...moduleConfig };
    }
}

async function initializeOverlays() {
    console.log('Initializing LLMOD Desktop Overlay...');
    
    // Check status of all modules
    const moduleStatus = await Promise.all(modules.map(checkModuleStatus));
    
    console.log('Module Status:');
    moduleStatus.forEach(module => {
        console.log(`  ${module.status === 'online' ? '[ONLINE]' : '[OFFLINE]'} ${module.name}: ${module.status}`);
    });
    
    // Create overlay windows for running modules
    moduleStatus.forEach(module => {
        if (module.status === 'online' || true) { // Create windows even if offline for retry capability
            const window = createOverlayWindow(module);
            overlayWindows.push({ window, config: module });
        }
    });
    
    isInitialized = true;
    console.log(`Created ${overlayWindows.length} overlay windows`);
}

// IPC handlers
ipcMain.handle('minimize-window', (event) => {
    const window = BrowserWindow.fromWebContents(event.sender);
    if (window) {
        window.minimize();
    }
});

ipcMain.handle('close-window', (event) => {
    const window = BrowserWindow.fromWebContents(event.sender);
    if (window) {
        window.close();
    }
});

ipcMain.handle('start-drag', (event, x, y) => {
    const window = BrowserWindow.fromWebContents(event.sender);
    if (window) {
        // Store initial position for drag calculation
        window.dragOffset = { x, y };
        window.startDrag();
    }
});

// Create application menu
function createMenu() {
    const template = [
        {
            label: 'LLMOD Overlay',
            submenu: [
                {
                    label: 'Show All Modules',
                    accelerator: 'CmdOrCtrl+Shift+A',
                    click: () => {
                        overlayWindows.forEach(({ window }) => {
                            window.show();
                            window.focus();
                        });
                    }
                },
                {
                    label: 'Hide All Modules', 
                    accelerator: 'CmdOrCtrl+Shift+H',
                    click: () => {
                        overlayWindows.forEach(({ window }) => {
                            window.hide();
                        });
                    }
                },
                { type: 'separator' },
                {
                    label: 'Reload All',
                    accelerator: 'CmdOrCtrl+R',
                    click: () => {
                        overlayWindows.forEach(({ window }) => {
                            window.reload();
                        });
                    }
                },
                {
                    label: 'Restart Overlay',
                    accelerator: 'CmdOrCtrl+Shift+R',
                    click: () => {
                        overlayWindows.forEach(({ window }) => {
                            window.close();
                        });
                        overlayWindows = [];
                        setTimeout(initializeOverlays, 1000);
                    }
                },
                { type: 'separator' },
                {
                    label: 'Quit',
                    accelerator: process.platform === 'darwin' ? 'Cmd+Q' : 'Ctrl+Q',
                    click: () => {
                        app.quit();
                    }
                }
            ]
        },
        {
            label: 'View',
            submenu: [
                { role: 'reload' },
                { role: 'forceReload' },
                { role: 'toggleDevTools' },
                { type: 'separator' },
                { role: 'resetZoom' },
                { role: 'zoomIn' },
                { role: 'zoomOut' },
                { type: 'separator' },
                { role: 'togglefullscreen' }
            ]
        }
    ];

    const menu = Menu.buildFromTemplate(template);
    Menu.setApplicationMenu(menu);
}

// App event handlers
app.whenReady().then(() => {
    createMenu();
    
    // Delay initialization to allow servers to start
    setTimeout(initializeOverlays, 3000);
});

app.on('window-all-closed', () => {
    if (process.platform !== 'darwin') {
        app.quit();
    }
});

app.on('activate', () => {
    if (!isInitialized) {
        initializeOverlays();
    }
});

// Handle window dragging
let draggedWindow = null;
let dragStartPos = null;

app.on('browser-window-created', (event, window) => {
    window.on('will-move', (event, newBounds) => {
        if (draggedWindow === window) {
            event.preventDefault();
        }
    });
    
    window.webContents.on('will-navigate', (event, url) => {
        // Prevent navigation away from module URLs
        if (!url.startsWith('http://localhost:')) {
            event.preventDefault();
        }
    });
});