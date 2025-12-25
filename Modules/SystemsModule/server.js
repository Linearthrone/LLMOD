const si = require('systeminformation');
const logger = require('../../Central Core/logger');
const path = require('path');
const BaseServer = require('../../Central Core/BaseServer');

class SystemsServer extends BaseServer {
    constructor() {
        super('Systems', process.env.SYSTEMS_PORT || 8083, { modulePath: __dirname });
        this.monitoringInterval = null;
        this.currentStats = {
            cpu: { usage: 0, temperature: 0 },
            gpu: { usage: 0, temperature: 0 },
            memory: { used: 0, total: 0, percentage: 0 },
            network: { upload: 0, download: 0 },
            system: { uptime: 0, platform: '' },
            services: {
                ollama: { connected: false, model: null },
                mcpServers: {
                    ltm: { connected: false },
                    resourceBank: { connected: false },
                    agentTools: { connected: false },
                    tts: { connected: false }
                }
            }
        };
        
        this.setupBaseRoutes();
        this.setupRoutes();
        this.startMonitoring();
    }

    getHealthData() {
        return {
            monitoring: this.monitoringInterval ? 'active' : 'inactive'
        };
    }

    setupRoutes() {

        // Get current system stats
        this.app.get('/api/stats', (req, res) => {
            res.json(this.currentStats);
        });

        // Get detailed CPU info
        this.app.get('/api/cpu', async (req, res) => {
            try {
                const cpuData = await si.cpu();
                const cpuSpeed = await si.cpuCurrentSpeed();
                const cpuLoad = await si.currentLoad();
                
                res.json({
                    manufacturer: cpuData.manufacturer,
                    brand: cpuData.brand,
                    cores: cpuData.cores,
                    physicalCores: cpuData.physicalCores,
                    speed: cpuSpeed,
                    load: cpuLoad
                });
            } catch (error) {
                logger.error('Error getting CPU info:', error);
                res.status(500).json({ error: 'Failed to get CPU info' });
            }
        });

        // Get detailed memory info
        this.app.get('/api/memory', async (req, res) => {
            try {
                const memData = await si.mem();
                res.json(memData);
            } catch (error) {
                logger.error('Error getting memory info:', error);
                res.status(500).json({ error: 'Failed to get memory info' });
            }
        });

        // Get GPU info
        this.app.get('/api/gpu', async (req, res) => {
            try {
                const gpuData = await si.graphics();
                res.json(gpuData);
            } catch (error) {
                logger.error('Error getting GPU info:', error);
                res.status(500).json({ error: 'Failed to get GPU info' });
            }
        });

        // Get network stats
        this.app.get('/api/network', async (req, res) => {
            try {
                const networkData = await si.networkInterfaces();
                const networkStats = await si.networkStats();
                
                res.json({
                    interfaces: networkData,
                    stats: networkStats
                });
            } catch (error) {
                logger.error('Error getting network info:', error);
                res.status(500).json({ error: 'Failed to get network info' });
            }
        });

        // Get disk info
        this.app.get('/api/disk', async (req, res) => {
            try {
                const diskData = await si.fsSize();
                const diskLayout = await si.diskLayout();
                
                res.json({
                    filesystems: diskData,
                    layout: diskLayout
                });
            } catch (error) {
                logger.error('Error getting disk info:', error);
                res.status(500).json({ error: 'Failed to get disk info' });
            }
        });

        // Get system info
        this.app.get('/api/system', async (req, res) => {
            try {
                const osData = await si.osInfo();
                const systemData = await si.system();
                const timeData = await si.time();
                
                res.json({
                    os: osData,
                    system: systemData,
                    time: timeData,
                    uptime: timeData.uptime
                });
            } catch (error) {
                logger.error('Error getting system info:', error);
                res.status(500).json({ error: 'Failed to get system info' });
            }
        });

        // Check service status
        this.app.get('/api/services', async (req, res) => {
            try {
                const services = await this.checkServices();
                res.json(services);
            } catch (error) {
                logger.error('Error checking services:', error);
                res.status(500).json({ error: 'Failed to check services' });
            }
        });

        // Check specific service
        this.app.get('/api/services/:service', async (req, res) => {
            try {
                const service = req.params.service;
                const status = await this.checkService(service);
                res.json(status);
            } catch (error) {
                logger.error('Error checking service:', error);
                res.status(500).json({ error: 'Failed to check service' });
            }
        });

        // Get processes
        this.app.get('/api/processes', async (req, res) => {
            try {
                const processes = await si.processes();
                res.json(processes);
            } catch (error) {
                logger.error('Error getting processes:', error);
                res.status(500).json({ error: 'Failed to get processes' });
            }
        });

        // Get history (placeholder)
        this.app.get('/api/history/:metric', (req, res) => {
            // In a real implementation, this would return historical data
            res.json({
                metric: req.params.metric,
                data: [],
                message: 'History tracking not implemented yet'
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

        // Start/stop monitoring
        this.app.post('/api/monitoring/:action', (req, res) => {
            const action = req.params.action;
            
            if (action === 'start') {
                this.startMonitoring();
                res.json({ success: true, monitoring: 'active' });
            } else if (action === 'stop') {
                this.stopMonitoring();
                res.json({ success: true, monitoring: 'inactive' });
            } else {
                res.status(400).json({ error: 'Invalid action' });
            }
        });

    }

    async updateStats() {
        try {
            // CPU
            const cpuLoad = await si.currentLoad();
            this.currentStats.cpu.usage = cpuLoad.currentLoad;

            // Memory
            const memData = await si.mem();
            this.currentStats.memory = {
                used: memData.used,
                total: memData.total,
                percentage: (memData.used / memData.total) * 100
            };

            // Network
            const networkStats = await si.networkStats();
            if (networkStats.length > 0) {
                this.currentStats.network = {
                    upload: networkStats[0].tx_sec || 0,
                    download: networkStats[0].rx_sec || 0
                };
            }

            // System
            const timeData = await si.time();
            this.currentStats.system.uptime = timeData.uptime;

            // Check services
            await this.updateServiceStatus();

        } catch (error) {
            logger.error('Error updating stats:', error);
        }
    }

    async checkServices() {
        const services = {
            ollama: await this.checkOllama(),
            mcpServers: {
                ltm: await this.checkMCPServer('ltm', 3001),
                resourceBank: await this.checkMCPServer('resourceBank', 3002),
                agentTools: await this.checkMCPServer('agentTools', 3003),
                tts: await this.checkMCPServer('tts', 3004)
            }
        };
        
        this.currentStats.services = services;
        return services;
    }

    async checkOllama() {
        try {
            const response = await fetch('http://localhost:11434/api/tags', {
                timeout: 2000
            });
            if (response.ok) {
                const data = await response.json();
                return {
                    connected: true,
                    model: data.models?.[0]?.name || 'unknown',
                    models: data.models || []
                };
            }
        } catch (error) {
            // Connection failed
        }
        
        return { connected: false, model: null };
    }

    async checkMCPServer(name, port) {
        try {
            const response = await fetch(`http://localhost:${port}/health`, {
                timeout: 1000
            });
            return { connected: response.ok };
        } catch (error) {
            return { connected: false };
        }
    }

    async updateServiceStatus() {
        try {
            this.currentStats.services = await this.checkServices();
        } catch (error) {
            logger.error('Error updating service status:', error);
        }
    }

    startMonitoring() {
        if (this.monitoringInterval) {
            return; // Already running
        }

        logger.log('[Systems] Starting monitoring');
        this.monitoringInterval = setInterval(async () => {
            await this.updateStats();
        }, 2000);

        // Initial update
        this.updateStats();
    }

    stopMonitoring() {
        if (this.monitoringInterval) {
            clearInterval(this.monitoringInterval);
            this.monitoringInterval = null;
            logger.log('[Systems] Stopped monitoring');
        }
    }

    getSettings() {
        return {
            monitoringInterval: 2000,
            autoStart: true,
            logLevel: 'info',
            gpuMonitoring: false
        };
    }

    updateSettings(newSettings) {
        logger.log('Settings updated:', newSettings);
        
        // Apply settings
        if (newSettings.monitoringInterval) {
            if (this.monitoringInterval) {
                this.stopMonitoring();
                this.monitoringInterval = setInterval(async () => {
                    await this.updateStats();
                }, newSettings.monitoringInterval);
            }
        }
    }

    async stop() {
        this.stopMonitoring();
        await super.stop();
    }
}

// Start server if run directly
if (require.main === module) {
    const server = new SystemsServer();
    server.setupSignalHandlers();
    server.start().catch((error) => {
        logger.error('Failed to start server:', error);
        process.exit(1);
    });
}

module.exports = SystemsServer;