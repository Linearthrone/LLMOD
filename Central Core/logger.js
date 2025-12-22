const fs = require('fs');
const path = require('path');

class Logger {
    constructor() {
        this.logFile = path.join(__dirname, '..', 'app.log');
        this.enableConsole = true;
        this.enableFile = true;
    }

    formatMessage(level, message) {
        const timestamp = new Date().toISOString();
        return `[${timestamp}] [${level}] ${message}`;
    }

    log(message, level = 'INFO') {
        const formattedMessage = this.formatMessage(level, message);
        
        if (this.enableConsole) {
            console.log(formattedMessage);
        }
        
        if (this.enableFile) {
            try {
                fs.appendFileSync(this.logFile, formattedMessage + '\n');
            } catch (error) {
                console.error('Failed to write to log file:', error);
            }
        }
    }

    info(message) {
        this.log(message, 'INFO');
    }

    error(message) {
        this.log(message, 'ERROR');
    }

    warn(message) {
        this.log(message, 'WARN');
    }

    debug(message) {
        this.log(message, 'DEBUG');
    }
}

module.exports = new Logger();