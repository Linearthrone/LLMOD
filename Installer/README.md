## HouseVictoria Installer

This folder contains an **Inno Setup** script that builds a standard Windows installer (`.exe`) for the **HouseVictoria** desktop app.  
The installer includes a normal **uninstaller** entry in “Apps & Features” (Programs and Features), so non-technical users can install and remove the app easily.

From the repository root, run **`install.bat`** once to restore NuGet packages, build the solution, and set up the MCP Python virtual environment. Use **`start.bat`** for day-to-day launching (Ollama/MCP/TTS/STT/app as configured).

### Prerequisites

- **.NET SDK 8.0** (for building the app)
- **Inno Setup 6+** installed  
  Download from [`https://jrsoftware.org/isinfo.php`](https://jrsoftware.org/isinfo.php)

### Build and Package Steps

1. **Publish the application**

   From the repository root:

   ```powershell
   dotnet publish .\HouseVictoria.App\HouseVictoria.App.csproj `
       -c Release `
       -r win-x64 `
       -o .\publish\HouseVictoria `
       --self-contained false
   ```

   - This will create the files to be packaged in `.\publish\HouseVictoria`.
   - If you prefer a self-contained build (no separate .NET runtime install), change `--self-contained false` to `--self-contained true`.

2. **Compile the installer with Inno Setup**

   Option A – **Using the Inno Setup GUI**

   - Open **Inno Setup Compiler**
   - Open the script: `Installer\HouseVictoria.iss`
   - Press **Compile** (or `F9`)
   - On success, the installer `HouseVictoriaSetup.exe` will be created in the `Installer` folder.

   Option B – **Using the command line**

   Adjust the path to `ISCC.exe` if needed:

   ```powershell
   & "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" ".\Installer\HouseVictoria.iss"
   ```

3. **Distribute the installer**

   - Share the generated `HouseVictoriaSetup.exe` with users.
   - They can **double-click to install**, follow the wizard, and a Start Menu and optional Desktop shortcut will be created.

### Uninstalling

After installation, users can uninstall in any of these ways:

- Open **Settings → Apps → Installed apps** and uninstall **HouseVictoria**
- Or open **Control Panel → Programs and Features** and uninstall **HouseVictoria**
- Or run the **Uninstall HouseVictoria** entry from the Start Menu group

All of these are handled automatically by Inno Setup using the `HouseVictoria.iss` script.

