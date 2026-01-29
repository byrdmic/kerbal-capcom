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
| 1.4 | Check `deploy\GameData\KSPCapcom\Data\kos_docs.json` exists | File present, >100KB | File missing or empty |

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

## 6. Error Handling Scenarios

Manual verification of error message mapping. These tests verify that transport and API errors produce user-friendly, actionable messages.

| Scenario | How to Trigger | Expected Result |
|----------|----------------|-----------------|
| DNS failure | Set endpoint to `https://invalid.local.host/v1/chat` | Orange: "Cannot resolve hostname. Check endpoint URL in Settings." |
| Connection refused | Set endpoint to `http://localhost:9999/v1/chat` | Orange: "Cannot connect to server. Is the endpoint running?" |
| Auth failure (401) | Use invalid API key (e.g., `sk-invalid`) | Red: "Invalid API key. Check your API key in Settings." |
| Server error (5xx) | (requires mock server returning 500) | Orange: "Provider server error (5xx). Will retry automatically." |

**Notes:**
- Orange messages indicate retryable errors
- Red messages indicate non-retryable errors requiring user action
- All error messages should be actionable (tell user what to check/do)

---

## 7. kOS Documentation Retrieval (VS10)

In-game validation that the `search_kos_docs` tool returns grounded snippets instead of hallucinated API responses.

| Step | Action | Pass | Fail |
|------|--------|------|------|
| 7.1 | Open chat panel in VAB or Flight | Window opens | - |
| 7.2 | Ask "How do I get ship velocity in kOS?" | Response mentions `SHIP:VELOCITY` or `VELOCITY` suffix | Generic/hallucinated API |
| 7.3 | Ask "What's the kOS command to pause execution?" | Response mentions `WAIT` command | Incorrect or made-up command |
| 7.4 | Ask "How do I lock steering in kOS?" | Response mentions `LOCK STEERING TO` syntax | - |
| 7.5 | Check `KSP.log` for telemetry lines | See `TELEM|SEARCH|query=` lines | No telemetry = tool not called |

**Telemetry verification:**
Search the KSP.log for lines like:
```
[KSPCapcom] TELEM|SEARCH|query=velocity|topN=5|ms=X|results=Y
```

The presence of these lines confirms:
- The `search_kos_docs` tool was invoked
- Query was processed
- Results were returned to the LLM

**What to look for in responses:**
- Responses should use correct kOS syntax (e.g., `SHIP:VELOCITY`, not `ship.velocity`)
- Code snippets should end with periods (kOS statement terminator)
- References to official kOS documentation are a good sign

**Common failure modes:**
- LLM ignores tool and hallucinates → Check if tool is registered in CapcomCore
- Tool called but no results → Check if `kos_docs.json` exists in `Data/` folder
- Wrong results → Check scoring/ranking algorithm

---

## 8. Script Save Security Tests

Manual verification of save guardrails to prevent path traversal and validate filenames.

### Filename Validation

| Test | Action | Expected Result |
|------|--------|-----------------|
| 8.1 | Try to save with empty filename | Error: "Filename cannot be empty" |
| 8.2 | Try to save with filename containing `/` (e.g., `dir/script`) | Error: "Filename cannot contain path separators" |
| 8.3 | Try to save with reserved name (e.g., `CON`, `NUL`, `COM1`) | Error: "'CON' is a reserved filename" |
| 8.4 | Try to save with very long filename (65+ chars before `.ks`) | Error: "Filename too long (max 64 characters before extension)" |
| 8.5 | Try to save with filename ending in dot before extension (e.g., `script..ks`) | Error: "Filename cannot end with a dot or space" |
| 8.6 | Try to save with filename containing `..` | Error: "Filename cannot contain path separators" |

### Save Flow

| Test | Action | Expected Result |
|------|--------|-----------------|
| 8.7 | Save script with valid filename | Success message, file appears in archive |
| 8.8 | Save script with same filename again | Overwrite confirmation dialog appears |
| 8.9 | Confirm overwrite | File is overwritten, success message shows "(overwritten)" |
| 8.10 | Cancel overwrite | File is not modified, dialog closes |

**Notes:**
- All error messages should appear inline in the save dialog
- File browser should not allow navigation outside archive folder
- No files should be created outside the configured archive path

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

## Related Tests

- **[TEACH_DO_TRANSCRIPT.md](./TEACH_DO_TRANSCRIPT.md)** — Acceptance test for Teach/Do mode toggle. Run after basic chat works.

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
