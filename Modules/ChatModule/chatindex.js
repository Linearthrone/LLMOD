const EventEmitter = require('events');

/**
 * ChatModule - Pure JavaScript module for chat functionality
 * No UI - communicates via events with the WinUI 3 overlay
 */
class ChatModule extends EventEmitter {
    constructor(config = {}) {
        super();
        this.name = 'ChatModule';
        this.messages = [];
        this.maxMessages = config.maxMessages || 100;
        this.ollamaUrl = config.ollamaUrl || 'http://localhost:11434';
        this.model = config.model || 'llama2';
        
        this.initialize();
    }

    initialize() {
        console.log(`[${this.name}] Initializing...`);
        this.emit('module:ready', { module: this.name });
    }

    /**
     * Send a message and get AI response
     */
    async sendMessage(message, user = 'User') {
        const messageObj = {
            id: Date.now(),
            user: user,
            text: message,
            timestamp: new Date().toISOString(),
            type: 'user'
        };

        this.messages.push(messageObj);
        this.emit('chat:message', messageObj);

        // Get AI response
        try {
            const response = await this.getAIResponse(message);
            const aiMessage = {
                id: Date.now() + 1,
                user: 'Assistant',
                text: response,
                timestamp: new Date().toISOString(),
                type: 'assistant'
            };

            this.messages.push(aiMessage);
            this.emit('chat:response', aiMessage);

            // Trim messages if exceeding max
            if (this.messages.length > this.maxMessages) {
                this.messages = this.messages.slice(-this.maxMessages);
            }

            return aiMessage;
        } catch (error) {
            console.error(`[${this.name}] Error getting AI response:`, error);
            this.emit('chat:error', { error: error.message });
            return null;
        }
    }

    /**
     * Get AI response from Ollama
     */
    async getAIResponse(prompt) {
        // TODO: Implement actual Ollama integration
        // For now, return a mock response
        return `AI response to: ${prompt}`;
    }

    /**
     * Get all messages
     */
    getMessages() {
        return this.messages;
    }

    /**
     * Clear message history
     */
    clearMessages() {
        this.messages = [];
        this.emit('chat:cleared');
    }

    /**
     * Get module state for overlay
     */
    getState() {
        return {
            name: this.name,
            messageCount: this.messages.length,
            lastMessage: this.messages[this.messages.length - 1] || null,
            status: 'active'
        };
    }

    /**
     * Cleanup
     */
    destroy() {
        this.removeAllListeners();
        this.messages = [];
        console.log(`[${this.name}] Destroyed`);
    }
}

module.exports = ChatModule;