module.exports = {
    ollama: {
        host: 'http://localhost:11434',
        defaultModel: 'llama2'
    },
    mcp: {
        servers: {
            ltm: { port: 3001 },
            resourceBank: { port: 3002 },
            agentTools: { port: 3003 },
            tts: { port: 3004 }
        }
    },
    modules: {
        chat: {
            port: 8080,
            script: './Modules/ChatModule/server.js',
            command: 'node',
            args: ['server.js'],
            healthCheck: '/health'
        },
        contacts: {
            port: 8081,
            script: './Modules/ContactsModule/server.js',
            command: 'node',
            args: ['server.js'],
            healthCheck: '/health'
        },
        viewport: {
            port: 8082,
            script: './Modules/ViewPortModule/server.js',
            command: 'node',
            args: ['server.js'],
            healthCheck: '/health'
        },
        systems: {
            port: 8083,
            script: './Modules/SystemsModule/server.js',
            command: 'node',
            args: ['server.js'],
            healthCheck: '/health'
        },
        contextDataExchange: {
            port: 8084,
            script: './Modules/ContextDataExchangeModule/server.js',
            command: 'node',
            args: ['server.js'],
            healthCheck: '/health'
        },
        appTray: {
            port: 8085,
            script: './Modules/AppTray/server.js',
            command: 'node',
            args: ['server.js'],
            healthCheck: '/health'
        }
    },
    orchestrator: {
        port: 9000,
        discovery: true,
        healthCheckInterval: 10000
    }
};