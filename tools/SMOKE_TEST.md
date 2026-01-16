# Smoke Test Checklist

Quick validation that the mod builds, deploys, and runs. Run after every meaningful change.

**Target:** Windows / KSP 1.x

---

## Prerequisites

- KSP installed and launchable
- Either junction link or `KSP_DIR` environment variable set (see README_DEV.md)
- Visual Studio or `msbuild` available

---

## 1. Build & Deploy

| Step | Action | Pass | Fail |
|------|--------|------|------|
| 1.1 | Build solution (Debug) | Build succeeds with 0 errors | Build errors in Output window |
| 1.2 | Check `deploy\GameData\KSPCapcom\Plugins\KSPCapcom.dll` exists | File present, timestamp matches build time | File missing or stale |
| 1.3 | (Copy deploy only) Run `.\tools\Deploy-ToKsp.ps1 -TargetPath ".\deploy\GameData\KSPCapcom\Plugins\KSPCapcom.dll" -Clean` | "Deployed to:" message appears | Script throws error |

---

## 2. KSP Launch & Mod Loading

| Step | Action | Pass | Fail |
|------|--------|------|------|
| 2.1 | Launch KSP | Main menu loads | Crash or hang |
| 2.2 | During loading, watch for `KSPCapcom` in the loading bar (bottom-left assembly list) | Mod name appears briefly | Name never appears (DLL not found) |
| 2.3 | After reaching main menu, open `KSP.log` | File exists in KSP root folder | File missing |
| 2.4 | Search log for `[KSPCapcom]` | Lines present: `[KSPCapcom] Bootstrap Start()` and version info | No `[KSPCapcom]` entries = mod didn't initialize |
| 2.5 | Search log for errors/exceptions near KSPCapcom lines | No exceptions mentioning KSPCapcom | Stack traces with `KSPCapcom` in them |

**Log location (Windows):**
```
%KSP_DIR%\KSP.log
```
or
```
%KSP_DIR%\KSP_x64_Data\output_log.txt   (older KSP versions)
```

---

## 3. VAB/SPH Scene

| Step | Action | Pass | Fail |
|------|--------|------|------|
| 3.1 | From main menu: Start > VAB | VAB loads normally | Crash or freeze |
| 3.2 | Look for toolbar button (stock toolbar or Blizzy if installed) | Capcom button visible | Button missing |
| 3.3 | Click toolbar button | Chat window opens | Nothing happens / exception in log |
| 3.4 | Click toolbar button again | Chat window closes | Window stuck open |
| 3.5 | Type text in input field | Characters appear in field | Input ignored or crashes |
| 3.6 | Press Enter or Send button | Echo response appears in chat area | No response / exception |
| 3.7 | Drag window by title bar | Window moves smoothly, no hitching | Jitter, lag, or stuck |
| 3.8 | Close window, exit to main menu | No errors | Exception on scene unload |

---

## 4. Flight Scene

| Step | Action | Pass | Fail |
|------|--------|------|------|
| 4.1 | Load any vessel onto launchpad | Flight scene loads | Crash |
| 4.2 | Look for toolbar button | Capcom button visible | Button missing |
| 4.3 | Click toolbar button | Chat window opens | Nothing happens |
| 4.4 | Type and send a message | Echo response appears | No response |
| 4.5 | Window does not block vessel control (click outside window) | Can still control vessel | Clicks absorbed by invisible UI |
| 4.6 | Pause game (Esc) with window open | Pause menu appears normally | Conflict or crash |
| 4.7 | Unpause and close window | Window closes, flight continues | Stuck state |

---

## 5. Error States (Expected Failures)

These are situations where the mod should fail gracefully, not crash.

| Scenario | Expected Behavior | Actual |
|----------|-------------------|--------|
| Send empty message | Input ignored or "empty" feedback | |
| Send very long message (1000+ chars) | Message truncates or warns | |
| Rapid open/close toggle (spam click) | No crash, window state consistent | |
| Open window, switch scenes (VAB->Flight via debug menu) | Window closes, reinitializes in new scene | |

---

## Quick Pass Summary

After completing all steps, check:

- [ ] Mod loads (log shows `[KSPCapcom] Bootstrap Start()`)
- [ ] Toolbar button present in VAB
- [ ] Toolbar button present in Flight
- [ ] Window opens/closes in both scenes
- [ ] Text input and echo response work
- [ ] No exceptions in `KSP.log` mentioning KSPCapcom

If all boxes checked: **PASS**

Any unchecked: Note which step failed and check log for details.

---

## Troubleshooting

### Mod not loading at all
1. Verify DLL is in `GameData\KSPCapcom\Plugins\`
2. Check DLL isn't blocked (right-click > Properties > Unblock)
3. Ensure targeting .NET Framework 4.7.2 (not .NET Core/5+)

### Window doesn't appear
1. Check log for `OnGUI` or toolbar registration errors
2. Verify scene filtering (EditorLogic vs FlightGlobals)

### Input field not working
1. GUI.FocusControl issues - check if other mods conflict
2. Verify Event.current handling in OnGUI

### Exceptions on scene change
1. Check for dangling event subscriptions (GameEvents)
2. Verify OnDestroy cleanup
