#!/usr/bin/env node

const { spawn } = require('child_process');
const path = require('path');
const fs = require('fs');

// Module configurations
const modules = [
    {
        name: 'Chat Module',
        script: 'Modules/ChatModule/server.js',
        port: 8080,
        color: '\x1b[36m' // Cyan
    },
    {
        name: 'Contacts Module',
        script: 'Modules/ContactsModule/server.js',
        port: 8081,
        color: '\x1b[35m' // Magenta
    },
    {
        name: 'ViewPort Module',
        script: 'Modules/ViewPortModule/server.js',
        port: 8082,
        color: '\x1b[33m' // Yellow
    },
    {
        name: 'Systems Module',
        script: 'Modules/SystemsModule/server.js',
        port: 8083,
        color: '\x1b[32m' // Green
    },
    {
        name: 'Context Data Exchange Module',
        script: 'Modules/ContextDataExchangeModule/server.js',
        port: 8084,
        color: '\x1b[34m' // Blue
    },
    {
        name: 'App Tray Module',
        script: 'Modules/AppTray/server.js',
        port: 8085,
        color: '\x1b[31m' // Red
    },
    {
        name: 'Prompt Settings Module',
        script: 'Modules/PromptSettingsModule/server.js',
        port: 8086,
        color: '\x1b[95m' // Bright Magenta
    }
];

const runningProcesses = new Map();

function startModule(module) {
    return new Promise((resolve, reject) => {
        console.log(`${module.color}Starting ${module.name} on port ${module.port}...\x1b[0m`);
        
        const process = spawn('node', [module.script], {
            stdio: ['pipe', 'pipe', 'pipe'],
            cwd: __dirname
        });

        runningProcesses.set(module.name, process);

        process.stdout.on('data', (data) => {
            console.log(`${module.color}[${module.name}]\x1b[0m ${data.toString().trim()}`);
        });

        process.stderr.on('data', (data) => {
            console.error(`${module.color}[${module.name} ERROR]\x1b[0m ${data.toString().trim()}`);
        });

        process.on('close', (code) => {
            if (code !== 0) {
                console.error(`${module.color}[${module.name}] Process exited with code ${code}\x1b[0m`);
                reject(new Error(`${module.name} failed to start`));
            } else {
                console.log(`${module.color}[${module.name}] Process exited successfully\x1b[0m`);
                runningProcesses.delete(module.name);
                resolve();
            }
        });

        process.on('error', (error) => {
            console.error(`${module.color}[${module.name}] Failed to start: ${error.message}\x1b[0m`);
            reject(error);
        });

        // Give the process time to start
        setTimeout(() => {
            if (runningProcesses.has(module.name)) {
                console.log(`${module.color}✓ ${module.name} started successfully\x1b[0m`);
                resolve();
            }
        }, 2000);
    });
}

async function startAllModules() {
    console.log('\x1b[1;36m====================================\x1b[0m');
    console.log('\x1b[1;36m   Starting LLMOD Application\x1b[0m');
    console.log('\x1b[1;36m====================================\x1b[0m');
    console.log();

    try {
        // Start all modules
        for (const module of modules) {
            try {
                await startModule(module);
                // Small delay between starting modules
                await new Promise(resolve => setTimeout(resolve, 1000));
            } catch (error) {
                console.error(`\x1b[31mFailed to start ${module.name}: ${error.message}\x1b[0m`);
                // Continue with other modules even if one fails
            }
        }

        console.log();
        console.log('\x1b[1;32m====================================\x1b[0m');
        console.log('\x1b[1;32m   LLMOD Application Started!\x1b[0m');
        console.log('\x1b[1;32m====================================\x1b[0m');
        console.log();
        console.log('\x1b[1mAccess your modules at:\x1b[0m');
        
        modules.forEach(module => {
            console.log(`${module.color}• ${module.name}: http://localhost:${module.port}\x1b[0m`);
        });

        console.log();
        console.log('\x1b[1mApp Tray (Main Control Panel):\x1b[0m');
        console.log('\x1b[31m• http://localhost:8085\x1b[0m');
        console.log();
        console.log('\x1b[33mPress Ctrl+C to stop all modules\x1b[0m');
        console.log();

    } catch (error) {
        console.error('\x1b[31mFailed to start application:\x1b[0m', error.message);
        stopAllModules();
        process.exit(1);
    }
}

function stopAllModules() {
    console.log('\n\x1b[33mStopping all modules...\x1b[0m');
    
    runningProcesses.forEach((process, moduleName) => {
        try {
            process.kill('SIGTERM');
            console.log(`\x1b[32m✓ Stopped ${moduleName}\x1b[0m`);
        } catch (error) {
            console.error(`\x1b[31mFailed to stop ${moduleName}: ${error.message}\x1b[0m`);
        }
    });
    
    runningProcesses.clear();
}

// Handle process termination
process.on('SIGINT', () => {
    stopAllModules();
    process.exit(0);
});

process.on('SIGTERM', () => {
    stopAllModules();
    process.exit(0);
});

// Start the application
startAllModules().catch(error => {
    console.error('\x1b[31mApplication startup failed:\x1b[0m', error);
    process.exit(1);
});