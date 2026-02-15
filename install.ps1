# House Victoria Complete Installer
# This script installs and starts all components including the MCP server

param(
    [switch]$SkipPrerequisites,
    [switch]$SkipBuild,
    [switch]$SkipMCP,
    [switch]$NoStart
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

# Colors for output
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Write-Step {
    param([string]$Message)
    Write-ColorOutput "`n=== $Message ===" "Cyan"
}

function Write-Success {
    param([string]$Message)
    Write-ColorOutput "✓ $Message" "Green"
}

function Write-Error {
    param([string]$Message)
    Write-ColorOutput "✗ $Message" "Red"
}

function Write-Warning {
    param([string]$Message)
    Write-ColorOutput "⚠ $Message" "Yellow"
}

function Write-Info {
    param([string]$Message)
    Write-ColorOutput "ℹ $Message" "Blue"
}

# Get script directory
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptDir

Write-ColorOutput @"
╔══════════════════════════════════════════════════════════════╗
║         House Victoria Complete Installation Script          ║
╚══════════════════════════════════════════════════════════════╝
"@ "Cyan"

# ============================================================
# Step 1: Check Prerequisites
# ============================================================
Write-Step "Checking Prerequisites"

if (-not $SkipPrerequisites) {
    # Check .NET 8.0 SDK
    Write-Info "Checking for .NET 8.0 SDK..."
    try {
        $dotnetVersion = dotnet --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            $versionParts = $dotnetVersion -split '\.'
            $majorVersion = [int]$versionParts[0]
            if ($majorVersion -ge 8) {
                Write-Success ".NET SDK found: $dotnetVersion"
            } else {
                Write-Error ".NET 8.0 SDK is required. Found version: $dotnetVersion"
                Write-Info "Please install .NET 8.0 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0"
                exit 1
            }
        } else {
            Write-Error ".NET SDK not found. Please install .NET 8.0 SDK."
            Write-Info "Download from: https://dotnet.microsoft.com/download/dotnet/8.0"
            exit 1
        }
    } catch {
        Write-Error ".NET SDK not found. Please install .NET 8.0 SDK."
        Write-Info "Download from: https://dotnet.microsoft.com/download/dotnet/8.0"
        exit 1
    }

    # Check Python 3.10+
    Write-Info "Checking for Python 3.10+..."
    try {
        $pythonVersion = python --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            if ($pythonVersion -match "Python (\d+)\.(\d+)") {
                $major = [int]$matches[1]
                $minor = [int]$matches[2]
                if ($major -gt 3 -or ($major -eq 3 -and $minor -ge 10)) {
                    Write-Success "Python found: $pythonVersion"
                } else {
                    Write-Error "Python 3.10+ is required. Found: $pythonVersion"
                    Write-Info "Please install Python 3.10 or higher from: https://www.python.org/downloads/"
                    exit 1
                }
            } else {
                Write-Error "Could not parse Python version: $pythonVersion"
                exit 1
            }
        } else {
            Write-Error "Python not found. Please install Python 3.10+."
            Write-Info "Download from: https://www.python.org/downloads/"
            exit 1
        }
    } catch {
        Write-Error "Python not found. Please install Python 3.10+."
        Write-Info "Download from: https://www.python.org/downloads/"
        exit 1
    }

    # Check pip
    Write-Info "Checking for pip..."
    try {
        $pipVersion = pip --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Success "pip found: $pipVersion"
        } else {
            Write-Error "pip not found. Please install pip."
            exit 1
        }
    } catch {
        Write-Error "pip not found. Please install pip."
        exit 1
    }
} else {
    Write-Warning "Skipping prerequisite checks..."
}

# ============================================================
# Step 2: Install .NET Dependencies and Build
# ============================================================
Write-Step "Installing .NET Dependencies and Building Solution"

if (-not $SkipBuild) {
    Write-Info "Restoring NuGet packages..."
    dotnet restore HouseVictoria.sln
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to restore NuGet packages."
        exit 1
    }
    Write-Success "NuGet packages restored."

    Write-Info "Building solution..."
    dotnet build HouseVictoria.sln --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed."
        exit 1
    }
    Write-Success "Solution built successfully."
} else {
    Write-Warning "Skipping .NET build..."
}

# ============================================================
# Step 3: Setup MCP Server
# ============================================================
Write-Step "Setting Up MCP Server"

if (-not $SkipMCP) {
    $mcpPath = Join-Path $ScriptDir "MCPServerTemplate"
    
    if (-not (Test-Path $mcpPath)) {
        Write-Error "MCP Server directory not found: $mcpPath"
        exit 1
    }

    Set-Location $mcpPath

    # Create virtual environment if it doesn't exist
    if (-not (Test-Path ".venv")) {
        Write-Info "Creating Python virtual environment..."
        python -m venv .venv
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to create virtual environment."
            exit 1
        }
        Write-Success "Virtual environment created."
    } else {
        Write-Info "Virtual environment already exists."
    }

    # Activate virtual environment
    Write-Info "Activating virtual environment..."
    $activateScript = Join-Path $mcpPath ".venv\Scripts\Activate.ps1"
    if (Test-Path $activateScript) {
        & $activateScript
    } else {
        Write-Error "Virtual environment activation script not found."
        exit 1
    }

    # Upgrade pip
    Write-Info "Upgrading pip..."
    python -m pip install --upgrade pip --quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Failed to upgrade pip, continuing anyway..."
    }

    # Install dependencies
    Write-Info "Installing Python dependencies..."
    pip install -e . --quiet
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to install Python dependencies."
        exit 1
    }
    Write-Success "Python dependencies installed."

    # Create necessary directories
    Write-Info "Creating data directories..."
    $directories = @("data", "data\banks", "data\projects", "logs")
    foreach ($dir in $directories) {
        $fullPath = Join-Path $mcpPath $dir
        if (-not (Test-Path $fullPath)) {
            New-Item -ItemType Directory -Path $fullPath -Force | Out-Null
            Write-Success "Created directory: $dir"
        }
    }

    # Create .env file if it doesn't exist
    $envFile = Join-Path $mcpPath ".env"
    if (-not (Test-Path $envFile)) {
        Write-Info "Creating .env configuration file..."
        $envContent = @"
# Server Configuration
SERVER_PORT=8080
SERVER_HOST=localhost

# Database Configuration
DATABASE_PATH=.\data\memory.db

# Data Banks
DATA_BANKS_PATH=.\data\banks
PROJECTS_PATH=.\data\projects

# Logging
LOG_LEVEL=INFO
LOG_FILE=.\logs\server.log

# Memory
MEMORY_MAX_ENTRIES=10000
MEMORY_RETENTION_DAYS=365
"@
        Set-Content -Path $envFile -Value $envContent
        Write-Success ".env file created."
    } else {
        Write-Info ".env file already exists."
    }

    Set-Location $ScriptDir
    Write-Success "MCP Server setup complete."
} else {
    Write-Warning "Skipping MCP server setup..."
}

# ============================================================
# Step 4: Start Services
# ============================================================
if (-not $NoStart) {
    Write-Step "Starting Services"

    # Start Ollama service
    Write-Info "Starting Ollama service..."
    $ollamaCmd = Get-Command ollama -ErrorAction SilentlyContinue
    if ($ollamaCmd) {
        $null = Start-Process -FilePath "ollama" -ArgumentList "serve" -WindowStyle Hidden -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Write-Success "Ollama server launch attempted. Service should be available at http://localhost:11434"
    } else {
        Write-Info "Ollama command not found in PATH."
        Write-Info "If Ollama is installed, ensure it's in your PATH or start it manually."
        Write-Info "Download from: https://ollama.ai if needed."
        Write-Info "Note: On Windows, Ollama may run as a service - check Task Manager."
    }

    # Start Stability Matrix first (required for Stable Diffusion)
    Write-Info "Starting Stability Matrix..."
    $stabilityMatrixPaths = @(
        "C:\StabilityMatrix\StabilityMatrix.exe",
        "C:\Program Files\StabilityMatrix\StabilityMatrix.exe",
        "C:\Program Files (x86)\StabilityMatrix\StabilityMatrix.exe"
    )
    
    $stabilityMatrixPath = $null
    foreach ($path in $stabilityMatrixPaths) {
        if (Test-Path $path) {
            $stabilityMatrixPath = $path
            break
        }
    }
    
    if ($stabilityMatrixPath) {
        Write-Info "Launching Stability Matrix from: $stabilityMatrixPath"
        $stabilityMatrixProcess = Start-Process -FilePath $stabilityMatrixPath -PassThru -ErrorAction SilentlyContinue
        if ($stabilityMatrixProcess) {
            Start-Sleep -Seconds 5
            Write-Success "Stability Matrix started (PID: $($stabilityMatrixProcess.Id))"
            Write-Info "Waiting for Stability Matrix to initialize..."
            Start-Sleep -Seconds 3
        } else {
            Write-Warning "Failed to start Stability Matrix. It may already be running."
        }
    } else {
        Write-Info "Stability Matrix not found in common C: drive locations."
        Write-Info "Skipping Stability Matrix startup. Start manually if installed elsewhere."
    }

    # Start Stable Diffusion/ComfyUI if available
    Write-Info "Starting Stable Diffusion/ComfyUI..."
    $comfyUIPaths = @(
        "C:\ComfyUI\main.py",
        "C:\StabilityMatrix\Data\Packages\ComfyUI\main.py",
        "$env:USERPROFILE\ComfyUI\main.py",
        "$env:LOCALAPPDATA\ComfyUI\main.py"
    )
    
    $comfyUIPath = $null
    foreach ($path in $comfyUIPaths) {
        if (Test-Path $path) {
            $comfyUIPath = Split-Path -Parent $path
            break
        }
    }
    
    if ($comfyUIPath) {
        Write-Info "Launching ComfyUI from: $comfyUIPath"
        $pythonCmd = Get-Command python -ErrorAction SilentlyContinue
        if ($pythonCmd) {
            $null = Start-Process -FilePath "python" -ArgumentList "main.py", "--port", "8188" -WorkingDirectory $comfyUIPath -WindowStyle Hidden -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2
            Write-Success "ComfyUI launch attempted. Service should be available at http://localhost:8188"
        } else {
            Write-Warning "Python not found in PATH. Skipping ComfyUI startup."
        }
    } else {
        Write-Info "ComfyUI not found in common locations."
        Write-Info "Skipping Stable Diffusion startup. Start manually if installed elsewhere."
    }

    # Start MCP Server in background
    if (-not $SkipMCP) {
        Write-Info "Starting MCP Server..."
        $mcpPath = Join-Path $ScriptDir "MCPServerTemplate"
        $pythonExe = Join-Path $mcpPath ".venv\Scripts\python.exe"
        $httpServer = Join-Path $mcpPath "http_server.py"
        
        if (Test-Path $pythonExe) {
            if (Test-Path $httpServer) {
                $mcpProcess = Start-Process -FilePath $pythonExe -ArgumentList "http_server.py" -WorkingDirectory $mcpPath -WindowStyle Hidden -PassThru
                Start-Sleep -Seconds 3
                
                if (-not $mcpProcess.HasExited) {
                    Write-Success "MCP Server started (PID: $($mcpProcess.Id))"
                    Write-Info "Server should be available at http://localhost:8080"
                } else {
                    Write-Warning "MCP Server may have exited immediately. Check logs in MCPServerTemplate\logs\server.log"
                }
            } else {
                Write-Error "MCP HTTP server not found: $httpServer"
                Write-Info "Please ensure http_server.py exists in MCPServerTemplate directory."
            }
        } else {
            Write-Error "Python executable not found: $pythonExe"
        }
    }

    # Start WPF Application
    Write-Info "Starting House Victoria Application..."
    $appPath = Join-Path $ScriptDir "HouseVictoria.App\bin\Release\net8.0-windows\HouseVictoria.App.exe"
    
    if (Test-Path $appPath) {
        $appProcess = Start-Process -FilePath $appPath -PassThru
        Write-Success "House Victoria Application started (PID: $($appProcess.Id))"
    } else {
        # Try Debug build if Release doesn't exist
        $appPathDebug = Join-Path $ScriptDir "HouseVictoria.App\bin\Debug\net8.0-windows\HouseVictoria.App.exe"
        if (Test-Path $appPathDebug) {
            $appProcess = Start-Process -FilePath $appPathDebug -PassThru
            Write-Success "House Victoria Application started from Debug build (PID: $($appProcess.Id))"
        } else {
            Write-Error "House Victoria Application executable not found."
            Write-Info "Expected locations:"
            Write-Info "  - $appPath"
            Write-Info "  - $appPathDebug"
            Write-Info "Please build the solution first."
        }
    }
} else {
    Write-Info "Skipping service startup (--NoStart specified)."
}

# ============================================================
# Summary
# ============================================================
Write-Step "Installation Complete"

Write-ColorOutput @"
╔══════════════════════════════════════════════════════════════╗
║                    Installation Summary                     ║
╚══════════════════════════════════════════════════════════════╝
"@ "Green"

Write-ColorOutput "✓ .NET Solution: Built and ready" "Green"
if (-not $SkipMCP) {
    Write-ColorOutput "✓ MCP Server: Installed and configured" "Green"
    Write-ColorOutput "  Location: MCPServerTemplate\" "White"
    Write-ColorOutput "  Python: MCPServerTemplate\.venv\Scripts\python.exe" "White"
}
if (-not $NoStart) {
    Write-ColorOutput "✓ Services: Started" "Green"
}

Write-ColorOutput @"

To start services later (if already installed):
  Run: start.ps1 or start.bat

To manually start services individually:
  Ollama:      ollama serve
  ComfyUI:     cd ComfyUI && python main.py --port 8188
  MCP Server:  cd MCPServerTemplate && .venv\Scripts\python.exe http_server.py
  Application: HouseVictoria.App\bin\Release\net8.0-windows\HouseVictoria.App.exe

To stop services:
  Use Task Manager or: Get-Process | Where-Object {$_.ProcessName -like '*HouseVictoria*' -or $_.ProcessName -like '*python*' -or $_.ProcessName -like '*ollama*'} | Stop-Process
"@ "Cyan"

Write-ColorOutput "`nInstallation completed successfully!`n" "Green"
