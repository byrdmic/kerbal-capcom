# Build Instructions for KSP Capcom

## Prerequisites

1. **Visual Studio 2019 or later** (Community Edition works fine)
   - Required workload: ".NET desktop development"
   - Or: Visual Studio Build Tools with .NET Framework 4.7.2 SDK

2. **.NET Framework 4.7.2 Developer Pack**
   - Download from: https://dotnet.microsoft.com/download/dotnet-framework/net472

3. **Kerbal Space Program 1 (Windows)** installation
   - Steam or standalone version
   - Required for KSP assembly references (Assembly-CSharp.dll, UnityEngine.dll, etc.)

## Environment Setup

Set the `KSP_DIR` environment variable to point to your KSP installation:

```powershell
# PowerShell (run once, persists across sessions)
setx KSP_DIR "C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program"
```

Or set it via Windows System Properties:
1. Right-click "This PC" → Properties → Advanced system settings
2. Environment Variables → New (User or System variable)
3. Variable name: `KSP_DIR`
4. Variable value: Your KSP install path

**Important:** Restart Visual Studio or your terminal after setting the environment variable.

## Building the Project

### Option 1: Visual Studio

1. Open `KSPCapcom.sln` in Visual Studio
2. Select your build configuration:
   - **Debug**: Includes debug symbols, no optimization
   - **Release**: Optimized for production
3. Build → Build Solution (or press `Ctrl+Shift+B`)

Build output will be placed in: `deploy\GameData\KSPCapcom\Plugins\KSPCapcom.dll`

### Option 2: Command Line (MSBuild)

```powershell
# Navigate to the project root
cd C:\dev\personal\kerbal-capcom

# Build Debug configuration
msbuild KSPCapcom.sln /p:Configuration=Debug

# Build Release configuration
msbuild KSPCapcom.sln /p:Configuration=Release
```

### Option 3: dotnet CLI (if available)

```powershell
dotnet build KSPCapcom.sln -c Debug
# or
dotnet build KSPCapcom.sln -c Release
```

## Deployment to KSP

After building, you need to get the DLL into your KSP GameData folder. There are two approaches:

### Recommended: Junction Link (Zero-copy)

Create a directory junction so KSP loads directly from your repo's deploy folder:

```powershell
# Run PowerShell as Administrator
$repoPath = "C:\dev\personal\kerbal-capcom" # or whatever your repo path is
$kspPath = "C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program"

New-Item -ItemType Junction -Path "$kspPath\GameData\KSPCapcom" -Target "$repoPath\deploy\GameData\KSPCapcom"
```

With this setup, every build automatically "deploys" to KSP. Just restart KSP to load the new DLL.

### Alternative: Copy-based Deployment

Use the provided PowerShell deployment script:

```powershell
# Deploy and clean old files
.\tools\Deploy-ToKsp.ps1 -TargetPath ".\deploy\GameData\KSPCapcom\Plugins\KSPCapcom.dll" -Clean
```

To automate this after every build, add a post-build event:
1. Right-click project → Properties → Build Events
2. Post-build event command line:
```
powershell -ExecutionPolicy Bypass -File "$(SolutionDir)tools\Deploy-ToKsp.ps1" -TargetPath "$(TargetPath)" -Clean
```

## Rapid Development Workflow

For fast iteration during development:

```powershell
# Terminal 1: Watch mode (auto-rebuilds on file changes)
.\tools\Watch-BuildDeploy.ps1

# Terminal 2: Run KSP, test, close KSP, repeat
```

The watch script monitors `*.cs` files and automatically rebuilds when you save changes.

## Verifying the Build

1. Check that the DLL exists:
   ```powershell
   Test-Path ".\deploy\GameData\KSPCapcom\Plugins\KSPCapcom.dll"
   ```

2. Launch KSP and check `KSP.log` for load confirmation:
   ```
   [KSPCapcom] Bootstrap Start()
   ```

3. Look for the toolbar button or use the configured hotkey to open the chat panel

## Troubleshooting

### "KSP_DIR is not set" or assembly reference errors

- Verify `KSP_DIR` is set: `echo $env:KSP_DIR` (PowerShell) or `echo %KSP_DIR%` (CMD)
- Restart Visual Studio after setting the environment variable
- Confirm the path points to your KSP root folder (contains `KSP_x64_Data\Managed\`)

### Build succeeds but mod doesn't load in KSP

- Check KSP.log for load errors
- Ensure the DLL is in the correct location: `GameData\KSPCapcom\Plugins\KSPCapcom.dll`
- Verify you're running KSP 1 (not KSP 2)
- Try a clean deploy: `.\tools\Deploy-ToKsp.ps1 -Clean`

### "Access denied" or "file in use" during build

- Close KSP before building (Unity locks DLLs)
- If using a junction, ensure it's pointing to the correct location
- Check that no other processes have the DLL open

### MSBuild not found

- Run build commands from "Developer Command Prompt for VS" or "Developer PowerShell for VS"
- Or add MSBuild to PATH: `C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin`

## Project Structure

```
KSPCapcom/
├── KSPCapcom.sln              # Visual Studio solution file
├── src/
│   ├── KSPCapcom/
│   │   ├── KSPCapcom.csproj   # Main project file
│   │   └── *.cs               # Source files
│   └── KSPCapcom.Tests/
│       └── KSPCapcom.Tests.csproj  # Test project
├── deploy/
│   └── GameData/
│       └── KSPCapcom/
│           └── Plugins/       # Build output location
└── tools/
    ├── Deploy-ToKsp.ps1       # Manual deployment script
    └── Watch-BuildDeploy.ps1  # Auto-rebuild on changes
```

## Running Tests

```powershell
# Visual Studio Test Explorer: Test → Run All Tests

# Or command line:
dotnet test src\KSPCapcom.Tests\KSPCapcom.Tests.csproj
```

## Additional Resources

- See `README.md` for project overview and features
- See `README_DEV.md` for detailed development workflow
- See `tools\SMOKE_TEST.md` for manual testing checklist
