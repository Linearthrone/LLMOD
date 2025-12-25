#!/usr/bin/env node

/**
 * LLMOD Module Launcher
 * Starts all pure JavaScript modules and WebSocket server
 * Modules communicate with WinUI 3 overlay via WebSocket
 */

const ModuleWebSocketServer = require('./Central Core/websocket-server');
const logger = require('./Central Core/logger');
const config = require('./Central Core/config');

// Import modules
const ChatModule = require('./Modules/ChatModule/chatindex');
const ChatState = require('./Modules/ChatModule/chatstate');

// Initialize WebSocket server
const wsServer = new ModuleWebSocketServer(9001);

// Initialize modules
const modules = {};

function initializeModules() {
    logger.info('Initializing LLMOD modules...');
    
    // Chat Module
    try {
        modules.chat = new ChatModule({
            maxMessages: 100,
            ollamaUrl: config.ollama?.host || 'http://localhost:11434',
            model: config.ollama?.defaultModel || 'llama2'
        });
        
        modules.chatState = new ChatState();
        
        // Register with WebSocket server
        wsServer.registerModule('ChatModule', modules.chat);
        
        logger.info('✓ Chat Module initialized');
    } catch (error) {
        logger.error(`Failed to initialize Chat Module: ${error.message}`);
    }

    // TODO: Initialize other modules
    // modules.contacts = new ContactsModule();
    // modules.viewport = new ViewPortModule();
    // modules.systems = new SystemsModule();
    // modules.contextData = new ContextDataExchangeModule();
    // modules.appTray = new AppTrayModule();
}

function startServer() {
    logger.info('Starting LLMOD Module Server...');
    logger.info('=====================================');
    
    // Start WebSocket server
    wsServer.start();
    
    // Initialize all modules
    initializeModules();
    
    logger.info('=====================================');
    logger.info('LLMOD Module Server Started!');
    logger.info('WebSocket Server: ws://localhost:9001');
    logger.info('Waiting for WinUI 3 overlay to connect...');
    logger.info('Press Ctrl+C to stop');
    logger.info('=====================================');
}

function stopServer() {
    logger.info('Stopping LLMOD Module Server...');
    
    // Cleanup modules
    Object.values(modules).forEach(module => {
        if (module.destroy) {
            module.destroy();
        }
    });
    
    // Stop WebSocket server
    wsServer.stop();
    
    logger.info('LLMOD Module Server stopped');
    process.exit(0);
}

// Handle process termination
process.on('SIGINT', stopServer);
process.on('SIGTERM', stopServer);

// Handle uncaught errors
process.on('uncaughtException', (error) => {
    logger.error(`Uncaught exception: ${error.message}`);
    logger.error(error.stack);
});

process.on('unhandledRejection', (reason, promise) => {
    logger.error(`Unhandled rejection at: ${promise}, reason: ${reason}`);
});

// Start the server
startServer();