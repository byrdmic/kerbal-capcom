# M5/VS15: LKO Ascent Quick Action → True Fast Path

## Context

**Objective:** Make the "Ascent" quick action a true one-click fast path that reliably produces a Script Card (not plain text) with Copy/Save/Open actions.

**Key Files:**
- `src/KSPCapcom/ChatPanel.cs` — Main chat panel; contains `OnAscentScriptClick()`, `ASCENT_SCRIPT_PROMPT`, `CanWriteAscentScript()`, and retry/error handling
- `src/KSPCapcom/Chat/ChatPanel.Input.cs` — Input area rendering with Ascent button
- `src/KSPCapcom/PromptBuilder.cs` — System prompt builder with `LKOAscentGuidance`, `ScriptOutputFormatting`, grounded mode instructions
- `src/KSPCapcom/UI/ScriptCardRenderer.cs` — Renders kOS script cards with Copy/Save/Open buttons
- `src/KSPCapcom/Chat/ChatPanel.Messages.cs` — Message rendering including code block detection via `ParseAndValidateMessage()`
- `tools/HERO_ASCENT_TRANSCRIPT.md` — Acceptance criteria and verification procedures

**Current State Analysis:**
1. `OnAscentScriptClick()` already exists (ChatPanel.cs:338-380)
2. Uses canonical `ASCENT_SCRIPT_PROMPT` constant: "Write a kOS script for LKO ascent for this craft"
3. `CanWriteAscentScript()` checks: not busy + in editor scene
4. Craft snapshot injected via `PromptBuilder.BuildUserContext()`
5. `ScriptCardRenderer` already has Copy/Save/Open actions
6. `ParseAndValidateMessage()` extracts code blocks and validates kOS syntax

**Gap Analysis (why it's not yet a "true fast path"):**
1. **Missing prerequisite validation:** Ascent button enables in editor even without valid snapshot or connector
2. **No clear error feedback:** If connector offline or snapshot missing, request may fail silently or with generic error
3. **No guarantee of Script Card:** Output depends on LLM reliably producing ```kos block; no enforcement
4. **Flight scene handling:** Button shows in flight but ascent is irrelevant there

---

## Instructions

### Phase 1: Prerequisite Validation & UI Feedback

**1.1 Enhance `CanWriteAscentScript()` to check prerequisites:**
- Add connector connectivity check (LLM endpoint configured and reachable)
- Add snapshot availability check (non-empty craft in editor)
- Return a structured result with reason string if disabled

**1.2 Update Ascent button rendering in `ChatPanel.Input.cs`:**
- Show tooltip/label explaining why disabled when prerequisites not met
- Use distinct visual state (dimmed or with warning icon) when partially ready

**1.3 Add in-chat error handling for missing prerequisites:**
- If connector offline when clicked: show "Connector not configured. Check Settings." with link to settings
- If snapshot empty when clicked: show "No craft loaded. Load a craft in VAB/SPH."
- Include Retry button if appropriate (connector might come online)

### Phase 2: Ensure Script Card Output

**2.1 Strengthen prompt instructions for reliable ```kos output:**
- Review `ScriptOutputFormatting` in PromptBuilder.cs
- Ensure Do mode explicitly says "respond with a single ```kos code block"
- Add fallback instruction: "If you cannot generate a script, explain why instead of producing partial code"

**2.2 Consider post-processing validation:**
- If response lacks ```kos block, show warning: "Response did not contain a kOS script. Try again or rephrase."
- Optionally offer to retry automatically once

### Phase 3: Flight Scene Handling

**3.1 Hide or disable Ascent button in flight scene:**
- `CanWriteAscentScript()` already checks `HighLogic.LoadedSceneIsEditor`
- Verify button is disabled (not just enabled=false) in flight
- Consider hiding entirely or showing "VAB/SPH only" tooltip

### Phase 4: Performance & Responsiveness

**4.1 Verify throttling on snapshot acquisition:**
- `EditorCraftMonitor` already throttles updates
- Confirm no extra snapshot capture on click (use cached)

**4.2 UI responsiveness during request:**
- Existing async pattern with `_pendingMessage` and streaming callbacks
- Confirm "Generating..." indicator appears immediately
- Verify Stop button works to cancel

---

## Constraints

- Do not modify `ASCENT_SCRIPT_PROMPT` constant (canonical for testing)
- Maintain existing retry mechanism for failed requests
- Keep snapshot acquisition throttled (EditorCraftMonitor handles this)
- No new dependencies or assemblies

---

## Output Format

Implementation changes as code edits to existing files. No new files unless strictly necessary.

---

## Definition of Done

1. **Happy path:** Clicking Ascent button → assistant response contains Script Card (```kos block parsed) with Copy/Save/Open available
2. **Prerequisite missing:** If snapshot unavailable or connector offline:
   - Button is disabled with readable explanation (tooltip or adjacent label)
   - OR clicking shows clear in-chat error with actionable guidance
3. **Flight scene:** Ascent button disabled or hidden with explanation
4. **No user typing required:** Works with single click when prerequisites met
5. **UI remains responsive:** No frame hitches during request; Generating indicator shows elapsed time
6. **Failure states clear:** Network errors, timeouts, or empty responses show clear message with Retry option

---

## Implementation Checklist

- [ ] Add `CanWriteAscentScriptResult` struct with (CanExecute, Reason) tuple
- [ ] Update `CanWriteAscentScript()` to return structured result
- [ ] Add connector status check to prerequisites
- [ ] Update button rendering to show disabled reason
- [ ] Add specific error messages for each prerequisite failure
- [ ] Verify prompt formatting rules enforce single ```kos block
- [ ] Add post-response validation for missing code block
- [ ] Hide/disable Ascent button properly in flight scene
- [ ] Verify throttling and responsiveness
- [ ] Manual test with HERO_ASCENT_TRANSCRIPT checklist
