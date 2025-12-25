const { spawn } = require('child_process');
const logger = require('../../Central Core/logger');
const path = require('path');
const BaseServer = require('../../Central Core/BaseServer');

class AppTrayServer extends BaseServer {
    constructor() {
        super('AppTray', process.env.APPTRAY_PORT || 8085, { modulePath: __dirname });
        this.modules = new Map();
        this.config = require('../../Central Core/config');
        
        this.setupBaseRoutes();
        this.setupRoutes();
        this.discoverModules();
    }

    getHealthData() {
        return {
            modules: this.modules.size
        };
    }

    setupRoutes() {

        // Get all modules
        this.app.get('/api/modules', (req, res) => {
            const modules = Array.from(this.modules.entries()).map(([name, info]) => ({
                name,
                ...info
            }));
            res.json({ modules });
        });

        // Get module status
        this.app.get('/api/modules/:name', (req, res) => {
            const module = this.modules.get(req.params.name);
            if (!module) {
                return res.status(404).json({ error: 'Module not found' });
            }
            res.json(module);
        });

        // Start module
        this.app.post('/api/modules/:name/start', async (req, res) => {
            try {
                const success = await this.startModule(req.params.name);
                if (success) {
                    res.json({ success: true });
                } else {
                    res.status(400).json({ error: 'Failed to start module' });
                }
            } catch (error) {
                logger.error('Error starting module:', error);
                res.status(500).json({ error: 'Failed to start module' });
            }
        });

        // Stop module
        this.app.post('/api/modules/:name/stop', async (req, res) => {
            try {
                const success = await this.stopModule(req.params.name);
                if (success) {
                    res.json({ success: true });
                } else {
                    res.status(400).json({ error: 'Failed to stop module' });
                }
            } catch (error) {
                logger.error('Error stopping module:', error);
                res.status(500).json({ error: 'Failed to stop module' });
            }
        });

        // Restart module
        this.app.post('/api/modules/:name/restart', async (req, res) => {
            try {
                await this.stopModule(req.params.name);
                const success = await this.startModule(req.params.name);
                if (success) {
                    res.json({ success: true });
                } else {
                    res.status(400).json({ error: 'Failed to restart module' });
                }
            } catch (error) {
                logger.error('Error restarting module:', error);
                res.status(500).json({ error: 'Failed to restart module' });
            }
        });

        // Start all modules
        this.app.post('/api/modules/start-all', async (req, res) => {
            try {
                const results = {};
                const moduleNames = Object.keys(this.config.modules);
                
                for (const name of moduleNames) {
                    results[name] = await this.startModule(name);
                }
                
                res.json({ results });
            } catch (error) {
                logger.error('Error starting all modules:', error);
                res.status(500).json({ error: 'Failed to start modules' });
            }
        });

        // Stop all modules
        this.app.post('/api/modules/stop-all', async (req, res) => {
            try {
                const results = {};
                const moduleNames = Array.from(this.modules.keys());
                
                for (const name of moduleNames) {
                    results[name] = await this.stopModule(name);
                }
                
                res.json({ results });
            } catch (error) {
                logger.error('Error stopping all modules:', error);
                res.status(500).json({ error: 'Failed to stop modules' });
            }
        });

        // Get system info
        this.app.get('/api/system', (req, res) => {
            res.json({
                nodeVersion: process.version,
                platform: process.platform,
                arch: process.arch,
                memory: process.memoryUsage(),
                uptime: process.uptime()
            });
        });

        // Get settings
        this.app.get('/api/settings', (req, res) => {
            res.json(this.getSettings());
        });

        // Update settings
        this.app.put('/api/settings', (req, res) => {
            this.updateSettings(req.body);
            res.json({ success: true });
        });

    }

    discoverModules() {
        for (const [name, config] of Object.entries(this.config.modules)) {
            this.modules.set(name, {
                name,
                port: config.port,
                status: 'stopped',
                process: null,
                url: `http://localhost:${config.port}`,
                config
            });
        }
        
        logger.log(`[AppTray] Discovered ${this.modules.size} modules`);
    }

    async startModule(name) {
        const module = this.modules.get(name);
        if (!module) {
            logger.error(`[AppTray] Module not found: ${name}`);
            return false;
        }

        if (module.status === 'running') {
            logger.log(`[AppTray] Module ${name} already running`);
            return true;
        }

        try {
            logger.log(`[AppTray] Starting module: ${name}`);
            
            const child = spawn(module.config.command, module.config.args, {
                cwd: path.dirname(module.config.script),
                stdio: 'pipe'
            });

            module.process = child;
            module.status = 'starting';
            module.pid = child.pid;
            module.startTime = new Date().toISOString();

            child.on('error', (error) => {
                logger.error(`[AppTray] Module ${name} error:`, error);
                module.status = 'error';
                module.error = error.message;
            });

            child.on('exit', (code) => {
                logger.log(`[AppTray] Module ${name} exited with code: ${code}`);
                module.status = 'stopped';
                module.process = null;
                module.pid = null;
            });

            // Wait a moment and check if it's running
            await new Promise(resolve => setTimeout(resolve, 2000));
            
            // Try to check if module is responsive
            try {
                const response = await fetch(`http://localhost:${module.port}/health`, { timeout: 1000 });
                if (response.ok) {
                    module.status = 'running';
                    logger.log(`[AppTray] Module ${name} started successfully`);
                    return true;
                }
            } catch (error) {
                // Module might not have health endpoint
                if (child && !child.killed) {
                    module.status = 'running';
                    logger.log(`[AppTray] Module ${name} started (no health check)`);
                    return true;
                }
            }

            module.status = 'error';
            return false;
            
        } catch (error) {
            logger.error(`[AppTray] Failed to start module ${name}:`, error);
            module.status = 'error';
            module.error = error.message;
            return false;
        }
    }

    async stopModule(name) {
        const module = this.modules.get(name);
        if (!module || !module.process) {
            return true;
        }

        try {
            logger.log(`[AppTray] Stopping module: ${name}`);
            
            module.process.kill('SIGTERM');
            
            // Wait for graceful shutdown
            await new Promise(resolve => setTimeout(resolve, 3000));
            
            if (module.process && !module.process.killed) {
                module.process.kill('SIGKILL');
            }

            module.status = 'stopped';
            module.process = null;
            module.pid = null;
            
            logger.log(`[AppTray] Module ${name} stopped`);
            return true;
            
        } catch (error) {
            logger.error(`[AppTray] Failed to stop module ${name}:`, error);
            return false;
        }
    }

    getSettings() {
        return {
            autoStart: false,
            healthCheckInterval: 10000,
            logLevel: 'info'
        };
    }

    updateSettings(newSettings) {
        logger.log('Settings updated:', newSettings);
    }
}

// Start server if run directly
if (require.main === module) {
    const server = new AppTrayServer();
    server.setupSignalHandlers();
    server.start().catch((error) => {
        logger.error('Failed to start server:', error);
        process.exit(1);
    });
}

module.exports = AppTrayServer;