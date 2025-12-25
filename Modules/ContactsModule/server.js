const fs = require('fs').promises;
const path = require('path');
const logger = require('../../Central Core/logger');
const BaseServer = require('../../Central Core/BaseServer');

class ContactsServer extends BaseServer {
    constructor() {
        super('Contacts', process.env.CONTACTS_PORT || 8081);
        this.contactsFile = path.join(__dirname, 'contacts.json');
        this.contacts = [];
        
        this.setupBaseRoutes();
        this.setupRoutes();
        this.loadContacts();
    }

    getHealthData() {
        return {
            contacts: this.contacts.length
        };
    }

    setupRoutes() {

        // Get all contacts
        this.app.get('/api/contacts', (req, res) => {
            res.json({
                contacts: this.contacts,
                total: this.contacts.length
            });
        });

        // Get a specific contact
        this.app.get('/api/contacts/:id', (req, res) => {
            const contact = this.contacts.find(c => c.id == req.params.id);
            if (!contact) {
                return res.status(404).json({ error: 'Contact not found' });
            }
            res.json(contact);
        });

        // Create a new contact
        this.app.post('/api/contacts', async (req, res) => {
            try {
                const { name, avatar, description, model, metadata = {} } = req.body;
                
                if (!name) {
                    return res.status(400).json({ error: 'Name is required' });
                }

                const contact = {
                    id: Date.now(),
                    name,
                    avatar: avatar || null,
                    description: description || '',
                    model: model || 'llama2',
                    metadata,
                    createdAt: new Date().toISOString(),
                    lastUsed: null
                };

                this.contacts.push(contact);
                await this.saveContacts();
                
                logger.log(`[Contacts] Created contact: ${name}`);
                res.status(201).json(contact);
            } catch (error) {
                logger.error('Error creating contact:', error);
                res.status(500).json({ error: 'Failed to create contact' });
            }
        });

        // Update a contact
        this.app.put('/api/contacts/:id', async (req, res) => {
            try {
                const index = this.contacts.findIndex(c => c.id == req.params.id);
                if (index === -1) {
                    return res.status(404).json({ error: 'Contact not found' });
                }

                const updates = req.body;
                this.contacts[index] = {
                    ...this.contacts[index],
                    ...updates,
                    updatedAt: new Date().toISOString()
                };

                await this.saveContacts();
                logger.log(`[Contacts] Updated contact: ${req.params.id}`);
                res.json(this.contacts[index]);
            } catch (error) {
                logger.error('Error updating contact:', error);
                res.status(500).json({ error: 'Failed to update contact' });
            }
        });

        // Delete a contact
        this.app.delete('/api/contacts/:id', async (req, res) => {
            try {
                const index = this.contacts.findIndex(c => c.id == req.params.id);
                if (index === -1) {
                    return res.status(404).json({ error: 'Contact not found' });
                }

                const deleted = this.contacts.splice(index, 1)[0];
                await this.saveContacts();
                
                logger.log(`[Contacts] Deleted contact: ${deleted.name}`);
                res.json({ success: true, deleted });
            } catch (error) {
                logger.error('Error deleting contact:', error);
                res.status(500).json({ error: 'Failed to delete contact' });
            }
        });

        // Get last used contacts
        this.app.get('/api/contacts/recent/:limit', (req, res) => {
            const limit = parseInt(req.params.limit) || 2;
            const recent = this.contacts
                .filter(c => c.lastUsed)
                .sort((a, b) => new Date(b.lastUsed) - new Date(a.lastUsed))
                .slice(0, limit);
            
            res.json(recent);
        });

        // Mark contact as used
        this.app.post('/api/contacts/:id/use', async (req, res) => {
            try {
                const contact = this.contacts.find(c => c.id == req.params.id);
                if (!contact) {
                    return res.status(404).json({ error: 'Contact not found' });
                }

                contact.lastUsed = new Date().toISOString();
                await this.saveContacts();
                
                logger.log(`[Contacts] Used contact: ${contact.name}`);
                res.json({ success: true, contact });
            } catch (error) {
                logger.error('Error marking contact as used:', error);
                res.status(500).json({ error: 'Failed to update contact' });
            }
        });

        // Upload avatar
        this.app.post('/api/contacts/:id/avatar', async (req, res) => {
            try {
                // This would handle file upload
                // For now, just return a placeholder URL
                res.json({ 
                    success: true, 
                    avatarUrl: `/api/contacts/${req.params.id}/avatar.png` 
                });
            } catch (error) {
                logger.error('Error uploading avatar:', error);
                res.status(500).json({ error: 'Failed to upload avatar' });
            }
        });

        // Get available models
        this.app.get('/api/models', async (req, res) => {
            try {
                const response = await fetch('http://localhost:11434/api/tags');
                const data = await response.json();
                res.json(data.models || []);
            } catch (error) {
                logger.error('Failed to fetch models:', error);
                res.status(500).json({ error: 'Failed to fetch models' });
            }
        });

        // Search contacts
        this.app.get('/api/contacts/search/:query', (req, res) => {
            const query = req.params.query.toLowerCase();
            const results = this.contacts.filter(contact => 
                contact.name.toLowerCase().includes(query) ||
                contact.description.toLowerCase().includes(query)
            );
            
            res.json(results);
        });

        // Get settings
        this.app.get('/api/settings', (req, res) => {
            res.json(this.getSettings());
        });

        // Update settings
        this.app.put('/api/settings', async (req, res) => {
            await this.updateSettings(req.body);
            res.json({ success: true });
        });

    }

    getSettings() {
        return {
            defaultModel: 'llama2',
            autoSave: true,
            maxContacts: 1000,
            avatarSizeLimit: 5 * 1024 * 1024 // 5MB
        };
    }

    async updateSettings(newSettings) {
        // Save settings to file
        const settingsFile = path.join(__dirname, 'settings.json');
        try {
            await fs.writeFile(settingsFile, JSON.stringify(newSettings, null, 2));
            logger.log('Settings updated:', newSettings);
        } catch (error) {
            logger.error('Failed to save settings:', error);
        }
    }

    async loadContacts() {
        try {
            const data = await fs.readFile(this.contactsFile, 'utf8');
            this.contacts = JSON.parse(data);
            logger.log(`[Contacts] Loaded ${this.contacts.length} contacts`);
        } catch (error) {
            if (error.code !== 'ENOENT') {
                logger.error('Error loading contacts:', error);
            }
            this.contacts = [];
        }
    }

    async saveContacts() {
        try {
            await fs.writeFile(this.contactsFile, JSON.stringify(this.contacts, null, 2));
            logger.log(`[Contacts] Saved ${this.contacts.length} contacts`);
        } catch (error) {
            logger.error('Error saving contacts:', error);
            throw error;
        }
    }
}

// Start server if run directly
if (require.main === module) {
    const server = new ContactsServer();
    server.setupSignalHandlers();
    server.start().catch((error) => {
        logger.error('Failed to start server:', error);
        process.exit(1);
    });
}

module.exports = ContactsServer;