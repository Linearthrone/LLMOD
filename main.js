// Main entry point for LLMOD application
// This file is referenced in package.json as the main entry point
// For running the application, use: npm start or node start-all.js

module.exports = {
  // This can be used if LLMOD is imported as a module
  version: require('./package.json').version,
  name: require('./package.json').name,
};
