# House Victoria - Comprehensive Functionality Documentation

**Generated:** January 2025  
**Version:** 1.0  
**Status:** Development Phase

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Current Functionality](#current-functionality)
3. [Remaining Implementations](#remaining-implementations)
4. [Forms, Screens, Windows Catalog](#forms-screens-windows-catalog)
5. [Development Timeline](#development-timeline)
6. [Technical Requirements](#technical-requirements)
7. [Architecture Overview](#architecture-overview)

---

## Executive Summary

House Victoria is a modular overlay desktop application inspired by Xbox Game Bar, featuring multimodal AI chat interface, SMS/MMS communication, project management, system monitoring, and virtual environment integration.

### Current Status

**✅ Fully Functional:**

- Core infrastructure and service layer
- Main Tray with window management
- SMS/MMS chat interface with full conversation management and improved timeout handling
- AI Models & Personas window with persona creation, LLM parameter configuration, and model management
- Settings window with configuration persistence
- Global Log Directory window
- Top Tray with drag-and-drop file processing
- System Monitor with real-time CPU/RAM metrics and WMI-based CPU temperature monitoring
- Dark theme UI with proper selection background colors for all input controls
- Projects Window with complete UI implementation (project list, filtering, sorting, CRUD operations, detail dialogs, roadblocks, artifacts, AI collaboration logs)

**🚧 Partially Implemented:**

- System Monitor (CPU temperature via WMI working; GPU metrics and fan speeds limited by WMI constraints)
- Virtual Environment Service (scaffold exists, not connected)

**❌ Not Implemented:**

- Video call interface
- Hardware monitoring library integration (GPU metrics via vendor SDKs)
- Image generation service
- Audio processing service
- Unreal Engine WebSocket connection

---

## Current Functionality

### 1. Core Infrastructure ✅

**Location:** `HouseVictoria.Core/`, `HouseVictoria.Services/`

**Status:** Fully Implemented

**Components:**

- **Dependency Injection:** Microsoft.Extensions.DependencyInjection container
- **Event Aggregator:** Central event system for loosely coupled communication
- **Service Layer:** All 10 service interfaces implemented
- **Database:** SQLite with Dapper for persistence
- **Logging:** Serilog integration
- **Models:** Complete data models for all modules

**Services Implemented:**

1. `IAIService` - OllamaAIService, LlamaCppAIService
2. `ICommunicationService` - SMSMMSCommunicationService
3. `IPersistenceService` - DatabasePersistenceService
4. `IProjectManagementService` - ProjectManagementService
5. `ISystemMonitorService` - SystemMonitorService
6. `IVirtualEnvironmentService` - UnrealEnvironmentService
7. `ILoggingService` - LoggingService
8. `IFileGenerationService` - FileGenerationService
9. `IMCPService` - MCPService
10. `IMemoryService` - MemoryService (via Persistence)

---

### 2. Main Tray (Bottom Right) ✅

**Location:** `HouseVictoria.App/Screens/Trays/MainTray.xaml`

**Status:** Fully Functional

**Features:**

- Glass panel overlay with click-through support
- Three main buttons:
  - **SMS/MMS Button:** Opens SMS/MMS chat window
  - **AI Models Button:** Opens AI Models & Personas window
  - **Settings Button:** Opens Settings window
- Window management (open, restore, minimize)
- Event-driven architecture using EventAggregator

**Implementation Details:**

- ViewModel: `MainTrayViewModel.cs`
- Uses RelayCommand for button actions
- Publishes ShowWindowEvent to EventAggregator
- MainWindow handles window lifecycle

---

### 3. Top Tray (Auto-hiding) ✅

**Location:** `HouseVictoria.App/Screens/Trays/TopTray.xaml`

**Status:** Fully Functional

**Features:**

- **Drag-and-Drop Data Bank Upload:**
  - Accepts multiple files
  - Processes text files (txt, md, json, xml, csv, log, cs, js, py, html, css)
  - Stores binary file metadata
  - Creates "Dropped Files" data bank automatically
  - Shows processing results with error reporting

- **Generated Files Button:**
  - Opens folder containing AI-generated files
  - Integrates with FileGenerationService
  - Shows message if no files available

- **Global Log Directory Button:**
  - Opens GlobalLogDirectoryWindow
  - Displays categorized log entries

- **Projects/Goals Button:**
  - Opens ProjectsWindow
  - (Functionality minimal - see Projects section)

**Implementation Details:**

- ViewModel: `TopTrayViewModel.cs`
- Auto-hide behavior configured in MainWindow
- File processing uses IMemoryService for data bank storage

---

### 4. System Monitor Drawer (Left Edge) 🚧

**Location:** `HouseVictoria.App/Screens/Trays/SystemMonitorDrawer.xaml`

**Status:** Partially Implemented

**Working Features:**

- Real-time CPU usage monitoring (accurate)
- Real-time RAM monitoring (accurate)
- System uptime display
- AI status indicators (Primary AI, Current AI Contact)
- Virtual environment status display
- Server management:
  - Server status list (Ollama, MCP Server, TTS, Unreal Engine)
  - Restart/Stop controls per server
  - Circuit breaker pattern for unreachable servers
- "House Victoria" calligraphy branding
- Drawer open/close toggle
- Auto-hide functionality

**Hardware Monitoring Status:**

- CPU Temperature: ✅ Implemented via WMI (MSAcpi_ThermalZoneTemperature)
- CPU Fan Speed: ⚠️ Limited (WMI constraints, returns 0.0)
- GPU Usage: ⚠️ Limited (WMI constraints, returns 0.0)
- GPU Temperature: ⚠️ Limited (WMI constraints, returns 0.0)
- GPU Fan Speed: ⚠️ Limited (WMI constraints, returns 0.0)

**Implementation Details:**

- ViewModel: `SystemMonitorDrawerViewModel.cs`
- Updates every 500ms
- Uses PerformanceCounter for CPU/RAM usage
- Uses WMI (Windows Management Instrumentation) for CPU temperature via `System.Management` NuGet package
- Note: Switched from OpenHardwareMonitorLib to WMI to avoid Windows Defender WinRing0 driver warnings
- WMI provides limited sensor data but is more secure and doesn't require kernel drivers
- For full GPU metrics, would need vendor-specific libraries (NVIDIA NVML, AMD ADL SDK) in future

---

### 5. SMS/MMS Window ✅

**Location:** `HouseVictoria.App/Screens/Windows/SMSMMSWindow.xaml`

**Status:** Fully Functional

**Features:**

- **Conversation List View:**
  - Displays all conversations
  - Shows contact name, last message preview, timestamp
  - Sorted by last message time
  - Click to open conversation

- **Message View:**
  - Displays conversation messages (last 100)
  - Incoming/outgoing message alignment
  - Timestamp display
  - Message type support (Text, Image, Video, Audio, Document)
  - Media preview in message bubbles (images, videos, documents, audio)
  - Click to open/view full-size media files
  - Auto-scroll to latest message

- **Contact Selection:**
  - List of all contacts (Human and AI)
  - Start new conversation with contact
  - AI contacts load persona automatically

- **Message Sending:**
  - Text input with Enter key support
  - Shift+Enter for new line
  - Immediate UI update (optimistic)
  - AI response handling for AI contacts
  - **Media Attachments:**
    - Attachment button (📎) in input area
    - File picker with type filtering (Images, Videos, Audio, Documents)
    - File size validation (50MB maximum)
    - Pending attachment preview (filename, size, type indicator)
    - Remove/clear pending attachment button
    - Media stored in conversation-specific directories
    - Supports: Images (jpg, jpeg, png, gif, bmp), Videos (mp4, avi, mov, wmv), Audio (mp3, wav, ogg), Documents (pdf, doc, docx, txt)
  - **Improved Timeout Handling:**
    - 5-minute timeout for AI responses (configurable per request)
    - Detailed timeout error messages with suggestions
    - User-friendly error messages in chat interface
    - Guidance on reducing timeouts (MaxTokens, context length, model selection)
  - Error handling and user feedback

- **Window Management:**
  - Minimize to tray
  - Restore from tray
  - Resizable with constraints
  - Phone-like aspect ratio (Galaxy S23 proportions)
  - Positioned to avoid Main Tray

**Implementation Details:**

- ViewModel: `SMSMMSWindowViewModel.cs`
- Service: `SMSMMSCommunicationService.cs`
- Integrates with AI service for AI contact responses
- Loads AI personas when selecting AI contacts
- Event-driven message receiving
- Media files stored in `Data/Media/{ConversationId}/` directory
- Small files (<10MB) cached in memory for performance

**Not Implemented:**

- Video call interface (separate feature)

---

### 6. AI Models & Personas Window ✅

**Location:** `HouseVictoria.App/Screens/Windows/AIModelsWindow.xaml`

**Status:** Fully Functional

**Features:**

- **Contact Book View:**
  - List of all AI personas/contacts
  - Display name, model, description
  - Load/Edit/Delete actions per persona
  - Last used timestamp

- **Create Persona:**
  - Name, Model selection, System Prompt, Description
  - MCP Server endpoint configuration
  - **LLM Parameters (Advanced):**
    - Temperature (0.0-2.0, default: 0.7)
    - TopP (0.0-1.0, default: 0.9)
    - TopK (default: 40)
    - Repeat Penalty (default: 1.1)
    - Max Tokens (-1 for unlimited, default: -1)
    - Context Length (default: 4096)
  - Model validation
  - Duplicate name checking
  - Automatic data path creation
  - MCP server initialization
  - Data bank creation

- **Load Model:**
  - View available models from Ollama
  - Load model into memory
  - Model status tracking

- **Pull Model:**
  - Pull model from Ollama repository
  - Progress status display
  - Supports "ollama run" command format
  - Refresh available models after pull

- **Edit Persona:**
  - Edit system prompt via dialog
  - Save changes to persistence
  - Update UI after save

- **Delete Persona:**
  - Remove persona from contact book
  - Clean up persistence

**Implementation Details:**

- ViewModel: `AIModelsWindowViewModel.cs`
- Service: `OllamaAIService.cs`
- Persistence: Stores AIContact objects
- MCP Integration: Initializes MCP server context for each persona
- Memory Service: Creates initial memory entries and data banks

**Dialogs:**

- `EditSystemPromptDialog.xaml` - Modal dialog for editing system prompts

**Recent Enhancements:**

- Added LLM parameter configuration UI (Temperature, TopP, TopK, RepeatPenalty, MaxTokens, ContextLength)
- LLM parameters are passed to Ollama API in `/api/chat` requests
- Improved model pull functionality with proper JSON parsing
- Increased timeout for model downloads (30 minutes)

---

### 7. Settings Window ✅

**Location:** `HouseVictoria.App/Screens/Windows/SettingsWindow.xaml`

**Status:** Fully Functional - All Enhancements Complete

**Features:**

- **LLM Server Settings:**
  - Ollama endpoint configuration
  - Connection testing button
  - Individual connection status indicator (✓ Connected / ✗ Failed / Testing...)

- **MCP Server Settings:**
  - MCP server endpoint configuration
  - Connection testing button
  - Individual connection status indicator

- **TTS Settings:**
  - TTS endpoint configuration
  - Connection testing button
  - Individual connection status indicator

- **Virtual Environment Settings:**
  - Unreal Engine endpoint (WebSocket) configuration
  - Connection testing button
  - Individual connection status indicator

- **Overlay Settings:**
  - Enable/disable overlay
  - Opacity control (with validation: 0.1-1.0)
  - Auto-hide trays toggle
  - Auto-hide delay configuration (with validation: 0-60000ms)

- **Avatar Settings:**
  - Model Path configuration
  - Voice Model configuration
  - Voice Speed slider (0.1-3.0, with validation)
  - Voice Pitch slider (0.1-3.0, with validation)

- **Locomotion Settings:**
  - Walk Speed slider (0.1-10.0, with validation)
  - Run Speed slider (0.1-20.0, with validation)
  - Jump Height slider (0.1-10.0, with validation)
  - Enable Physics Interaction checkbox

- **Tools Configuration:**
  - File System Access checkbox
  - Network Access checkbox
  - System Commands checkbox

- **Persistent Memory Configuration:**
  - Enable Memory checkbox
  - Memory Path configuration
  - Max Entries input (1-1000000, with validation)
  - Importance Threshold slider (0.0-1.0, with validation)
  - Retention Days input (1-3650, with validation)

- **Settings Management:**
  - Reset to Defaults button (with confirmation dialog)
  - Import Settings (JSON format)
  - Export Settings (JSON format)
  - Save Settings (to App.config file)
  - Real-time validation with error messages

- **Connection Testing:**
  - Test buttons for each service endpoint
  - Connection status indicators with color coding
  - Detailed connection test results showing which service was tested
  - Error messages with exception details

**Implementation Details:**

- ViewModel: `SettingsWindowViewModel.cs` (fully implemented with all features)
- Persistence: Uses ConfigurationManager for App.config
- Settings loaded from AppConfig service
- Validation: URL format checking, numeric range validation, required field validation
- Connection Testing: Individual test methods for each service with proper error handling
- Status Indicators: Color-coded status text (green for success, red for failure, orange for testing)

---

### 8. Projects Window ✅

**Location:** `HouseVictoria.App/Screens/Windows/ProjectsWindow.xaml`

**Status:** Fully Functional

**Features:**

- **Project List View:**
  - Card-based display of all projects
  - Priority indicator with color coding (red/orange/green based on priority level)
  - Project type and phase badges
  - Deadline display
  - Completion percentage progress bar
  - Roadblocks indicator and count
  - Click on project card to open detail dialog

- **Filtering and Sorting:**
  - Filter by project type (All, Development, Research, Personal, Business, Other)
  - Filter by phase (All, Planning, InProgress, Review, Completed, OnHold, Cancelled)
  - Sort by name (A-Z, Z-A)
  - Sort by priority (High-Low, Low-High)
  - Sort by deadline (Soonest, Latest)
  - Sort by completion percentage (High-Low, Low-High)
  - Real-time filter and sort updates

- **Project Creation:**
  - "Create New Goal/Project" button
  - Fully integrated CreateProjectDialog
  - Create project with all fields:
    - Name, Type, Description
    - Priority (1-10 slider)
    - Start Date and Deadline (DatePicker)
    - Initial Phase
    - Assigned AI Contact
    - Initial Roadblocks (add/remove in dialog)
  - Validation and error handling
  - Automatic refresh of project list after creation

- **Project Detail Dialog (ProjectDetailDialog):**
  - **Overview Tab:**
    - Edit project details (name, description, type, priority)
    - Phase management with dropdown selector
    - Timeline information (start date, deadline, created date)
    - Completion percentage with progress bar
    - Assigned AI contact selection
    - Roadblocks summary display
    - Statistics panel (artifacts count, AI interactions count, days remaining)
  - **Roadblocks Tab:**
    - Add new roadblocks via text input
    - List all roadblocks with remove functionality
    - Edit mode toggle
  - **Artifacts Tab:**
    - Upload artifact files (with file picker)
    - List all artifacts with type icons
    - Preview, Download, and Delete buttons for each artifact
    - File size and description display
  - **AI Collaboration Logs Tab:**
    - Filter logs by AI contact
    - Search logs by text
    - Timeline display of all AI interactions
    - Shows action, details, timestamp, and performer
    - Clear search functionality

- **Project Management:**
  - Edit project functionality (toggle edit mode, save, cancel)
  - Delete project with confirmation
  - Phase transitions (change phase via dropdown)
  - Priority updates (via slider)
  - Roadblock management (add/remove in detail dialog)
  - Artifact management (upload, download, delete, preview)
  - AI collaboration logging integration

- **Window Management:**
  - Minimize to tray functionality
  - Restore from tray
  - Close button
  - Proper error handling and null safety

**Implementation Details:**

- ViewModel: `ProjectsWindowViewModel.cs` (fully implemented with filtering, sorting, CRUD operations)
- Service: `ProjectManagementService.cs` (fully functional backend)
- Dialogs:
  - `CreateProjectDialog.xaml` and `CreateProjectDialogViewModel.cs` (fully integrated)
  - `ProjectDetailDialog.xaml` and `ProjectDetailDialogViewModel.cs` (fully integrated)
- Data Models: Complete ProjectViewModel with all display properties
- Filter/Sort: FilterOption and SortOption helper classes

**Recent Enhancements:**

- Complete UI implementation with all features
- Full CRUD operations working
- Filtering and sorting fully functional
- Both dialogs integrated and operational
- Roadblock and artifact management complete
- AI collaboration logs display functional

---

### 9. Global Log Directory Window ✅

**Location:** `HouseVictoria.App/Screens/Windows/GlobalLogDirectoryWindow.xaml`

**Status:** Fully Functional

**Features:**

- **Hierarchical Log Display:**
  - Categories and subcategories
  - Tree view navigation
  - Unread count badges
  - Total count display

- **Log Entry Details:**
  - Title, timestamp, severity
  - Source, summary, full content
  - Tags display
  - Read/unread status

- **Actions:**
  - Refresh logs
  - Mark all as read
  - Export logs (TXT, JSON, CSV formats)
  - Auto-mark as read on selection

**Implementation Details:**

- ViewModel: `GlobalLogDirectoryWindowViewModel.cs`
- Service: `LoggingService.cs`
- Export uses SaveFileDialog
- Tree view with LogCategoryViewModel

---

### 10. Edit System Prompt Dialog ✅

**Location:** `HouseVictoria.App/Screens/Windows/EditSystemPromptDialog.xaml`

**Status:** Fully Functional

**Features:**

- Modal dialog for editing AI persona system prompts
- Multi-line text input
- Save/Cancel buttons
- Contact name display
- Returns updated system prompt

**Implementation Details:**

- Used by AIModelsWindow
- Implements INotifyPropertyChanged
- DialogResult pattern

---

### 11. UI Theme and Styling ✅

**Location:** `HouseVictoria.App/Styles/`

**Status:** Fully Functional

**Recent Improvements:**

- **Selection Background Colors Fixed:**
  - TextBox: Purple selection background (#9575CD) with white text for visibility on dark backgrounds
  - ComboBox: Purple selection background for items
  - ListBox: Purple selection background for items
  - PasswordBox: Purple selection background for selected text
  - All input controls now have visible selection on dark theme

**Style Resources:**

- `MaterialDesign.xaml` - Color definitions and selection brushes
- `MaterialDesignControls.xaml` - Input control styles with selection colors
- `MaterialDesignButtons.xaml` - Button styles
- `GlassEffect.xaml` - Glass overlay effects
- `OverlayStyles.xaml` - Tray overlay styles

**Implementation Details:**

- Added `SelectionBrush` (#9575CD - Primary300) and `SelectionTextBrush` (#FFFFFF) to theme
- Applied globally through default styles
- All windows automatically use improved selection colors

---

## Remaining Implementations

### High Priority

#### 1. Enhanced Hardware Monitoring (Optional Enhancement)

**Module:** System Monitor  
**Files:** `SystemMonitorService.cs`  
**Status:** ✅ CPU Temperature implemented via WMI. GPU metrics limited by WMI constraints.

**Current Implementation:**

- CPU Temperature: ✅ Working via WMI (MSAcpi_ThermalZoneTemperature)
- CPU Fan Speed: ⚠️ Limited (WMI doesn't provide fan speeds)
- GPU Metrics: ⚠️ Limited (WMI doesn't provide GPU sensor data)

**Why WMI Instead of OpenHardwareMonitorLib:**

- OpenHardwareMonitorLib uses WinRing0 kernel driver which triggers Windows Defender warnings
- WMI is more secure, doesn't require kernel drivers, and is built into Windows
- Trade-off: Limited sensor data, but no security warnings

**Future Enhancement Options:**

- NVIDIA Management Library (NVML) for NVIDIA GPU metrics (requires NVIDIA drivers)
- AMD ADL SDK for AMD GPU metrics (requires AMD drivers)
- Vendor-specific libraries can be integrated alongside WMI

**Effort:** 3-5 days (if pursuing GPU metrics)  
**Dependencies:** Vendor-specific GPU drivers and SDKs

**Tasks (if pursuing GPU metrics):**

- Research and select GPU monitoring library (NVML for NVIDIA, ADL for AMD)
- Install vendor-specific SDKs/NuGet packages
- Implement GPU usage reading (NVIDIA/AMD specific)
- Implement GPU temperature reading
- Implement GPU fan speed reading
- Update SystemMonitorService methods
- Test with various GPU configurations
- Add fallback to WMI for systems without vendor SDKs

---

#### 2. Settings Window Enhancements ✅

**Module:** Settings  
**Files:** `SettingsWindow.xaml`, `SettingsWindowViewModel.cs`  
**Status:** ✅ Complete - All features implemented

**Implemented:**

- ✅ Settings validation (URL format, numeric ranges, required fields)
- ✅ Connection testing buttons for each service (Ollama, MCP, TTS, Unreal Engine)
- ✅ Individual connection status indicators for each service endpoint (shows ✓ Connected, ✗ Failed, or Testing...)
- ✅ Improved connection test results showing which service was tested (e.g., "Ollama: ✓ Connection successful!")
- ✅ Avatar settings section (appearance, voice configuration - Model Path, Voice Model, Voice Speed, Voice Pitch)
- ✅ Locomotion settings section (movement parameters - Walk Speed, Run Speed, Jump Height, Enable Physics)
- ✅ Tools configuration section (available tools, permissions - File System Access, Network Access, System Commands)
- ✅ Persistent memory configuration UI (Enable Memory, Memory Path, Max Entries, Importance Threshold, Retention Days)
- ✅ Settings import/export functionality (JSON format)
- ✅ Settings reset/restore defaults functionality with confirmation dialog

**Recent Enhancements:**

- Added individual connection status indicators next to each endpoint
- Connection test messages now identify which service was tested
- Added "Reset to Defaults" button with confirmation dialog
- Status indicators use color coding (green for connected, red for failed, orange for testing)

**Effort:** 3-4 days ✅ Complete  
**Dependencies:** None

---

#### 3. SMS/MMS Media Sharing ✅ COMPLETE

**Module:** SMS/MMS Window  
**Files:** `SMSMMSWindow.xaml`, `SMSMMSWindowViewModel.cs`, `MessageToImageSourceConverter.cs`, `FilePathToImageSourceConverter.cs`  
**Status:** ✅ Fully Implemented and Working

**Implemented Features:**

- ✅ Image attachment UI (file picker, preview)
- ✅ Video attachment UI (file picker, preview with placeholder)
- ✅ Document attachment UI (file picker, preview with icon)
- ✅ Audio attachment UI (file picker, preview with icon)
- ✅ Media preview in message bubbles (thumbnail for images, placeholder for videos, icon for documents/audio)
- ✅ File picker integration with type filtering
- ✅ Media storage management (files stored in conversation-specific directories with absolute paths)
- ✅ Attachment size validation (50MB maximum with user-friendly error messages)
- ✅ Pending attachment preview/info display (filename, size, type)
- ✅ Remove/clear pending attachment functionality
- ✅ Click to open/view full-size media files
- ✅ Enhanced converter supporting both FilePath and MediaData (in-memory images)
- ✅ Proper handling of absolute file paths for reliable file access

**Implementation Details:**

- Attachment button in message input area (📎 icon) with visual feedback when attachment is pending
- File picker dialog with filtering: Images, Videos, Audio, Documents, All Files
- File size validation: 50MB maximum with detailed error messages
- Media files stored in `Data/Media/{ConversationId}/` directory with absolute paths
- Small image files (<10MB) optionally stored in-memory (MediaData) for faster display
- Large files use FilePath only to avoid memory issues
- MessageToImageSourceConverter checks both FilePath and MediaData for flexible image display
- Image preview in message bubbles with proper sizing (max 250x200px)
- Video preview shows placeholder with play icon and filename
- Audio/Document preview shows icon and filename, clickable to open
- Pending attachment info bar showing filename, size, and type with remove button
- Supports: Images (jpg, jpeg, png, gif, bmp), Videos (mp4, avi, mov, wmv), Audio (mp3, wav, ogg), Documents (pdf, doc, docx, txt)
- All media types properly persisted to database with FilePath
- Media files are preserved even after app restart

---

### Medium Priority

#### 4. Virtual Environment Integration

**Module:** Virtual Environment  
**Files:** `UnrealEnvironmentService.cs`  
**Status:** Scaffold exists, WebSocket connection not tested

**Required:**

- WebSocket connection testing
- Connection state management UI
- Message protocol implementation
- Error handling and reconnection logic
- Scene information display
- Avatar spawning controls
- Pose/movement controls UI

**Effort:** 3-5 days  
**Dependencies:** Unreal Engine with WebSocket plugin

**Tasks:**

- Test WebSocket connection to Unreal Engine
- Implement connection state management
- Create connection UI in System Monitor
- Implement message protocol
- Add error handling and auto-reconnect
- Create scene information display
- Add avatar spawning UI
- Create pose/movement control panel

---

#### 5. SMS/MMS Media Sharing ✅ COMPLETE

**Module:** SMS/MMS Window  
**Files:** `SMSMMSWindow.xaml`, `SMSMMSWindowViewModel.cs`, `MessageToImageSourceConverter.cs`  
**Status:** ✅ Fully Implemented - See section 3 for complete details

**Note:** This feature is fully complete. All functionality has been implemented including image/video/audio/document attachments, media preview, file picker integration, and media storage management. See section 3 above for full implementation details.

---

#### 6. Video Call Interface

**Module:** SMS/MMS Window  
**Files:** New window/dialog needed  
**Status:** Not implemented

**Required:**

- Video call window
- WebRTC or similar integration
- Call controls (mute, video on/off, hang up)
- Call state management
- Integration with CommunicationService

**Effort:** 5-7 days  
**Dependencies:** WebRTC library or video calling service

**Tasks:**

- Research and select video calling solution
- Create video call window
- Implement WebRTC or service integration
- Add call controls UI
- Integrate with CommunicationService
- Test call functionality

---

### Low Priority

#### 7. Image Generation Service

**Module:** AI Services  
**Files:** `OllamaAIService.cs`  
**Status:** Throws NotImplementedException

**Required:**

- Image generation API integration
- Options: Stable Diffusion API, Ollama vision models, or separate service
- Image generation UI in AI Models window
- Generated image storage

**Effort:** 2-3 days  
**Dependencies:** Image generation API or service

**Tasks:**

- Select image generation solution
- Implement image generation service
- Add UI for image generation
- Integrate with file generation service
- Test image generation

---

#### 8. Audio Processing Service

**Module:** AI Services  
**Files:** `OllamaAIService.cs`  
**Status:** Throws NotImplementedException

**Required:**

- Speech-to-text integration
- Options: Whisper API, local Whisper, Azure Speech, Google Speech
- Audio input UI
- Audio file processing

**Effort:** 2-3 days  
**Dependencies:** Speech-to-text service or library

**Tasks:**

- Select speech-to-text solution
- Implement audio processing service
- Add audio input UI
- Integrate with AI service
- Test audio processing

---

#### 9. Data Bank Management UI

**Module:** Top Tray / New Window  
**Files:** New window needed  
**Status:** Backend exists, no UI

**Required:**

- Data bank list view
- Data bank creation/editing
- Data entry management
- Search and filtering
- Data bank deletion

**Effort:** 3-4 days  
**Dependencies:** None (MemoryService ready)

**Tasks:**

- Create data bank management window
- Implement data bank list
- Add create/edit dialogs
- Add data entry viewer/editor
- Implement search and filtering
- Add delete functionality

---

## Forms, Screens, Windows Catalog

### Main Windows

| Window | File Location | Status | Key Features |
| -------- | -------------- | -------- | -------------- |
| **MainWindow** | `Screens/Windows/MainWindow.xaml` | ✅ Complete | Main overlay window, tray management, window lifecycle |
| **SMSMMSWindow** | `Screens/Windows/SMSMMSWindow.xaml` | ✅ Complete | Chat interface, conversations, message sending (text only) |
| **AIModelsWindow** | `Screens/Windows/AIModelsWindow.xaml` | ✅ Complete | Persona management, model loading, contact book |
| **SettingsWindow** | `Screens/Windows/SettingsWindow.xaml` | ✅ Complete | All settings sections, validation, connection testing, import/export, reset to defaults |
| **ProjectsWindow** | `Screens/Windows/ProjectsWindow.xaml` | ✅ Complete | Project list, filtering, sorting, CRUD, detail dialogs |
| **GlobalLogDirectoryWindow** | `Screens/Windows/GlobalLogDirectoryWindow.xaml` | ✅ Complete | Log viewing, export, categorization |

### Dialogs

| Dialog | File Location | Status | Purpose |
| -------- | -------------- | -------- | --------- |
| **EditSystemPromptDialog** | `Screens/Windows/EditSystemPromptDialog.xaml` | ✅ Complete | Edit AI persona system prompts |
| **CreateProjectDialog** | `Screens/Windows/CreateProjectDialog.xaml` | ✅ Complete | Create new projects with all fields, integrated |
| **ProjectDetailDialog** | `Screens/Windows/ProjectDetailDialog.xaml` | ✅ Complete | Project details, editing, roadblocks, artifacts, AI logs |

### Trays (Overlay Components)

| Tray | File Location | Status | Key Features |
| ------ | -------------- | -------- | -------------- |
| **MainTray** | `Screens/Trays/MainTray.xaml` | ✅ Complete | Bottom right, window launcher buttons |
| **TopTray** | `Screens/Trays/TopTray.xaml` | ✅ Complete | Top edge, drag-drop, file retrieval, GLD button |
| **SystemMonitorDrawer** | `Screens/Trays/SystemMonitorDrawer.xaml` | 🚧 Partial | Left edge, metrics display, server management |

### Test/Debug Windows

| Window | File Location | Status | Purpose |
| -------- | -------------- | -------- | --------- |
| **TestWindow** | `Screens/Windows/TestWindow.xaml` | ❓ Unknown | Testing/debugging (not in main flow) |

---

## Development Timeline

### Phase 1: Critical Infrastructure ✅ COMPLETE

**Goal:** Complete hardware monitoring and fix placeholder values

**Status:** ✅ Phase 1 Completed

**Completed Tasks:**

1. Hardware Monitoring Integration ✅
   - Implemented CPU temperature via WMI (MSAcpi_ThermalZoneTemperature)
   - Added System.Management NuGet package
   - Switched from OpenHardwareMonitorLib to WMI to avoid Windows Defender warnings
   - GPU metrics limited by WMI constraints (acceptable trade-off for security)

2. UI Improvements ✅
   - Fixed selection background colors for all input controls
   - Added LLM parameter configuration to AI Models window
   - Fixed Projects Window crashes
   - Improved SMS Window timeout handling

**Deliverables:**

- ✅ CPU temperature monitoring (WMI-based)
- ✅ UI stability improvements
- ✅ LLM parameter support
- ✅ Improved error handling

**Note:** Full GPU metrics would require vendor-specific SDKs (NVIDIA NVML, AMD ADL). Current WMI-based approach is secure but limited. Can be enhanced in future if needed.

---

### Phase 2: Core UI Completion ✅ COMPLETE (Projects UI)

**Goal:** Complete Projects window and Settings enhancements

**Status:** ✅ Projects UI Complete - Settings enhancements remaining but not blocking

**Completed Tasks:**

1. Project Management UI (5-7 days) ✅ COMPLETE
   - ✅ Window stability fixes (COMPLETE)
   - ✅ CreateProjectDialog fully integrated and functional
   - ✅ Project creation form integration
   - ✅ Project list view with filtering and sorting
   - ✅ ProjectDetailDialog with all tabs (Overview, Roadblocks, Artifacts, AI Logs)
   - ✅ Phase management UI (phase transitions via dropdown)
   - ✅ Roadblocks management UI (add/remove in detail dialog)
   - ✅ Artifact management UI (upload, preview, download, delete)
   - ✅ AI collaboration log display (filtering and timeline view)
   - ✅ Project editing and deletion
   - ✅ All CRUD operations functional

**Remaining Tasks:**
2. Settings Window Enhancements (3-4 days) ✅ COMPLETE

- ✅ Settings validation (URL format, numeric ranges)
- ✅ Connection testing buttons (all services)
- ✅ Individual connection status indicators
- ✅ Advanced settings sections (Avatar, Locomotion, Tools)
- ✅ Settings import/export (JSON format)
- ✅ Persistent memory configuration UI
- ✅ Reset to defaults functionality

**Deliverables:**

- ✅ Fully functional Projects window (COMPLETE)
- ✅ Complete Settings window with all enhancements (COMPLETE)

**Dependencies:** None

**Note:** Projects UI is fully complete with all major features implemented. Settings enhancements can be done in a future phase as they are not critical for core functionality.

---

### Phase 3: Media and Communication (IN PROGRESS - Weeks 6-8)

**Goal:** Add media sharing and video calling

**Status:** 🚧 In Progress - Media sharing complete, video calling pending

**Tasks:**

1. SMS/MMS Media Sharing (3-4 days) ✅ COMPLETE
   - ✅ Image/video/document/audio attachments
   - ✅ Media preview in message bubbles
   - ✅ File picker integration with type filtering
   - ✅ Media storage management
   - ✅ Attachment size validation (50MB max)
   - ✅ Pending attachment preview and removal

2. Settings Window Enhancements (3-4 days) ✅ COMPLETE
   - ✅ Settings validation
   - ✅ Connection testing buttons
   - ✅ Advanced settings sections
   - ✅ Connection status indicators
   - ✅ Reset to defaults

3. Video Call Interface (5-7 days) 📋 PENDING
   - WebRTC integration
   - Call window
   - Call controls (mute, video on/off, hang up)

**Deliverables:**

- ✅ Media sharing in SMS/MMS (COMPLETE)
- ✅ Enhanced Settings window (COMPLETE)
- 📋 Video calling functionality (Pending)

**Dependencies:** WebRTC library or service (for video calls)

**Current Priority:**

1. ✅ ~~SMS/MMS Media Sharing~~ (COMPLETE)
2. 📋 Video Call Interface (CURRENT PRIORITY - advanced feature)
3. ✅ ~~Settings Window Enhancements~~ (COMPLETE)

---

### Phase 4: Advanced Features (Weeks 9-11)

**Goal:** Virtual environment and AI enhancements

**Tasks:**

1. Virtual Environment Integration (3-5 days)
   - WebSocket connection
   - Scene management
   - Avatar controls

2. Image Generation Service (2-3 days)
   - API integration
   - UI implementation

3. Audio Processing Service (2-3 days)
   - Speech-to-text integration
   - Audio input UI

**Deliverables:**

- Working virtual environment connection
- Image generation capability
- Audio processing capability

**Dependencies:** Unreal Engine, image/audio APIs

---

### Phase 5: Polish and Optimization (Weeks 12-13)

**Goal:** Data bank UI and final polish

**Tasks:**

1. Data Bank Management UI (3-4 days)
   - Data bank window
   - CRUD operations
   - Search and filtering

2. Testing and Bug Fixes (3-5 days)
   - Integration testing
   - Performance optimization
   - Bug fixes

**Deliverables:**

- Complete data bank management
- Stable, tested application

**Dependencies:** Previous phases

---

### Development Flowchart

```text
Phase 1: Critical Infrastructure ✅ COMPLETE
    └─> Hardware Monitoring Integration ✅
            └─> [No dependencies]

Phase 2: Core UI Completion ✅ COMPLETE (Projects UI)
    ├─> Project Management UI ✅ COMPLETE
    │       └─> [No dependencies]
    └─> Settings Window Enhancements 📋 OPTIONAL
            └─> [No dependencies]

Phase 3: Media and Communication ✅ COMPLETE
    ├─> SMS/MMS Media Sharing ✅ COMPLETE
    │       └─> [No dependencies]
    ├─> Settings Window Enhancements 📋 OPTIONAL
    │       └─> [No dependencies]
    └─> Video Call Interface 📋 PENDING
            └─> [Requires WebRTC library]

Phase 4: Advanced Features 📋 PENDING
    ├─> Virtual Environment Integration
    │       └─> [Requires Unreal Engine]
    ├─> Image Generation Service
    │       └─> [Requires image API]
    └─> Audio Processing Service
            └─> [Requires speech-to-text API]

Phase 5: Polish and Optimization 📋 PENDING
    ├─> Data Bank Management UI
    │       └─> [No dependencies]
    └─> Testing and Bug Fixes
            └─> [Depends on all previous phases]
```

---

## Technical Requirements

### Required Libraries

#### Hardware Monitoring

- **System.Management** ✅ **Currently Used** - WMI-based CPU temperature monitoring (secure, no drivers needed)
- **NVIDIA Management Library (NVML)** - Optional future enhancement for NVIDIA GPU metrics
- **AMD ADL SDK** - Optional future enhancement for AMD GPU metrics
- ~~**OpenHardwareMonitorLib**~~ - Deprecated (triggers Windows Defender WinRing0 warnings)

#### Video Calling

- **WebRTC** library or service (e.g., WebRTC.NET, Pion, or cloud service)

#### Image Generation

- **Stable Diffusion API** OR
- **Ollama vision models** OR
- **Separate image generation service**

#### Audio Processing

- **Whisper API** OR
- **Local Whisper model** OR
- **Azure Speech Services** OR
- **Google Speech API**

### External Services

| Service | Endpoint | Purpose | Status |
| --------- | ---------- | --------- | -------- |
| **Ollama** | <http://localhost:11434> | AI model server | ✅ Required |
| **MCP Server** | <http://localhost:8080> | Autonomous agents | ✅ Required |
| **Unreal Engine** | ws://localhost:8888 | Virtual environment | ⚠️ Optional |
| **TTS Service** | <http://localhost:5000> | Text-to-speech | ⚠️ Optional |

### Development Environment

- **.NET 8.0 SDK** - Required
- **Visual Studio 2022** - Recommended
- **SQLite** - Included via NuGet
- **Ollama** - Must be installed and running
- **Unreal Engine** (Optional) - For virtual environment features

### NuGet Packages (Already Installed)

- Microsoft.Extensions.DependencyInjection
- Microsoft.Extensions.Hosting
- Microsoft.Extensions.Logging
- CommunityToolkit.Mvvm
- Newtonsoft.Json
- SQLite & Dapper
- Hardcodet.NotifyIcon.Wpf
- Serilog
- MaterialDesignThemes
- **System.Management** (for WMI-based hardware monitoring)

### Additional Packages Needed (Future Enhancements)

- **Hardware monitoring (GPU):** NVIDIA NVML or AMD ADL SDK (optional, for full GPU metrics)
- **WebRTC library:** For video calling (e.g., WebRTC.NET, Pion, or cloud service)
- **Image generation client:** For AI image generation (Stable Diffusion API, Ollama vision models)
- **Speech-to-text library:** For audio processing (Whisper API, local Whisper, Azure Speech, Google Speech)

---

## Architecture Overview

### Solution Structure

```text
HouseVictoria/
├── HouseVictoria.sln
├── HouseVictoria.Core/           # Core interfaces and models
│   ├── Interfaces/              # 10 service interfaces
│   ├── Models/                  # Data models
│   └── Utils/                   # EventAggregator, helpers
├── HouseVictoria.Services/      # Service implementations
│   ├── AIServices/              # Ollama, LlamaCpp
│   ├── Communication/           # SMS/MMS
│   ├── Persistence/             # SQLite database
│   ├── ProjectManagement/       # Projects service
│   ├── SystemMonitor/           # System metrics
│   ├── VirtualEnvironment/      # Unreal Engine
│   ├── Logging/                 # Logging service
│   ├── FileGeneration/          # File generation
│   └── MCP/                     # MCP server integration
└── HouseVictoria.App/           # WPF application
    ├── Screens/
    │   ├── Trays/               # 3 overlay trays
    │   └── Windows/             # 6 main windows + dialogs
    ├── HelperClasses/           # MVVM helpers
    ├── Converters/              # Value converters
    └── Styles/                  # XAML styles
```

### Service Architecture

```text
Application Layer (WPF)
    ↓
ViewModels (MVVM Pattern)
    ↓
Event Aggregator (Loose Coupling)
    ↓
Service Layer (10 Services)
    ↓
Persistence Layer (SQLite)
    ↓
External Services (Ollama, MCP, etc.)
```

### Data Flow Example: SMS Message

```text
SMSMMSWindow (UI)
    ↓
SMSMMSWindowViewModel
    ↓
ICommunicationService.SendMessageAsync()
    ↓
SMSMMSCommunicationService
    ├─> If AI Contact: IAIService.GenerateResponseAsync()
    ├─> IPersistenceService (Save message)
    └─> EventAggregator.Publish(MessageReceivedEvent)
            ↓
    SMSMMSWindowViewModel (Updates UI)
```

---

## Summary

House Victoria is a well-architected application with a solid foundation. The core infrastructure is complete and functional, with most primary features implemented. The Projects Window is fully functional with complete UI implementation. The remaining work focuses on:

1. **Settings enhancements** (validation, connection testing, advanced sections)
2. **Media capabilities** (SMS/MMS media sharing, video calls)
3. **Advanced features** (virtual environment, image/audio processing)

The modular architecture allows for independent development of each feature, making it easy to prioritize and implement remaining functionality incrementally.

**Recent Accomplishments (Phases 1 & 2 Complete):**

- ✅ Hardware monitoring via WMI (CPU temperature working, secure approach)
- ✅ LLM parameter configuration in AI Models window
- ✅ Improved SMS window timeout handling (5-minute timeout, detailed error messages)
- ✅ Projects Window fully implemented (all features complete)
- ✅ Settings Window fully enhanced (validation, connection testing, status indicators, import/export, reset to defaults)
- ✅ UI theme improvements (visible selection backgrounds on dark theme)
- ✅ Model pull functionality improvements (proper JSON parsing, 30-minute timeout)
- ✅ Complete Projects UI with filtering, sorting, CRUD, detail dialogs, roadblocks, artifacts, AI logs
- ✅ Complete Settings UI with all advanced sections (Avatar, Locomotion, Tools, Persistent Memory)
- ✅ SMS/MMS Media Sharing fully implemented (image/video/audio/document attachments, preview, storage, validation, pending attachment UI)

**Estimated Remaining Effort:** 15-20 days of development

### Current Phase: Phase 3 - Media and Communication

### Recommended Development Order (Updated)

1. ✅ ~~Hardware monitoring~~ (COMPLETE - WMI-based approach)
2. ✅ ~~Projects UI~~ (COMPLETE - fully functional)
3. ✅ ~~Settings enhancements~~ (COMPLETE - all features implemented)
4. ✅ ~~SMS/MMS Media Sharing~~ (COMPLETE - fully implemented with all features)
5. 📋 **Video Call Interface** (CURRENT PRIORITY - advanced communication feature)
6. 📋 Advanced features (virtual environment, image/audio processing)

---

## Path Forward - Development Plan

### Immediate Next Steps (Week 1-2)

**Priority 1: SMS/MMS Media Sharing** ✅ **COMPLETE**

**Completed:** Media sharing functionality is now fully implemented with file picker, media preview, storage management, and size validation.

**Priority 2: Video Call Interface** 🎯 **CURRENT FOCUS**

**Why:** With text messaging and media sharing complete, video calling is the next logical step to provide a complete communication experience.

**Tasks (Estimated 5-7 days):**

1. **Research and Select Solution**
   - Evaluate WebRTC libraries (WebRTC.NET, Pion, etc.)
   - Consider cloud service alternatives
   - Choose best fit for WPF application

2. **Video Call Window**
   - Create new VideoCallWindow
   - Video display areas (local and remote)
   - Call controls UI (mute, video toggle, hang up)

3. **WebRTC Integration**
   - Implement WebRTC connection setup
   - Handle signaling and peer connection
   - Video/audio stream management

4. **Integration and Testing**
   - Integrate with CommunicationService
   - Test call functionality
   - Error handling and reconnection logic

### Short-Term Goals (Weeks 3-4)

#### Video Call Interface

- Research and select WebRTC solution
- Create video call window
- Implement call controls (mute, video on/off, hang up)
- Integrate with CommunicationService

#### System Monitor Enhancements (Optional)

- If GPU metrics are needed, integrate NVIDIA NVML or AMD ADL SDK
- Add vendor-specific GPU monitoring alongside WMI
- Fallback to WMI for systems without vendor SDKs

### Medium-Term Goals (Weeks 5-8)

#### Virtual Environment Integration

- Test WebSocket connection to Unreal Engine
- Implement connection state management
- Create scene information display
- Add avatar spawning controls
- Implement pose/movement controls

#### AI Enhancements (Optional)

- Image generation service integration
- Audio processing (speech-to-text) integration
- Enhanced multimodal capabilities

### Long-Term Goals (Weeks 9-12)

#### AI Enhancements

- Image generation service integration
- Audio processing (speech-to-text) integration
- Enhanced multimodal capabilities

#### Data Bank Management UI

- Create data bank management window
- CRUD operations for data banks
- Data entry viewer/editor
- Search and filtering
- Vector search UI

#### Polish and Optimization

- Performance optimization
- Comprehensive testing
- Bug fixes and refinements
- Documentation updates

### Development Principles

1. **Incremental Development:** Complete one feature fully before moving to the next
2. **User-Centric:** Prioritize features that provide the most user value
3. **Stability First:** Ensure each feature is stable before adding complexity
4. **Testing:** Test thoroughly at each stage, especially integration points
5. **Documentation:** Keep documentation updated as features are completed

### Risk Mitigation

1. **External Dependencies:**
   - Ollama, MCP Server are required services - ensure they're running
   - Unreal Engine is optional - plan for graceful degradation if unavailable

2. **Hardware Variations:**
   - WMI-based approach works across all Windows systems
   - Vendor-specific GPU monitoring requires specific hardware/drivers

3. **Performance:**
   - Monitor UI responsiveness with large datasets (projects, messages, logs)
   - Consider pagination or virtualization for large lists

4. **Error Handling:**
   - Continue improving error messages and user feedback
   - Add retry logic for network operations
   - Implement circuit breakers for external services

### Success Metrics

**Phase 2 Completion Criteria:**

- ✅ Projects Window fully functional (all CRUD operations working) - **COMPLETE**
- ✅ Settings Window with all enhancements (validation, connection testing, advanced sections) - **COMPLETE**
- ✅ Zero critical bugs
- ✅ All existing features still working
- ✅ Performance acceptable (no UI freezing, responsive interactions)

**Phase 3 Completion Criteria:**

- ✅ SMS/MMS Media Sharing (image/video/document/audio attachments) - **COMPLETE**
- 📋 Video Call Interface (WebRTC integration) - Pending
- ✅ All existing features still working
- ✅ Zero critical bugs

**Overall Project Health:**

- Build success: ✅ (0 errors)
- Runtime stability: ✅ (crash fixes applied, Projects Window stable)
- UI consistency: ✅ (theme improvements complete)
- Feature completeness: ✅ 75% (core features done, Projects UI complete, media sharing pending)

---

### Document End
