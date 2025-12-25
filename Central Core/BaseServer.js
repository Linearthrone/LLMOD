const express = require('express');
const path = require('path');
const logger = require('./logger');

/**
 * BaseServer - Base class for all LLMOD module servers
 * Provides common functionality like middleware setup, health checks, and server lifecycle
 */
class BaseServer {
    constructor(moduleName, port, options = {}) {
        this.moduleName = moduleName;
        this.port = port;
        this.app = express();
        this.server = null;
        this.options = {
            hasClient: true,
            modulePath: null, // Will be set by subclass
            ...options
        };
        
        this.setupBaseMiddleware();
    }

    /**
     * Setup common middleware for all servers
     */
    setupBaseMiddleware() {
        this.app.use(express.json());
        
        // Serve static client files if module has a client
        if (this.options.hasClient && this.options.modulePath) {
            this.app.use(express.static(path.join(this.options.modulePath, 'client')));
        }
        
        // Request logging middleware
        this.app.use((req, res, next) => {
            logger.log(`[${this.moduleName}] ${req.method} ${req.path}`);
            next();
        });
    }

    /**
     * Setup common routes for all servers
     */
    setupBaseRoutes() {
        // Health check endpoint - can be overridden by subclasses
        this.app.get('/health', (req, res) => {
            res.json({
                status: 'healthy',
                module: this.moduleName,
                timestamp: new Date().toISOString(),
                ...this.getHealthData()
            });
        });

        // Serve client interface if available
        if (this.options.hasClient && this.options.modulePath) {
            this.app.get('/', (req, res) => {
                res.sendFile(path.join(this.options.modulePath, 'client', 'index.html'));
            });
        }
    }

    /**
     * Get module-specific health data
     * Override in subclasses to add custom health information
     */
    getHealthData() {
        return {};
    }

    /**
     * Start the server
     */
    start() {
        return new Promise((resolve, reject) => {
            try {
                this.server = this.app.listen(this.port, () => {
                    logger.log(`${this.moduleName} server running on port ${this.port}`);
                    resolve();
                });
                
                this.server.on('error', (error) => {
                    logger.error(`${this.moduleName} server error:`, error);
                    reject(error);
                });
            } catch (error) {
                logger.error(`Failed to start ${this.moduleName} server:`, error);
                reject(error);
            }
        });
    }

    /**
     * Stop the server
     */
    stop() {
        return new Promise((resolve) => {
            if (this.server) {
                this.server.close(() => {
                    logger.log(`${this.moduleName} server stopped`);
                    resolve();
                });
            } else {
                logger.log(`${this.moduleName} server was not running`);
                resolve();
            }
        });
    }

    /**
     * Handle common error responses
     */
    handleError(res, error, message = 'Internal server error', statusCode = 500) {
        logger.error(`[${this.moduleName}] ${message}:`, error);
        res.status(statusCode).json({ 
            error: message,
            details: error.message 
        });
    }

    /**
     * Common endpoint to fetch Ollama models
     * Can be used by any module that needs model information
     */
    async getOllamaModels() {
        try {
            const response = await fetch('http://localhost:11434/api/tags');
            const data = await response.json();
            return data.models || [];
        } catch (error) {
            logger.error(`[${this.moduleName}] Failed to fetch Ollama models:`, error);
            throw error;
        }
    }

    /**
     * Setup common signal handlers for graceful shutdown
     */
    setupSignalHandlers() {
        const shutdown = () => {
            logger.log(`Shutting down ${this.moduleName} server...`);
            this.stop().then(() => process.exit(0));
        };

        process.on('SIGINT', shutdown);
        process.on('SIGTERM', shutdown);
    }

    /**
     * Static method to run a server if executed directly
     */
    static runIfMain(ServerClass, ...args) {
        if (require.main === module) {
            const server = new ServerClass(...args);
            server.setupSignalHandlers();
            server.start().catch((error) => {
                logger.error('Failed to start server:', error);
                process.exit(1);
            });
        }
    }
}

module.exports = BaseServer;
