const EventEmitter = require('events');

class MessageBus extends EventEmitter {
    constructor() {
        super();
        this.modules = new Map();
    }

    registerModule(name, instance) {
        this.modules.set(name, instance);
        this.emit('module:registered', { name, instance });
    }

    unregisterModule(name) {
        this.modules.delete(name);
        this.emit('module:unregistered', { name });
    }

    getModule(name) {
        return this.modules.get(name);
    }

    broadcast(event, data) {
        this.emit(event, data);
    }
}

module.exports = new MessageBus();