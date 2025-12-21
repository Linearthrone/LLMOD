const express = require('express');
const fs = require('fs').promises;
const path = require('path');
const logger = require('../../Central Core/logger');

class ViewPortServer {
    constructor() {
        this.app = express();
        this.port = process.env.VIEWPORT_PORT || 8082;
        this.currentAvatar = null;
        this.avatarDir = path.join(__dirname, 'avatars');
        
        this.setupMiddleware();
        this.setupRoutes();
        this.ensureAvatarDir();
    }

    setupMiddleware() {
        this.app.use(express.json());
        this.app.use('/avatars', express.static(this.avatarDir));
        this.app.use(express.static(path.join(__dirname, 'client')));
        this.app.use((req, res, next) => {
            logger.log(`[ViewPort] ${req.method} ${req.path}`);
            next();
        });
    }

    setupRoutes() {
        // Health check
        this.app.get('/health', (req, res) => {
            res.json({ 
                status: 'healthy', 
                timestamp: new Date().toISOString(),
                currentAvatar: this.currentAvatar ? 'set' : 'none'
            });
        });

        // Get current avatar
        this.app.get('/api/avatar', (req, res) => {
            res.json({
                currentAvatar: this.currentAvatar,
                url: this.currentAvatar ? `/avatars/${this.currentAvatar}` : null
            });
        });

        // Set avatar
        this.app.post('/api/avatar', async (req, res) => {
            try {
                const { avatarUrl, avatarData, contactName } = req.body;
                
                if (avatarUrl) {
                    this.currentAvatar = avatarUrl;
                } else if (avatarData) {
                    // Save avatar data to file
                    const filename = `avatar_${Date.now()}.png`;
                    const filepath = path.join(this.avatarDir, filename);
                    
                    // In a real implementation, you'd decode base64 data
                    await fs.writeFile(filepath, Buffer.from(avatarData, 'base64'));
                    
                    this.currentAvatar = filename;
                } else if (contactName) {
                    // Generate avatar from contact name
                    this.currentAvatar = this.generateAvatarPlaceholder(contactName);
                }
                
                logger.log(`[ViewPort] Avatar set: ${this.currentAvatar}`);
                res.json({ 
                    success: true, 
                    avatar: this.currentAvatar,
                    url: `/avatars/${this.currentAvatar}`
                });
            } catch (error) {
                logger.error('Error setting avatar:', error);
                res.status(500).json({ error: 'Failed to set avatar' });
            }
        });

        // Clear avatar
        this.app.delete('/api/avatar', (req, res) => {
            this.currentAvatar = null;
            logger.log('[ViewPort] Avatar cleared');
            res.json({ success: true });
        });

        // Get avatar history
        this.app.get('/api/avatars', async (req, res) => {
            try {
                const files = await fs.readdir(this.avatarDir);
                const avatars = files.filter(file => file.startsWith('avatar_'));
                res.json({ avatars, current: this.currentAvatar });
            } catch (error) {
                res.json({ avatars: [], current: this.currentAvatar });
            }
        });

        // Generate avatar SVG
        this.app.get('/api/avatar/generate/:name', (req, res) => {
            const name = req.params.name;
            const svg = this.generateAvatarSVG(name);
            
            res.setHeader('Content-Type', 'image/svg+xml');
            res.send(svg);
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

        // Serve client interface
        this.app.get('/', (req, res) => {
            res.sendFile(path.join(__dirname, 'client', 'index.html'));
        });
    }

    generateAvatarPlaceholder(name) {
        const initials = name.split(' ').map(word => word[0]).join('').toUpperCase().slice(0, 2);
        return initials || '?';
    }

    generateAvatarSVG(name) {
        const initials = this.generateAvatarPlaceholder(name);
        const colors = ['#4a9eff', '#ff4a4a', '#4eff4a', '#ff9800', '#9c27b0'];
        const color = colors[name.charCodeAt(0) % colors.length];
        
        return `
            <svg width="200" height="200" viewBox="0 0 200 200" xmlns="http://www.w3.org/2000/svg">
                <rect width="200" height="200" fill="${color}"/>
                <text x="100" y="110" text-anchor="middle" fill="white" font-size="60" font-weight="bold" font-family="Arial, sans-serif">
                    ${initials}
                </text>
            </svg>
        `;
    }

    async ensureAvatarDir() {
        try {
            await fs.mkdir(this.avatarDir, { recursive: true });
        } catch (error) {
            logger.error('Error creating avatar directory:', error);
        }
    }

    getSettings() {
        return {
            displayMode: 'avatar-only',
            showInfo: false,
            avatarSize: 200,
            refreshInterval: 5000
        };
    }

    updateSettings(newSettings) {
        logger.log('Settings updated:', newSettings);
    }

    start() {
        this.app.listen(this.port, () => {
            logger.log(`ViewPort server running on port ${this.port}`);
        });
    }

    stop() {
        logger.log('ViewPort server stopped');
    }
}

// Start server if run directly
if (require.main === module) {
    const server = new ViewPortServer();
    
    process.on('SIGINT', () => {
        logger.log('Shutting down ViewPort server...');
        server.stop();
        process.exit(0);
    });
    
    process.on('SIGTERM', () => {
        logger.log('Shutting down ViewPort server...');
        server.stop();
        process.exit(0);
    });
    
    server.start();
}

module.exports = ViewPortServer;