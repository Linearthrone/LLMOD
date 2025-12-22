/**
 * ChatState - Manages persistent state for ChatModule
 * Handles message history, user preferences, and session data
 */
class ChatState {
    constructor() {
        this.messages = [];
        this.sessions = new Map();
        this.currentSession = null;
        this.preferences = {
            theme: 'navy-rust',
            fontSize: 14,
            maxHistory: 100,
            autoSave: true
        };
    }

    /**
     * Create a new chat session
     */
    createSession(sessionId = null) {
        const id = sessionId || `session_${Date.now()}`;
        const session = {
            id: id,
            created: new Date().toISOString(),
            messages: [],
            metadata: {}
        };
        
        this.sessions.set(id, session);
        this.currentSession = id;
        return session;
    }

    /**
     * Add message to current session
     */
    addMessage(message) {
        if (!this.currentSession) {
            this.createSession();
        }
        
        const session = this.sessions.get(this.currentSession);
        if (session) {
            session.messages.push(message);
            this.messages.push(message);
        }
    }

    /**
     * Get messages from current session
     */
    getSessionMessages(sessionId = null) {
        const id = sessionId || this.currentSession;
        const session = this.sessions.get(id);
        return session ? session.messages : [];
    }

    /**
     * Get all messages
     */
    getAllMessages() {
        return this.messages;
    }

    /**
     * Clear current session
     */
    clearSession() {
        if (this.currentSession) {
            const session = this.sessions.get(this.currentSession);
            if (session) {
                session.messages = [];
            }
        }
    }

    /**
     * Delete a session
     */
    deleteSession(sessionId) {
        this.sessions.delete(sessionId);
        if (this.currentSession === sessionId) {
            this.currentSession = null;
        }
    }

    /**
     * Get all sessions
     */
    getSessions() {
        return Array.from(this.sessions.values());
    }

    /**
     * Update preferences
     */
    updatePreferences(prefs) {
        this.preferences = { ...this.preferences, ...prefs };
    }

    /**
     * Get preferences
     */
    getPreferences() {
        return this.preferences;
    }

    /**
     * Export state for persistence
     */
    export() {
        return {
            messages: this.messages,
            sessions: Array.from(this.sessions.entries()),
            currentSession: this.currentSession,
            preferences: this.preferences,
            timestamp: new Date().toISOString()
        };
    }

    /**
     * Import state from persistence
     */
    import(data) {
        if (data.messages) this.messages = data.messages;
        if (data.sessions) this.sessions = new Map(data.sessions);
        if (data.currentSession) this.currentSession = data.currentSession;
        if (data.preferences) this.preferences = data.preferences;
    }

    /**
     * Reset all state
     */
    reset() {
        this.messages = [];
        this.sessions.clear();
        this.currentSession = null;
    }
}

module.exports = ChatState;