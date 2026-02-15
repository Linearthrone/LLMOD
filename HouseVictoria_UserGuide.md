# House Victoria - User Guide

**Version:** 1.0  
**Last Updated:** January 2025

---

## Table of Contents

### Getting Started
1. [Introduction](#introduction)
2. [Installation & Setup](#installation--setup)
3. [First Launch](#first-launch)
4. [Understanding the Interface](#understanding-the-interface)

### Core Features
5. [Main Tray (Bottom Right)](#main-tray-bottom-right)
6. [Top Tray (Auto-hiding)](#top-tray-auto-hiding)
7. [System Monitor Drawer](#system-monitor-drawer)

### Communication
8. [SMS/MMS Chat Window](#smsmms-chat-window)
   - [Starting Conversations](#starting-conversations)
   - [Sending Messages](#sending-messages)
   - [Sending Media Attachments](#sending-media-attachments)
   - [Viewing Media](#viewing-media)
   - [Managing Conversations](#managing-conversations)

### AI Management
9. [AI Models & Personas Window](#ai-models--personas-window)
   - [Creating AI Personas](#creating-ai-personas)
   - [Configuring LLM Parameters](#configuring-llm-parameters)
   - [Loading Models](#loading-models)
   - [Pulling Models from Ollama](#pulling-models-from-ollama)
   - [Editing Personas](#editing-personas)
   - [Deleting Personas](#deleting-personas)

### Project Management
10. [Projects Window](#projects-window)
    - [Creating Projects](#creating-projects)
    - [Viewing Project List](#viewing-project-list)
    - [Filtering and Sorting Projects](#filtering-and-sorting-projects)
    - [Viewing Project Details](#viewing-project-details)
    - [Managing Roadblocks](#managing-roadblocks)
    - [Managing Artifacts](#managing-artifacts)
    - [Viewing AI Collaboration Logs](#viewing-ai-collaboration-logs)
    - [Editing Projects](#editing-projects)
    - [Deleting Projects](#deleting-projects)

### Settings & Configuration
11. [Settings Window](#settings-window)
    - [LLM Server Settings](#llm-server-settings)
    - [MCP Server Settings](#mcp-server-settings)
    - [TTS Settings](#tts-settings)
    - [Virtual Environment Settings](#virtual-environment-settings)
    - [Overlay Settings](#overlay-settings)
    - [Avatar Settings](#avatar-settings)
    - [Locomotion Settings](#locomotion-settings)
    - [Tools Configuration](#tools-configuration)
    - [Persistent Memory Configuration](#persistent-memory-configuration)
    - [Importing and Exporting Settings](#importing-and-exporting-settings)
    - [Resetting Settings](#resetting-settings)

### Utilities
12. [Global Log Directory Window](#global-log-directory-window)
13. [Generated Files](#generated-files)

### Tips & Troubleshooting
14. [Keyboard Shortcuts](#keyboard-shortcuts)
15. [Common Tasks](#common-tasks)
16. [Troubleshooting](#troubleshooting)
17. [FAQ](#faq)

---

## Introduction

House Victoria is a modular overlay desktop application that provides:

- **AI Chat Interface**: Communicate with AI personas through SMS/MMS-style messaging
- **Project Management**: Organize and track your goals and projects with AI collaboration
- **System Monitoring**: Real-time monitoring of CPU, RAM, and system health
- **File Management**: Drag-and-drop file processing and data bank organization
- **Virtual Environment Integration**: Connect to Unreal Engine for 3D interactions (optional)

The application runs as an overlay on your desktop, similar to Xbox Game Bar, allowing you to access its features without interrupting your workflow.

---

## Installation & Setup

### Prerequisites

Before using House Victoria, ensure you have:

1. **.NET 8.0 Runtime** installed
2. **Ollama** installed and running (default: `http://localhost:11434`)
3. **MCP Server** running (default: `http://localhost:8080`)
4. **Windows 10/11** operating system

### Installation Steps

1. Run the `install.bat` script to set up the application
2. The application will be installed and configured automatically
3. Ensure Ollama is running before launching House Victoria

### First-Time Configuration

On first launch, you'll need to:

1. Configure your Ollama endpoint in Settings (usually `http://localhost:11434`)
2. Configure your MCP Server endpoint (usually `http://localhost:8080`)
3. Test connections to ensure services are reachable
4. Create your first AI persona to start chatting

---

## First Launch

When you first launch House Victoria:

1. **Main Tray** appears in the bottom-right corner of your screen
2. **Top Tray** appears at the top edge (auto-hides when not in use)
3. **System Monitor Drawer** appears on the left edge (can be toggled)

The application runs in the background and can be minimized to the system tray.

---

## Understanding the Interface

House Victoria uses three main overlay components:

### 1. Main Tray (Bottom Right)
- Always visible glass panel
- Three buttons: SMS/MMS, AI Models, Settings
- Click-through support (won't interfere with other applications)

### 2. Top Tray (Top Edge)
- Auto-hides when not in use
- Drag-and-drop file processing
- Quick access to generated files, logs, and projects

### 3. System Monitor Drawer (Left Edge)
- Toggle open/close
- Real-time system metrics
- Server status and controls

---

## Main Tray (Bottom Right)

The Main Tray is your primary access point to House Victoria's features.

### Buttons

1. **SMS/MMS Button** 📱
   - Opens the chat window
   - Access all conversations
   - Start new conversations with contacts

2. **AI Models Button** 🤖
   - Opens AI Models & Personas window
   - Manage AI personas
   - Load and configure models

3. **Settings Button** ⚙️
   - Opens Settings window
   - Configure all application settings
   - Test service connections

### Window Management

- Windows opened from the Main Tray can be minimized to the system tray
- Right-click the system tray icon to restore minimized windows
- Windows remember their position and size

---

## Top Tray (Auto-hiding)

The Top Tray provides quick access to file processing and utilities.

### Features

#### Drag-and-Drop File Upload
1. Drag files from Windows Explorer onto the Top Tray
2. Supported file types:
   - Text files: `.txt`, `.md`, `.json`, `.xml`, `.csv`, `.log`
   - Code files: `.cs`, `.js`, `.py`, `.html`, `.css`
   - Binary files: Any file type (metadata stored)
3. Files are automatically processed and stored in a "Dropped Files" data bank
4. Processing results are displayed with error reporting

#### Generated Files Button 📁
- Opens the folder containing AI-generated files
- Shows message if no files are available
- Files are organized by generation date

#### Global Log Directory Button 📋
- Opens the Global Log Directory window
- View categorized log entries
- Export logs in various formats

#### Projects/Goals Button 📊
- Opens the Projects Window
- Quick access to project management

### Auto-Hide Behavior

- The Top Tray automatically hides when not in use
- Hover near the top edge to reveal it
- Auto-hide delay can be configured in Settings

---

## System Monitor Drawer

The System Monitor Drawer provides real-time system information and server management.

### Opening/Closing

- Click the toggle button on the left edge to open/close
- Drawer slides in from the left
- Can be configured to auto-hide in Settings

### System Metrics

**Real-Time Monitoring:**
- **CPU Usage**: Current CPU utilization percentage
- **RAM Usage**: Current memory usage and available memory
- **CPU Temperature**: Temperature in Celsius (via WMI)
- **System Uptime**: How long your system has been running

**Status Indicators:**
- **Primary AI**: Currently active AI persona
- **Current AI Contact**: AI contact in active conversation
- **Virtual Environment**: Connection status to Unreal Engine

### Server Management

**Server Status List:**
- **Ollama**: LLM server status
- **MCP Server**: Model Context Protocol server status
- **TTS**: Text-to-speech service status
- **Unreal Engine**: Virtual environment connection status

**Server Controls:**
- **Restart**: Restart a stopped server
- **Stop**: Stop a running server
- Status indicators show connection state
- Circuit breaker pattern prevents repeated connection attempts to unreachable servers

### Branding

The drawer features "House Victoria" calligraphy branding at the bottom.

---

## SMS/MMS Chat Window

The SMS/MMS Chat Window is your primary interface for communicating with contacts (both human and AI).

### Window Layout

- **Left Panel**: Conversation list (all your conversations)
- **Center Panel**: Message view (messages in selected conversation)
- **Right Panel**: Contact selection (start new conversations)
- **Bottom**: Message input area with attachment button

### Starting Conversations

1. Click a contact in the **Contact Selection** panel (right side)
2. If it's an AI contact, the persona loads automatically
3. A new conversation starts, or an existing conversation opens
4. Type your message and press Enter to send

### Sending Messages

**Text Messages:**
1. Type your message in the input area at the bottom
2. Press **Enter** to send
3. Press **Shift+Enter** for a new line
4. Messages appear immediately in the conversation (optimistic update)

**Message Types:**
- **Text**: Standard text messages
- **Image**: Image attachments (jpg, jpeg, png, gif, bmp)
- **Video**: Video attachments (mp4, avi, mov, wmv)
- **Audio**: Audio attachments (mp3, wav, ogg)
- **Document**: Document attachments (pdf, doc, docx, txt)

### Sending Media Attachments

1. Click the **attachment button** (📎) in the message input area
2. Select file type filter:
   - **Images**: jpg, jpeg, png, gif, bmp
   - **Videos**: mp4, avi, mov, wmv
   - **Audio**: mp3, wav, ogg
   - **Documents**: pdf, doc, docx, txt
   - **All Files**: Any file type
3. Select your file (maximum 50MB)
4. A preview appears showing:
   - Filename
   - File size
   - File type indicator
5. Click the **remove button** (×) to clear the attachment before sending
6. Type your message (optional) and press Enter to send

**File Size Limits:**
- Maximum file size: **50MB**
- Error messages appear if file is too large
- Large files are stored on disk; small images (<10MB) are cached in memory

### Viewing Media

**In Message Bubbles:**
- **Images**: Thumbnail preview (max 250x200px)
- **Videos**: Placeholder with play icon and filename
- **Audio/Documents**: Icon with filename

**Opening Full Media:**
- Click on any media preview in a message bubble
- Images open in default image viewer
- Videos open in default video player
- Documents open in default application
- Audio files open in default audio player

### Managing Conversations

**Conversation List:**
- Shows all conversations sorted by last message time
- Displays:
  - Contact name
  - Last message preview
  - Timestamp
- Click a conversation to open it

**Message View:**
- Displays last 100 messages in the conversation
- Incoming messages on the left
- Outgoing messages on the right
- Timestamps shown for each message
- Auto-scrolls to latest message

**Window Features:**
- **Minimize**: Minimize to system tray
- **Restore**: Restore from system tray icon
- **Resizable**: Drag edges to resize (maintains phone-like aspect ratio)
- **Positioned**: Automatically positioned to avoid Main Tray

### AI Response Handling

When chatting with AI contacts:
- AI responses are generated automatically
- **Timeout**: 5-minute timeout for AI responses (configurable)
- **Error Messages**: User-friendly error messages if timeout occurs
- **Suggestions**: Guidance on reducing timeouts (reduce MaxTokens, context length, or try different model)

---

## AI Models & Personas Window

The AI Models & Personas Window allows you to create and manage AI personas for conversations.

### Window Layout

- **Contact Book View**: List of all AI personas
- **Create Persona Button**: Create new AI persona
- **Load Model Button**: Load models from Ollama
- **Pull Model Button**: Download models from Ollama repository

### Creating AI Personas

1. Click **"Create Persona"** button
2. Fill in the persona details:
   - **Name**: Display name for the persona
   - **Model**: Select from available Ollama models
   - **System Prompt**: Instructions for the AI's behavior
   - **Description**: Brief description of the persona
3. **MCP Server Configuration** (optional):
   - Configure MCP Server endpoint
   - Enables autonomous agent capabilities
4. **LLM Parameters** (Advanced):
   - **Temperature** (0.0-2.0, default: 0.7): Controls randomness
   - **TopP** (0.0-1.0, default: 0.9): Nucleus sampling parameter
   - **TopK** (default: 40): Top-K sampling
   - **Repeat Penalty** (default: 1.1): Reduces repetition
   - **Max Tokens** (-1 for unlimited, default: -1): Maximum response length
   - **Context Length** (default: 4096): Conversation context window
5. Click **"Create"** to save the persona

**Validation:**
- Model must be available in Ollama
- Name must be unique
- Data path is created automatically
- MCP server is initialized if configured

### Configuring LLM Parameters

LLM parameters control how the AI generates responses:

- **Temperature**: Higher values (1.0-2.0) = more creative/random, Lower values (0.0-0.5) = more focused/deterministic
- **TopP**: Controls diversity via nucleus sampling
- **TopK**: Limits token selection to top K tokens
- **Repeat Penalty**: Reduces repetitive text (values >1.0)
- **Max Tokens**: Limits response length (-1 = unlimited)
- **Context Length**: How much conversation history to remember

**Tips:**
- Lower temperature for factual/task-oriented conversations
- Higher temperature for creative writing or brainstorming
- Adjust MaxTokens if responses are cut off or too long
- Increase Context Length for longer conversations

### Loading Models

1. Click **"Load Model"** button
2. View available models from Ollama
3. Select a model to load into memory
4. Model status is tracked and displayed
5. Loaded models are available for persona creation

### Pulling Models from Ollama

1. Click **"Pull Model"** button
2. Enter model name (e.g., `llama2`, `mistral`, `codellama`)
3. Click **"Pull"** to download from Ollama repository
4. Progress status is displayed
5. Available models list refreshes after pull completes
6. **Timeout**: 30 minutes for large model downloads

**Model Names:**
- Use format: `modelname` or `modelname:tag`
- Examples: `llama2`, `mistral:7b`, `codellama:13b`

### Editing Personas

1. Select a persona in the Contact Book
2. Click **"Edit"** button
3. **Edit System Prompt Dialog** opens
4. Modify the system prompt
5. Click **"Save"** to update
6. Changes are persisted immediately

**Note**: Only the system prompt can be edited. To change other settings, delete and recreate the persona.

### Deleting Personas

1. Select a persona in the Contact Book
2. Click **"Delete"** button
3. Confirm deletion
4. Persona is removed from contact book
5. Persistence data is cleaned up

**Warning**: Deletion cannot be undone. All conversation history with the persona is preserved.

---

## Projects Window

The Projects Window helps you organize and track your goals and projects with AI collaboration.

### Window Layout

- **Top Bar**: Filter and sort controls
- **Project List**: Card-based display of all projects
- **Create Button**: Create new project/goal

### Creating Projects

1. Click **"Create New Goal/Project"** button
2. Fill in project details:
   - **Name**: Project name
   - **Type**: Development, Research, Personal, Business, Other
   - **Description**: Project description
   - **Priority**: Slider from 1-10 (1 = lowest, 10 = highest)
   - **Start Date**: Project start date (DatePicker)
   - **Deadline**: Project deadline (DatePicker)
   - **Phase**: Initial phase (Planning, InProgress, Review, Completed, OnHold, Cancelled)
   - **Assigned AI Contact**: Select AI persona to collaborate with
   - **Initial Roadblocks**: Add roadblocks (optional)
3. Click **"Create"** to save
4. Project appears in the list immediately

### Viewing Project List

**Project Cards Display:**
- **Priority Indicator**: Color-coded (red = high, orange = medium, green = low)
- **Project Type Badge**: Development, Research, Personal, etc.
- **Phase Badge**: Current project phase
- **Deadline**: Days remaining or overdue
- **Completion Percentage**: Progress bar
- **Roadblocks Count**: Number of active roadblocks
- Click a card to open project details

### Filtering and Sorting Projects

**Filter Options:**
- **By Type**: All, Development, Research, Personal, Business, Other
- **By Phase**: All, Planning, InProgress, Review, Completed, OnHold, Cancelled

**Sort Options:**
- **By Name**: A-Z or Z-A
- **By Priority**: High-Low or Low-High
- **By Deadline**: Soonest or Latest
- **By Completion**: High-Low or Low-High

Filters and sorts update the project list in real-time.

### Viewing Project Details

Click any project card to open the **Project Detail Dialog**.

**Overview Tab:**
- **Project Information**: Name, description, type, priority
- **Phase Management**: Dropdown to change phase
- **Timeline**: Start date, deadline, created date
- **Completion**: Progress bar and percentage
- **Assigned AI**: Current AI contact
- **Roadblocks Summary**: Count and list
- **Statistics Panel**:
  - Artifacts count
  - AI interactions count
  - Days remaining

**Roadblocks Tab:**
- **Add Roadblock**: Text input to add new roadblocks
- **Roadblocks List**: All roadblocks with remove buttons
- **Edit Mode**: Toggle to enable/disable editing

**Artifacts Tab:**
- **Upload Artifact**: File picker to upload files
- **Artifacts List**: All uploaded files with:
  - Type icons
  - File size
  - Description
  - Preview, Download, Delete buttons

**AI Collaboration Logs Tab:**
- **Filter by AI Contact**: Dropdown to filter logs
- **Search**: Text search in logs
- **Timeline View**: Chronological list of AI interactions
- **Log Details**: Action, details, timestamp, performer
- **Clear Search**: Reset search filter

### Managing Roadblocks

**Adding Roadblocks:**
1. Open project details
2. Go to **Roadblocks Tab**
3. Type roadblock description in input field
4. Press Enter or click Add
5. Roadblock appears in list

**Removing Roadblocks:**
1. Open project details
2. Go to **Roadblocks Tab**
3. Click **Remove** button next to roadblock
4. Roadblock is deleted

### Managing Artifacts

**Uploading Artifacts:**
1. Open project details
2. Go to **Artifacts Tab**
3. Click **"Upload Artifact"** button
4. Select file(s) to upload
5. Files appear in artifacts list

**Viewing Artifacts:**
- Click **Preview** to view file
- Click **Download** to save file
- File type icons indicate file format
- File size and description displayed

**Deleting Artifacts:**
1. Open project details
2. Go to **Artifacts Tab**
3. Click **Delete** button next to artifact
4. Confirm deletion
5. File is removed

### Viewing AI Collaboration Logs

1. Open project details
2. Go to **AI Collaboration Logs Tab**
3. View timeline of all AI interactions
4. **Filter by AI Contact**: Select specific AI from dropdown
5. **Search**: Type to search log content
6. Logs show:
   - Action performed
   - Details
   - Timestamp
   - AI contact that performed action

### Editing Projects

1. Open project details
2. Click **"Edit"** button (toggles edit mode)
3. Modify:
   - Name
   - Description
   - Type
   - Priority (slider)
4. Click **"Save"** to apply changes
5. Click **"Cancel"** to discard changes

**Phase Changes:**
- Use dropdown in Overview tab to change phase
- Changes are saved immediately

### Deleting Projects

1. Open project details
2. Click **"Delete"** button
3. Confirm deletion in dialog
4. Project is permanently deleted
5. All associated data (roadblocks, artifacts, logs) is removed

**Warning**: Deletion cannot be undone.

---

## Settings Window

The Settings Window allows you to configure all aspects of House Victoria.

### Window Layout

Settings are organized into collapsible sections:
- LLM Server Settings
- MCP Server Settings
- TTS Settings
- Virtual Environment Settings
- Overlay Settings
- Avatar Settings
- Locomotion Settings
- Tools Configuration
- Persistent Memory Configuration

### LLM Server Settings

**Ollama Configuration:**
- **Endpoint**: Ollama server URL (default: `http://localhost:11434`)
- **Test Connection**: Button to test Ollama connectivity
- **Status Indicator**: Shows ✓ Connected, ✗ Failed, or Testing...

**Connection Testing:**
1. Enter Ollama endpoint URL
2. Click **"Test Connection"** button
3. Status indicator updates:
   - **Green (✓)**: Connection successful
   - **Red (✗)**: Connection failed (error details shown)
   - **Orange**: Testing in progress

### MCP Server Settings

**MCP Server Configuration:**
- **Endpoint**: MCP Server URL (default: `http://localhost:8080`)
- **Test Connection**: Button to test MCP Server connectivity
- **Status Indicator**: Connection status

**Purpose**: MCP Server enables autonomous agent capabilities for AI personas.

### TTS Settings

**Text-to-Speech Configuration:**
- **Endpoint**: TTS service URL (default: `http://localhost:5000`)
- **Test Connection**: Button to test TTS service
- **Status Indicator**: Connection status

**Note**: TTS is optional. Configure only if you have a TTS service running.

### Virtual Environment Settings

**Unreal Engine Configuration:**
- **Endpoint**: WebSocket URL (default: `ws://localhost:8888`)
- **Test Connection**: Button to test WebSocket connection
- **Status Indicator**: Connection status

**Purpose**: Connect to Unreal Engine for 3D virtual environment interactions.

**Note**: Unreal Engine integration is optional and requires Unreal Engine with WebSocket plugin.

### Overlay Settings

**Overlay Behavior:**
- **Enable Overlay**: Toggle to enable/disable overlay functionality
- **Opacity**: Slider (0.1-1.0) to control overlay transparency
- **Auto-Hide Trays**: Toggle to enable/disable auto-hide for trays
- **Auto-Hide Delay**: Milliseconds (0-60000) before trays auto-hide

**Validation:**
- Opacity must be between 0.1 and 1.0
- Auto-hide delay must be between 0 and 60000ms
- Error messages appear for invalid values

### Avatar Settings

**Avatar Configuration:**
- **Model Path**: Path to avatar 3D model file
- **Voice Model**: Voice model identifier
- **Voice Speed**: Slider (0.1-3.0) for speech speed
- **Voice Pitch**: Slider (0.1-3.0) for voice pitch

**Validation:**
- Voice Speed and Pitch must be between 0.1 and 3.0
- Error messages for invalid values

### Locomotion Settings

**Movement Parameters:**
- **Walk Speed**: Slider (0.1-10.0) for walking speed
- **Run Speed**: Slider (0.1-20.0) for running speed
- **Jump Height**: Slider (0.1-10.0) for jump height
- **Enable Physics Interaction**: Checkbox to enable physics

**Validation:**
- All speed/height values have specific ranges
- Error messages for invalid values

### Tools Configuration

**Available Tools:**
- **File System Access**: Checkbox to enable file system operations
- **Network Access**: Checkbox to enable network operations
- **System Commands**: Checkbox to enable system command execution

**Purpose**: Control which tools AI personas can use for autonomous operations.

### Persistent Memory Configuration

**Memory Settings:**
- **Enable Memory**: Toggle to enable persistent memory
- **Memory Path**: Directory path for memory storage
- **Max Entries**: Maximum memory entries (1-1000000)
- **Importance Threshold**: Slider (0.0-1.0) for memory importance filtering
- **Retention Days**: Days to retain memories (1-3650)

**Validation:**
- Max Entries: 1-1000000
- Importance Threshold: 0.0-1.0
- Retention Days: 1-3650
- Error messages for invalid values

### Importing and Exporting Settings

**Export Settings:**
1. Click **"Export Settings"** button
2. Choose save location
3. Settings are exported as JSON file
4. File can be shared or backed up

**Import Settings:**
1. Click **"Import Settings"** button
2. Select JSON settings file
3. Settings are imported and applied
4. Validation ensures imported settings are valid

**Format**: Settings are exported/imported as JSON for easy editing and sharing.

### Resetting Settings

1. Click **"Reset to Defaults"** button
2. Confirmation dialog appears
3. Click **"Yes"** to confirm
4. All settings reset to default values
5. Changes are saved immediately

**Warning**: Reset cannot be undone. Export your settings first if you want to restore them later.

### Settings Management

**Saving Settings:**
- Settings are automatically saved to `App.config` file
- No manual save button needed
- Changes persist across application restarts

**Validation:**
- Real-time validation with error messages
- Invalid values are highlighted
- Settings cannot be saved with invalid values

---

## Global Log Directory Window

The Global Log Directory Window provides access to all application logs.

### Opening the Window

- Click **"Global Log Directory"** button in Top Tray
- Or access from other windows that reference logs

### Window Layout

- **Left Panel**: Hierarchical log categories (tree view)
- **Right Panel**: Log entry details
- **Top Bar**: Actions (Refresh, Mark All Read, Export)

### Viewing Logs

**Category Navigation:**
- Expand categories in tree view
- Categories and subcategories organize logs
- **Unread Count Badges**: Show number of unread logs per category
- **Total Count**: Total logs in selected category

**Log Entry Details:**
- **Title**: Log entry title
- **Timestamp**: When the log was created
- **Severity**: Log level (Info, Warning, Error, etc.)
- **Source**: Component that created the log
- **Summary**: Brief summary of the log
- **Full Content**: Complete log message
- **Tags**: Associated tags
- **Read/Unread Status**: Visual indicator

### Log Actions

**Refresh Logs:**
- Click **"Refresh"** button
- Reloads logs from storage
- Updates unread counts

**Mark All as Read:**
- Click **"Mark All as Read"** button
- Marks all logs in current category as read
- Updates unread count badges

**Auto-Mark as Read:**
- Logs are automatically marked as read when selected
- No manual action needed

**Export Logs:**
1. Click **"Export"** button
2. Choose export format:
   - **TXT**: Plain text format
   - **JSON**: Structured JSON format
   - **CSV**: Comma-separated values
3. Choose save location
4. Logs are exported to selected file

### Log Categories

Logs are organized into categories such as:
- Application Events
- AI Interactions
- System Events
- Errors and Warnings
- User Actions

Each category may have subcategories for detailed organization.

---

## Generated Files

AI-generated files are stored in a dedicated folder accessible from the Top Tray.

### Accessing Generated Files

1. Click **"Generated Files"** button in Top Tray
2. Windows Explorer opens to the generated files folder
3. Files are organized by generation date/time

### File Organization

- Files are stored with timestamps in filenames
- Organized by date for easy browsing
- File types vary based on generation type (text, images, code, etc.)

### No Files Available

If no files have been generated:
- Button shows a message: "No generated files available"
- Folder may be empty or not yet created

---

## Keyboard Shortcuts

### General
- **Enter**: Send message (in SMS/MMS window)
- **Shift+Enter**: New line (in message input)
- **Escape**: Close dialogs or cancel operations

### Window Management
- **Minimize**: Click minimize button (minimizes to system tray)
- **Restore**: Right-click system tray icon → Restore window

### Navigation
- **Click**: Select items, open dialogs
- **Double-click**: Open project details (in Projects window)

---

## Common Tasks

### Quick Start: Chatting with AI

1. Launch House Victoria
2. Click **SMS/MMS** button in Main Tray
3. Click **"Create Persona"** in AI Models window (if no personas exist)
4. Create an AI persona with a model
5. Select the persona in SMS/MMS window
6. Type a message and press Enter
7. AI responds automatically

### Setting Up a New Project

1. Click **Projects/Goals** button in Top Tray
2. Click **"Create New Goal/Project"**
3. Fill in project details:
   - Name, type, description
   - Set priority (1-10)
   - Set start date and deadline
   - Select phase
   - Assign AI contact (optional)
4. Click **"Create"**
5. Project appears in list

### Uploading Files to Data Bank

1. Drag files from Windows Explorer
2. Drop onto Top Tray
3. Files are processed automatically
4. "Dropped Files" data bank is created
5. Processing results are displayed

### Configuring Services

1. Click **Settings** button in Main Tray
2. Enter service endpoints:
   - Ollama: `http://localhost:11434`
   - MCP Server: `http://localhost:8080`
3. Click **"Test Connection"** for each service
4. Verify status indicators show ✓ Connected
5. Settings are saved automatically

### Changing AI Persona Behavior

1. Open **AI Models & Personas** window
2. Select persona
3. Click **"Edit"** to modify system prompt
4. Or adjust LLM parameters when creating/editing
5. Changes take effect in next conversation

---

## Troubleshooting

### AI Responses Timing Out

**Problem**: AI responses take too long or timeout.

**Solutions:**
1. **Reduce Max Tokens**: Lower the MaxTokens parameter in persona settings
2. **Reduce Context Length**: Decrease context window size
3. **Try Different Model**: Use a smaller/faster model
4. **Check Ollama**: Ensure Ollama is running and responsive
5. **Check System Resources**: Ensure CPU/RAM are not maxed out

**Error Messages**: Timeout errors include suggestions for resolution.

### Services Not Connecting

**Problem**: Connection test fails for Ollama, MCP Server, etc.

**Solutions:**
1. **Verify Service is Running**: Check if service is actually running
2. **Check Endpoint URL**: Ensure URL is correct (e.g., `http://localhost:11434`)
3. **Check Firewall**: Windows Firewall may be blocking connections
4. **Check Port**: Ensure port is not in use by another application
5. **Restart Service**: Restart the service and try again

**Status Indicators**: Red (✗) indicates connection failure. Check error message for details.

### Files Not Processing

**Problem**: Files dropped on Top Tray are not processed.

**Solutions:**
1. **Check File Type**: Ensure file type is supported
2. **Check File Size**: Very large files may take time
3. **Check Permissions**: Ensure write permissions for data directory
4. **Check Logs**: View Global Log Directory for error messages
5. **Try Again**: Sometimes a retry resolves temporary issues

### Projects Window Not Loading

**Problem**: Projects window crashes or doesn't open.

**Solutions:**
1. **Restart Application**: Close and restart House Victoria
2. **Check Database**: Database may be corrupted (check logs)
3. **Clear Cache**: Delete temporary files if needed
4. **Check Logs**: View error logs in Global Log Directory

### System Monitor Not Showing Metrics

**Problem**: CPU/RAM metrics show 0% or incorrect values.

**Solutions:**
1. **Refresh**: Click refresh or wait for next update (500ms interval)
2. **Check Permissions**: Application may need elevated permissions
3. **Restart**: Restart the application
4. **Check WMI**: WMI service must be running (usually automatic)

**Note**: GPU metrics may show 0% due to WMI limitations. This is expected behavior.

### Settings Not Saving

**Problem**: Settings changes are not persisted.

**Solutions:**
1. **Check Validation**: Ensure all values are valid (no error messages)
2. **Check Permissions**: Ensure write permissions for App.config
3. **Manual Save**: Settings auto-save, but try closing and reopening Settings window
4. **Check Logs**: View logs for save errors

---

## FAQ

### Q: What is House Victoria?

**A**: House Victoria is a modular overlay desktop application that provides AI chat, project management, system monitoring, and file management in an Xbox Game Bar-style interface.

### Q: Do I need Ollama to use House Victoria?

**A**: Yes, Ollama is required for AI chat functionality. Install Ollama and ensure it's running before using AI features.

### Q: Can I use House Victoria without AI features?

**A**: Some features (like Projects, System Monitor, File Management) work without AI, but SMS/MMS chat requires AI personas, which require Ollama.

### Q: How do I create my first AI persona?

**A**: 
1. Click **AI Models** button in Main Tray
2. Click **"Create Persona"**
3. Enter name, select model, write system prompt
4. Click **"Create"**

### Q: What file types can I send in messages?

**A**: Images (jpg, jpeg, png, gif, bmp), Videos (mp4, avi, mov, wmv), Audio (mp3, wav, ogg), Documents (pdf, doc, docx, txt). Maximum file size is 50MB.

### Q: How do I change AI persona behavior?

**A**: Edit the system prompt in AI Models & Personas window, or adjust LLM parameters (Temperature, TopP, etc.) when creating/editing personas.

### Q: Can I backup my settings?

**A**: Yes, use **"Export Settings"** in Settings window to save settings as JSON. Use **"Import Settings"** to restore.

### Q: Where are my conversations stored?

**A**: Conversations are stored in a SQLite database in the application data directory. Media files are stored in `Data/Media/{ConversationId}/`.

### Q: How do I monitor system resources?

**A**: Open the System Monitor Drawer from the left edge. It shows real-time CPU, RAM, temperature, and server status.

### Q: What is the MCP Server?

**A**: MCP (Model Context Protocol) Server enables autonomous agent capabilities for AI personas, allowing them to perform actions beyond just chatting.

### Q: Can I use House Victoria with Unreal Engine?

**A**: Yes, if you have Unreal Engine with WebSocket plugin. Configure the endpoint in Settings → Virtual Environment Settings.

### Q: How do I filter projects?

**A**: Use the filter dropdowns in Projects Window to filter by type (Development, Research, etc.) or phase (Planning, InProgress, etc.).

### Q: What are roadblocks in projects?

**A**: Roadblocks are obstacles or issues that prevent project progress. Add them in the Project Detail Dialog → Roadblocks Tab.

### Q: How do I export logs?

**A**: Open Global Log Directory window, select logs, click **"Export"**, choose format (TXT, JSON, CSV), and save.

### Q: Can I customize the overlay appearance?

**A**: Yes, adjust opacity and auto-hide behavior in Settings → Overlay Settings.

### Q: What if my AI responses are too slow?

**A**: Reduce MaxTokens, decrease Context Length, or try a smaller/faster model. See Troubleshooting section for details.

---

## Document End

For technical documentation, see `HouseVictoria_Documentation.md`.

For support or issues, check the Global Log Directory for error messages and troubleshooting information.

**Version:** 1.0  
**Last Updated:** January 2025
