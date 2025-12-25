const { Server } = require('ws');
const http = require('http');
const path = require('path');
const logger = require('../../Central Core/logger');
const BaseServer = require('../../Central Core/BaseServer');

class ChatServer extends BaseServer {
    constructor() {
        super('Chat', process.env.CHAT_PORT || 8080, { hasClient: true });
        this.server = http.createServer(this.app);
        this.wss = new Server({ server: this.server });
        this.messages = [];
        this.clients = new Map();
        
        this.setupBaseRoutes();
        this.setupRoutes();
        this.setupWebSocket();
        this.loadHistory();
    }

    getHealthData() {
        return {
            clients: this.clients.size,
            messages: this.messages.length
        };
    }

    setupRoutes() {

        // Get all messages
        this.app.get('/api/messages', (req, res) => {
            const limit = parseInt(req.query.limit) || 50;
            const offset = parseInt(req.query.offset) || 0;
            
            res.json({
                messages: this.messages.slice(-limit - offset, -offset || undefined),
                total: this.messages.length
            });
        });

        // Send a message
        this.app.post('/api/messages', async (req, res) => {
            try {
                const { content, type = 'user', metadata = {} } = req.body;
                
                if (!content) {
                    return res.status(400).json({ error: 'Content is required' });
                }

                const message = {
                    id: Date.now(),
                    content,
                    type,
                    metadata,
                    timestamp: new Date().toISOString()
                };

                this.messages.push(message);
                this.saveHistory();
                this.broadcastMessage(message);

                // Process with AI if it's a user message
                if (type === 'user') {
                    this.processAIMessage(message);
                }

                res.json(message);
            } catch (error) {
                this.handleError(res, error, 'Failed to send message');
            }
        });

        // Clear messages
        this.app.delete('/api/messages', (req, res) => {
            this.messages = [];
            this.saveHistory();
            this.broadcast({ type: 'cleared' });
            res.json({ success: true });
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

        // Get models from Ollama
        this.app.get('/api/models', async (req, res) => {
            try {
                const models = await this.getOllamaModels();
                res.json(models);
            } catch (error) {
                this.handleError(res, error, 'Failed to fetch models');
            }
        });

    }

    setupWebSocket() {
        this.wss.on('connection', (ws) => {
            const clientId = Date.now();
            this.clients.set(clientId, ws);
            
            logger.log(`[Chat] Client connected: ${clientId}`);

            // Send recent messages to new client
            ws.send(JSON.stringify({
                type: 'history',
                messages: this.messages.slice(-50)
            }));

            ws.on('message', (data) => {
                try {
                    const message = JSON.parse(data);
                    this.handleWebSocketMessage(clientId, message);
                } catch (error) {
                    logger.error('Invalid WebSocket message:', error);
                }
            });

            ws.on('close', () => {
                this.clients.delete(clientId);
                logger.log(`[Chat] Client disconnected: ${clientId}`);
            });
        });
    }

    handleWebSocketMessage(clientId, message) {
        switch (message.type) {
            case 'message':
                this.postMessage({
                    ...message.data,
                    type: 'user'
                });
                break;
            case 'typing':
                this.broadcast({
                    type: 'typing',
                    clientId,
                    isTyping: message.isTyping
                }, clientId);
                break;
        }
    }

    async processAIMessage(userMessage) {
        try {
            const settings = this.getSettings();
            
            // Simulate AI response (replace with actual Ollama integration)
            setTimeout(() => {
                const aiResponse = {
                    id: Date.now(),
                    content: 'This is a simulated AI response. Integrate with Ollama for real responses.',
                    type: 'assistant',
                    metadata: { model: settings.model || 'llama2' },
                    timestamp: new Date().toISOString()
                };

                this.messages.push(aiResponse);
                this.saveHistory();
                this.broadcastMessage(aiResponse);
            }, 1000);

        } catch (error) {
            logger.error('Error processing AI message:', error);
        }
    }

    broadcastMessage(message) {
        this.broadcast({
            type: 'message',
            data: message
        });
    }

    broadcast(data, excludeClientId = null) {
        const message = JSON.stringify(data);
        
        this.clients.forEach((client, clientId) => {
            if (clientId !== excludeClientId && client.readyState === client.OPEN) {
                client.send(message);
            }
        });
    }

    async postMessage(messageData) {
        try {
            const response = await fetch(`http://localhost:${this.port}/api/messages`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(messageData)
            });
            
            if (!response.ok) {
                throw new Error('Failed to post message');
            }
            
            return await response.json();
        } catch (error) {
            logger.error('Error posting message:', error);
            throw error;
        }
    }

    getSettings() {
        return {
            model: 'llama2',
            temperature: 0.7,
            maxTokens: 2048,
            systemPrompt: 'You are a helpful AI assistant.',
            streaming: true
        };
    }

    updateSettings(newSettings) {
        // In a real implementation, save to file or database
        logger.log('Settings updated:', newSettings);
    }

    loadHistory() {
        // Load from file or database
        // For now, start with welcome message
        this.messages = [{
            id: 1,
            content: 'Welcome to LLMOD Chat! Connect to Ollama for AI responses.',
            type: 'system',
            timestamp: new Date().toISOString()
        }];
    }

    saveHistory() {
        // Save to file or database
        // For now, just log
        logger.log(`Saved ${this.messages.length} messages to history`);
    }

    start() {
        return new Promise((resolve, reject) => {
            try {
                this.server.listen(this.port, () => {
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
}

// Start server if run directly
if (require.main === module) {
    const server = new ChatServer();
    server.setupSignalHandlers();
    server.start().catch((error) => {
        logger.error('Failed to start server:', error);
        process.exit(1);
    });
}

module.exports = ChatServer;