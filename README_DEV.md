# KSPCapcom Development Guide

## Project Structure

```
KSPCapcom/
  KSPCapcom.sln           # Solution file
  src/
    KSPCapcom/
      KSPCapcom.csproj    # Project (outputs to deploy/)
      *.cs
  deploy/
    GameData/
      KSPCapcom/
        Plugins/          # Built DLL lands here
  tools/
    Deploy-ToKsp.ps1      # Manual deploy script
    Watch-BuildDeploy.ps1 # Watch mode for auto-rebuild
```

## Setup

### Option A: Junction (Recommended - Zero-copy deployment)

Create a junction so KSP loads directly from your repo's deploy folder:

```powershell
# Run as Administrator
mklink /J "C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program\GameData\KSPCapcom" "C:\dev\personal\kerbal-capcom\deploy\GameData\KSPCapcom"
```

With this setup, every build automatically "deploys" to KSP.

### Option B: Copy-based deployment

Set the `KSP_DIR` environment variable once:

```powershell
setx KSP_DIR "C:\Program Files (x86)\Steam\steamapps\common\Kerbal Space Program"
```

Then use the deploy script after building:

```powershell
.\tools\Deploy-ToKsp.ps1 -TargetPath ".\deploy\GameData\KSPCapcom\Plugins\KSPCapcom.dll" -Clean
```

### Post-build hook (optional)

Add this to Project Properties > Build Events > Post-build:

```
powershell -ExecutionPolicy Bypass -File "$(SolutionDir)tools\Deploy-ToKsp.ps1" -TargetPath "$(TargetPath)" -Clean
```

## Watch Mode

For rapid iteration, run the watch script:

```powershell
.\tools\Watch-BuildDeploy.ps1
```

This monitors `*.cs` files and auto-rebuilds on changes. You still need to restart KSP to load the new DLL.

## Smoke Test Checklist

### Deploy + Load

- [ ] Build Debug
- [ ] Confirm DLL exists in `deploy/GameData/KSPCapcom/Plugins/`
- [ ] Launch KSP
- [ ] Verify mod loaded by checking `KSP.log` for:
  ```
  [KSPCapcom] Bootstrap Start()
  ```

### UI Sanity

- [ ] Toolbar button appears (or hotkey opens window)
- [ ] Window opens/closes reliably
- [ ] Text input works (type -> send)
- [ ] Echo response appears (no LLM yet)

### Scene Coverage

- [ ] Works in Flight scene
- [ ] Works in VAB/SPH scene (if enabled)

### Clean Deploy Reliability

- [ ] Run deploy with `-Clean`
- [ ] Confirm no duplicate old DLLs remain
