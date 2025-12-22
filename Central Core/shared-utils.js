/**
 * Shared Utilities for LLMOD Modules
 * Common functions used across multiple modules
 */

const logger = require('./logger');

class SharedUtils {
    /**
     * Fetch available models from Ollama
     * @returns {Promise<Array>} Array of available models
     */
    static async fetchOllamaModels() {
        try {
            const response = await fetch('http://localhost:11434/api/tags');
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            const data = await response.json();
            return data.models || [];
        } catch (error) {
            logger.error('Failed to fetch Ollama models:', error.message);
            return [];
        }
    }

    /**
     * Standard error handler for Express routes
     * @param {Error} error - The error object
     * @param {Object} res - Express response object
     * @param {string} message - Custom error message
     */
    static handleError(error, res, message = 'An error occurred') {
        logger.error(`${message}:`, error.message);
        res.status(500).json({ 
            error: message,
            details: error.message 
        });
    }

    /**
     * Validate required fields in request body
     * @param {Object} body - Request body
     * @param {Array<string>} requiredFields - Array of required field names
     * @returns {Object} { valid: boolean, missing: Array<string> }
     */
    static validateRequiredFields(body, requiredFields) {
        const missing = requiredFields.filter(field => !body[field]);
        return {
            valid: missing.length === 0,
            missing
        };
    }

    /**
     * Create a standardized health check response
     * @param {string} serviceName - Name of the service
     * @param {Object} additionalData - Additional data to include
     * @returns {Object} Health check response
     */
    static createHealthResponse(serviceName, additionalData = {}) {
        return {
            service: serviceName,
            status: 'healthy',
            timestamp: new Date().toISOString(),
            ...additionalData
        };
    }

    /**
     * Setup standard middleware for Express app
     * @param {Object} app - Express app instance
     * @param {string} moduleName - Name of the module for logging
     */
    static setupStandardMiddleware(app, moduleName) {
        const express = require('express');
        
        app.use(express.json());
        app.use((req, res, next) => {
            logger.log(`[${moduleName}] ${req.method} ${req.path}`);
            next();
        });
    }

    /**
     * Create a standardized settings object
     * @param {Object} defaults - Default settings
     * @returns {Object} Settings object
     */
    static createDefaultSettings(defaults = {}) {
        return {
            model: 'llama2',
            temperature: 0.7,
            maxTokens: 2048,
            streaming: true,
            ...defaults
        };
    }

    /**
     * Format timestamp for consistent display
     * @param {Date|string} date - Date to format
     * @returns {string} Formatted timestamp
     */
    static formatTimestamp(date = new Date()) {
        return new Date(date).toISOString();
    }

    /**
     * Paginate array results
     * @param {Array} array - Array to paginate
     * @param {number} limit - Items per page
     * @param {number} offset - Starting offset
     * @returns {Object} { items: Array, total: number, hasMore: boolean }
     */
    static paginate(array, limit = 50, offset = 0) {
        const items = array.slice(offset, offset + limit);
        return {
            items,
            total: array.length,
            hasMore: offset + limit < array.length,
            limit,
            offset
        };
    }
}

module.exports = SharedUtils;