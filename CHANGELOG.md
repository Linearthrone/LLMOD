# Changelog

All notable changes to the LLMOD project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2024-12-22

### Added
- Created `shared-utils.js` for common utility functions across modules
- Implemented complete PromptSettingsModule with server, index, and state files
- Added comprehensive Python MCP servers:
  - AgentToolsServer: Code execution, web search, and weather tools
  - LtmMcpServer: Long-term memory storage and retrieval
  - ResourceBankServer: Resource management system
  - TtsModelsServer: Text-to-speech model management
- Added PromptSettings module to start-all.js and config.js
- Created CHANGELOG.md for tracking project changes

### Changed
- Standardized error handling across all modules using SharedUtils
- Updated pyproject.toml with correct dependencies (removed asyncio and json5)
- Improved module configuration in start-all.js
- Enhanced config.js with PromptSettings module configuration

### Removed
- Deleted empty main.js file (unused entry point)
- Removed duplicate INSTALLATION-FIXED.md (kept FIXED-INSTALLATION.md)
- Cleaned up empty Python files that had no implementation

### Fixed
- Fixed all empty Python MCP server files with proper implementations
- Standardized model fetching across modules using SharedUtils
- Improved error handling consistency across all modules
- Fixed missing PromptSettingsModule server implementation

### Security
- All Python MCP servers now have proper error handling
- Improved input validation in all server implementations

## [1.0.0] - Initial Release

### Added
- Initial LLMOD architecture with modular design
- Chat Module for AI conversations
- Contacts Module for managing AI personas
- ViewPort Module for UI management
- Systems Module for system information
- Context Data Exchange Module for data sharing
- App Tray Module for system tray integration
- Desktop overlay with Electron
- WebSocket server for module communication
- Central Core with logger, config, and bus
- Python MCP server structure