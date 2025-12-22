// Quick test to verify a single server can start
const express = require('express');
const path = require('path');

console.log('Testing basic server startup...');

try {
    // Test logger
    const logger = require('./Central Core/logger');
    console.log('✓ Logger loaded successfully');
    logger.info('Test log message');
    
    // Test config
    const config = require('./Central Core/config');
    console.log('✓ Config loaded successfully');
    console.log('  Modules configured:', Object.keys(config.modules).length);
    
    // Test express
    const app = express();
    const port = 8080;
    
    app.get('/', (req, res) => {
        res.send('Test server is running!');
    });
    
    app.get('/health', (req, res) => {
        res.json({ status: 'ok' });
    });
    
    const server = app.listen(port, () => {
        console.log(`✓ Test server started on port ${port}`);
        console.log(`  Visit: http://localhost:${port}`);
        console.log('\nPress Ctrl+C to stop');
    });
    
    process.on('SIGINT', () => {
        console.log('\nStopping test server...');
        server.close(() => {
            console.log('Test server stopped');
            process.exit(0);
        });
    });
    
} catch (error) {
    console.error('✗ Error:', error.message);
    console.error('Stack:', error.stack);
    process.exit(1);
}