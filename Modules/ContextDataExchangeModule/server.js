const multer = require('multer');
const fs = require('fs').promises;
const path = require('path');
const express = require('express');
const logger = require('../../Central Core/logger');
const BaseServer = require('../../Central Core/BaseServer');

class ContextDataServer extends BaseServer {
    constructor() {
        super('ContextData', process.env.CONTEXT_PORT || 8084, { modulePath: __dirname });
        this.contextDir = path.join(__dirname, 'context_data');
        this.maxFileSize = 10 * 1024 * 1024; // 10MB
        this.allowedTypes = ['jpg', 'jpeg', 'png', 'gif', 'pdf', 'txt', 'doc', 'docx', 'mp4', 'avi', 'mov'];
        
        this.setupMulter();
        this.setupBaseRoutes();
        this.setupRoutes();
        this.ensureContextDir();
    }

    setupMulter() {
        // Configure multer for file uploads
        this.storage = multer.diskStorage({
            destination: (req, file, cb) => {
                cb(null, this.contextDir);
            },
            filename: (req, file, cb) => {
                const uniqueSuffix = Date.now() + '-' + Math.round(Math.random() * 1E9);
                cb(null, uniqueSuffix + path.extname(file.originalname));
            }
        });

        this.upload = multer({
            storage: this.storage,
            limits: {
                fileSize: this.maxFileSize
            },
            fileFilter: (req, file, cb) => {
                const ext = path.extname(file.originalname).toLowerCase().slice(1);
                if (this.allowedTypes.includes(ext)) {
                    cb(null, true);
                } else {
                    cb(new Error(`File type ${ext} not allowed`));
                }
            }
        });

        // Serve uploaded files
        this.app.use('/files', express.static(this.contextDir));
    }

    getHealthData() {
        return {
            timestamp: new Date().toISOString(),
            storage: this.contextDir
        };
    }

    setupRoutes() {

        // Get all files
        this.app.get('/api/files', async (req, res) => {
            try {
                const files = await this.getFileList();
                res.json({ files });
            } catch (error) {
                logger.error('Error getting file list:', error);
                res.status(500).json({ error: 'Failed to get file list' });
            }
        });

        // Upload files
        this.app.post('/api/upload', this.upload.array('files'), async (req, res) => {
            try {
                const uploadedFiles = req.files.map(file => ({
                    id: file.filename,
                    originalName: file.originalname,
                    size: file.size,
                    type: path.extname(file.originalname).slice(1),
                    url: `/files/${file.filename}`,
                    uploadedAt: new Date().toISOString()
                }));

                logger.log(`[ContextData] Uploaded ${uploadedFiles.length} files`);
                res.json({ 
                    success: true, 
                    files: uploadedFiles 
                });
            } catch (error) {
                logger.error('Error uploading files:', error);
                res.status(500).json({ error: 'Failed to upload files' });
            }
        });

        // Get specific file info
        this.app.get('/api/files/:id', async (req, res) => {
            try {
                const files = await this.getFileList();
                const file = files.find(f => f.id === req.params.id);
                
                if (!file) {
                    return res.status(404).json({ error: 'File not found' });
                }
                
                res.json(file);
            } catch (error) {
                logger.error('Error getting file info:', error);
                res.status(500).json({ error: 'Failed to get file info' });
            }
        });

        // Delete file
        this.app.delete('/api/files/:id', async (req, res) => {
            try {
                const filepath = path.join(this.contextDir, req.params.id);
                await fs.unlink(filepath);
                
                logger.log(`[ContextData] Deleted file: ${req.params.id}`);
                res.json({ success: true });
            } catch (error) {
                logger.error('Error deleting file:', error);
                res.status(500).json({ error: 'Failed to delete file' });
            }
        });

        // Download file
        this.app.get('/api/files/:id/download', async (req, res) => {
            try {
                const filepath = path.join(this.contextDir, req.params.id);
                const stat = await fs.stat(filepath);
                
                res.download(filepath, (err) => {
                    if (err) {
                        logger.error('Error downloading file:', err);
                        res.status(500).json({ error: 'Failed to download file' });
                    }
                });
            } catch (error) {
                logger.error('Error preparing download:', error);
                res.status(500).json({ error: 'Failed to download file' });
            }
        });

        // Clear all files
        this.app.delete('/api/files', async (req, res) => {
            try {
                const files = await fs.readdir(this.contextDir);
                const deletePromises = files.map(file => 
                    fs.unlink(path.join(this.contextDir, file))
                );
                
                await Promise.all(deletePromises);
                
                logger.log(`[ContextData] Cleared ${files.length} files`);
                res.json({ success: true });
            } catch (error) {
                logger.error('Error clearing files:', error);
                res.status(500).json({ error: 'Failed to clear files' });
            }
        });

        // Get file content (for text files)
        this.app.get('/api/files/:id/content', async (req, res) => {
            try {
                const filepath = path.join(this.contextDir, req.params.id);
                const content = await fs.readFile(filepath, 'utf8');
                
                res.json({ content });
            } catch (error) {
                logger.error('Error reading file content:', error);
                res.status(500).json({ error: 'Failed to read file content' });
            }
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

    async getFileList() {
        try {
            const files = await fs.readdir(this.contextDir);
            const fileList = [];
            
            for (const filename of files) {
                const filepath = path.join(this.contextDir, filename);
                const stat = await fs.stat(filepath);
                
                fileList.push({
                    id: filename,
                    name: filename,
                    size: stat.size,
                    type: path.extname(filename).slice(1),
                    url: `/files/${filename}`,
                    createdAt: stat.birthtime.toISOString(),
                    modifiedAt: stat.mtime.toISOString()
                });
            }
            
            return fileList;
        } catch (error) {
            logger.error('Error reading directory:', error);
            return [];
        }
    }

    async ensureContextDir() {
        try {
            await fs.mkdir(this.contextDir, { recursive: true });
        } catch (error) {
            logger.error('Error creating context directory:', error);
        }
    }

    getSettings() {
        return {
            maxFileSize: this.maxFileSize,
            allowedTypes: this.allowedTypes,
            autoProcess: true,
            compressionEnabled: false
        };
    }

    updateSettings(newSettings) {
        if (newSettings.maxFileSize) {
            this.maxFileSize = newSettings.maxFileSize;
        }
        if (newSettings.allowedTypes) {
            this.allowedTypes = newSettings.allowedTypes;
        }
        
        logger.log('Settings updated:', newSettings);
    }
}

// Start server if run directly
if (require.main === module) {
    const server = new ContextDataServer();
    server.setupSignalHandlers();
    server.start().catch((error) => {
        logger.error('Failed to start server:', error);
        process.exit(1);
    });
}

module.exports = ContextDataServer;