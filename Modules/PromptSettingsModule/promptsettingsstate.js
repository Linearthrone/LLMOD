/**
 * Prompt Settings State Management
 * Manages the state for the Prompt Settings module
 */

class PromptSettingsState {
    constructor() {
        this.settings = {
            systemPrompt: 'You are a helpful AI assistant.',
            model: 'llama2',
            temperature: 0.7,
            maxTokens: 2048,
            topP: 0.9,
            topK: 40,
            repeatPenalty: 1.1,
            streaming: true,
            contextWindow: 4096
        };
        this.lastModified = new Date().toISOString();
    }

    /**
     * Get current settings
     * @returns {Object} Current settings
     */
    getSettings() {
        return { ...this.settings };
    }

    /**
     * Update settings
     * @param {Object} updates - Settings to update
     */
    updateSettings(updates) {
        this.settings = {
            ...this.settings,
            ...updates
        };
        this.lastModified = new Date().toISOString();
    }

    /**
     * Get specific setting
     * @param {string} key - Setting key
     * @returns {*} Setting value
     */
    getSetting(key) {
        return this.settings[key];
    }

    /**
     * Set specific setting
     * @param {string} key - Setting key
     * @param {*} value - Setting value
     */
    setSetting(key, value) {
        this.settings[key] = value;
        this.lastModified = new Date().toISOString();
    }

    /**
     * Reset to default settings
     */
    reset() {
        this.settings = {
            systemPrompt: 'You are a helpful AI assistant.',
            model: 'llama2',
            temperature: 0.7,
            maxTokens: 2048,
            topP: 0.9,
            topK: 40,
            repeatPenalty: 1.1,
            streaming: true,
            contextWindow: 4096
        };
        this.lastModified = new Date().toISOString();
    }

    /**
     * Get state for serialization
     * @returns {Object} Serializable state
     */
    getState() {
        return {
            settings: this.settings,
            lastModified: this.lastModified
        };
    }
}

module.exports = PromptSettingsState;