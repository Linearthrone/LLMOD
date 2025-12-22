const WebSocket = require('ws');
const EventEmitter = require('events');

/**
 * WebSocketServer - Communication bridge between JS modules and WinUI 3 overlay
 * Broadcasts module events to connected overlay clients
 */
class ModuleWebSocketServer extends EventEmitter {
    constructor(port = 9001) {
        super();
        this.port = port;
        this.wss = null;
        this.clients = new Set();
        this.modules = new Map();
    }

    /**
     * Start the WebSocket server
     */
    start() {
        this.wss = new WebSocket.Server({ port: this.port });

        this.wss.on('connection', (ws) => {
            console.log('[WebSocket] Client connected');
            this.clients.add(ws);

            // Send current module states to new client
            this.sendModuleStates(ws);

            ws.on('message', (message) => {
                this.handleMessage(ws, message);
            });

            ws.on('close', () => {
                console.log('[WebSocket] Client disconnected');
                this.clients.delete(ws);
            });

            ws.on('error', (error) => {
                console.error('[WebSocket] Client error:', error);
                this.clients.delete(ws);
            });
        });

        console.log(`[WebSocket] Server started on port ${this.port}`);
        this.emit('server:started', { port: this.port });
    }

    /**
     * Register a module with the server
     */
    registerModule(name, module) {
        this.modules.set(name, module);
        console.log(`[WebSocket] Module registered: ${name}`);

        // Forward all module events to connected clients
        module.on('*', (event, data) => {
            this.broadcast({
                type: 'module:event',
                module: name,
                event: event,
                data: data,
                timestamp: new Date().toISOString()
            });
        });

        // Broadcast module registration
        this.broadcast({
            type: 'module:registered',
            module: name,
            state: module.getState ? module.getState() : {},
            timestamp: new Date().toISOString()
        });
    }

    /**
     * Handle incoming messages from overlay
     */
    handleMessage(ws, message) {
        try {
            const data = JSON.parse(message);
            console.log('[WebSocket] Received:', data);

            switch (data.type) {
                case 'module:command':
                    this.handleModuleCommand(data);
                    break;
                case 'module:query':
                    this.handleModuleQuery(ws, data);
                    break;
                case 'ping':
                    ws.send(JSON.stringify({ type: 'pong', timestamp: Date.now() }));
                    break;
                default:
                    console.warn('[WebSocket] Unknown message type:', data.type);
            }
        } catch (error) {
            console.error('[WebSocket] Error handling message:', error);
        }
    }

    /**
     * Handle command sent to a module
     */
    handleModuleCommand(data) {
        const module = this.modules.get(data.module);
        if (module && typeof module[data.command] === 'function') {
            try {
                const result = module[data.command](...(data.args || []));
                this.broadcast({
                    type: 'module:command:result',
                    module: data.module,
                    command: data.command,
                    result: result,
                    timestamp: new Date().toISOString()
                });
            } catch (error) {
                console.error(`[WebSocket] Error executing command ${data.command} on ${data.module}:`, error);
                this.broadcast({
                    type: 'module:command:error',
                    module: data.module,
                    command: data.command,
                    error: error.message,
                    timestamp: new Date().toISOString()
                });
            }
        }
    }

    /**
     * Handle query for module state
     */
    handleModuleQuery(ws, data) {
        const module = this.modules.get(data.module);
        if (module) {
            const state = module.getState ? module.getState() : {};
            ws.send(JSON.stringify({
                type: 'module:state',
                module: data.module,
                state: state,
                timestamp: new Date().toISOString()
            }));
        }
    }

    /**
     * Send current module states to a client
     */
    sendModuleStates(ws) {
        const states = {};
        this.modules.forEach((module, name) => {
            states[name] = module.getState ? module.getState() : {};
        });

        ws.send(JSON.stringify({
            type: 'modules:states',
            states: states,
            timestamp: new Date().toISOString()
        }));
    }

    /**
     * Broadcast message to all connected clients
     */
    broadcast(data) {
        const message = JSON.stringify(data);
        this.clients.forEach((client) => {
            if (client.readyState === WebSocket.OPEN) {
                client.send(message);
            }
        });
    }

    /**
     * Stop the server
     */
    stop() {
        if (this.wss) {
            this.clients.forEach((client) => client.close());
            this.wss.close();
            console.log('[WebSocket] Server stopped');
            this.emit('server:stopped');
        }
    }
}

module.exports = ModuleWebSocketServer;