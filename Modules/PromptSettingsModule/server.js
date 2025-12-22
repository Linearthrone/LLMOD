const express = require('express');
const fs = require('fs').promises;
const path = require('path');
const logger = require('../../Central Core/logger');
const SharedUtils = require('../../Central Core/shared-utils');

class PromptSettingsServer {
    constructor() {
        this.app = express();
        this.port = process.env.PROMPT_SETTINGS_PORT || 8086;
        this.settingsFile = path.join(__dirname, 'settings.json');
        this.settings = {};
        
        this.setupMiddleware();
        this.setupRoutes();
        this.loadSettings();
    }

    setupMiddleware() {
        SharedUtils.setupStandardMiddleware(this.app, 'PromptSettings');
        this.app.use(express.static(path.join(__dirname, 'client')));
    }

    setupRoutes() {
        // Health check
        this.app.get('/health', (req, res) => {
            res.json(SharedUtils.createHealthResponse('PromptSettings', {
                settingsLoaded: Object.keys(this.settings).length > 0
            }));
        });

        // Get all settings
        this.app.get('/api/settings', (req, res) => {
            res.json({
                settings: this.settings,
                timestamp: SharedUtils.formatTimestamp()
            });
        });

        // Get specific setting
        this.app.get('/api/settings/:key', (req, res) => {
            const key = req.params.key;
            if (!(key in this.settings)) {
                return res.status(404).json({ error: 'Setting not found' });
            }
            res.json({
                key,
                value: this.settings[key],
                timestamp: SharedUtils.formatTimestamp()
            });
        });

        // Update settings
        this.app.put('/api/settings', async (req, res) => {
            try {
                const updates = req.body;
                
                this.settings = {
                    ...this.settings,
                    ...updates,
                    updatedAt: SharedUtils.formatTimestamp()
                };

                await this.saveSettings();
                logger.log('[PromptSettings] Settings updated');
                res.json({
                    success: true,
                    settings: this.settings
                });
            } catch (error) {
                SharedUtils.handleError(error, res, 'Failed to update settings');
            }
        });

        // Update specific setting
        this.app.put('/api/settings/:key', async (req, res) => {
            try {
                const key = req.params.key;
                const { value } = req.body;

                if (value === undefined) {
                    return res.status(400).json({ error: 'Value is required' });
                }

                this.settings[key] = value;
                this.settings.updatedAt = SharedUtils.formatTimestamp();

                await this.saveSettings();
                logger.log(`[PromptSettings] Updated setting: ${key}`);
                res.json({
                    success: true,
                    key,
                    value
                });
            } catch (error) {
                SharedUtils.handleError(error, res, 'Failed to update setting');
            }
        });

        // Delete specific setting
        this.app.delete('/api/settings/:key', async (req, res) => {
            try {
                const key = req.params.key;
                
                if (!(key in this.settings)) {
                    return res.status(404).json({ error: 'Setting not found' });
                }

                delete this.settings[key];
                await this.saveSettings();
                
                logger.log(`[PromptSettings] Deleted setting: ${key}`);
                res.json({ success: true, deleted: key });
            } catch (error) {
                SharedUtils.handleError(error, res, 'Failed to delete setting');
            }
        });

        // Reset to defaults
        this.app.post('/api/settings/reset', async (req, res) => {
            try {
                this.settings = this.getDefaultSettings();
                await this.saveSettings();
                
                logger.log('[PromptSettings] Settings reset to defaults');
                res.json({
                    success: true,
                    settings: this.settings
                });
            } catch (error) {
                SharedUtils.handleError(error, res, 'Failed to reset settings');
            }
        });

        // Get available models
        this.app.get('/api/models', async (req, res) => {
            try {
                const models = await SharedUtils.fetchOllamaModels();
                res.json({ models });
            } catch (error) {
                SharedUtils.handleError(error, res, 'Failed to fetch models');
            }
        });

        // Export settings
        this.app.get('/api/settings/export', (req, res) => {
            res.setHeader('Content-Type', 'application/json');
            res.setHeader('Content-Disposition', 'attachment; filename=prompt-settings.json');
            res.json(this.settings);
        });

        // Import settings
        this.app.post('/api/settings/import', async (req, res) => {
            try {
                const importedSettings = req.body;
                
                if (!importedSettings || typeof importedSettings !== 'object') {
                    return res.status(400).json({ error: 'Invalid settings format' });
                }

                this.settings = {
                    ...importedSettings,
                    importedAt: SharedUtils.formatTimestamp()
                };

                await this.saveSettings();
                logger.log('[PromptSettings] Settings imported');
                res.json({
                    success: true,
                    settings: this.settings
                });
            } catch (error) {
                SharedUtils.handleError(error, res, 'Failed to import settings');
            }
        });

        // Serve client interface
        this.app.get('/', (req, res) => {
            res.sendFile(path.join(__dirname, 'client', 'index.html'));
        });
    }

    getDefaultSettings() {
        return {
            systemPrompt: 'You are a helpful AI assistant.',
            model: 'llama2',
            temperature: 0.7,
            maxTokens: 2048,
            topP: 0.9,
            topK: 40,
            repeatPenalty: 1.1,
            streaming: true,
            contextWindow: 4096,
            createdAt: SharedUtils.formatTimestamp()
        };
    }

    async loadSettings() {
        try {
            const data = await fs.readFile(this.settingsFile, 'utf8');
            this.settings = JSON.parse(data);
            logger.log(`[PromptSettings] Loaded settings`);
        } catch (error) {
            if (error.code === 'ENOENT') {
                this.settings = this.getDefaultSettings();
                await this.saveSettings();
                logger.log('[PromptSettings] Created default settings');
            } else {
                logger.error('Error loading settings:', error);
                this.settings = this.getDefaultSettings();
            }
        }
    }

    async saveSettings() {
        try {
            await fs.writeFile(this.settingsFile, JSON.stringify(this.settings, null, 2));
            logger.log('[PromptSettings] Settings saved');
        } catch (error) {
            logger.error('Error saving settings:', error);
            throw error;
        }
    }

    start() {
        this.app.listen(this.port, () => {
            logger.log(`Prompt Settings server running on port ${this.port}`);
        });
    }

    stop() {
        logger.log('Prompt Settings server stopped');
    }
}

// Start server if run directly
if (require.main === module) {
    const server = new PromptSettingsServer();
    
    process.on('SIGINT', () => {
        logger.log('Shutting down prompt settings server...');
        server.stop();
        process.exit(0);
    });
    
    process.on('SIGTERM', () => {
        logger.log('Shutting down prompt settings server...');
        server.stop();
        process.exit(0);
    });
    
    server.start();
}

module.exports = PromptSettingsServer;