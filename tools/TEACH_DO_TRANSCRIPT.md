# Teach/Do Mode Acceptance Transcript

Validates that the Teach/Do toggle produces observably different response styles.

---

## Purpose

The Teach/Do toggle (`CapcomSettings.OperationMode`) changes how CAPCOM responds:
- **Teach mode**: Explanatory, educational responses that help players learn
- **Do mode**: Concise, actionable responses that get players flying quickly

This transcript provides a structured way to verify the toggle works correctly.

---

## Test Input

Use the same prompt in both modes to compare output:

> "Write an ascent to LKO kOS script for a basic rocket"

This prompt is ideal because:
- It's representative of typical CAPCOM usage
- kOS scripts benefit from both explanatory and minimal styles
- Output differences should be clearly observable

---

## System Prompts

The full prompts are documented in:
- `docs/prompts/example_prompt_teach.txt`
- `docs/prompts/example_prompt_do.txt`

### Key Markers

| Mode | Marker | Guidance Text |
|------|--------|---------------|
| Teach | `MODE: TEACH` | "The player wants to learn. Provide explanatory responses that help them understand the concepts..." |
| Do | `MODE: DO` | "The player wants actionable steps. Provide concise checklists and ready-to-use kOS scripts..." |

Both modes share:
- CAPCOM identity and tone
- Non-autopilot constraint ("You do NOT pilot the spacecraft directly")
- KSP terminology guidance

---

## Expected Output Shape: Teach Mode

Checklist for a well-formed Teach response:

- [ ] Explanatory paragraphs before code
- [ ] Conceptual breakdown (gravity turn, throttle, apoapsis targeting)
- [ ] Annotated code with WHY comments (not just WHAT)
- [ ] 400+ words typical
- [ ] Educational tone ("This works because...", "The reason we...")

Example structural pattern:
```
[Explanation of orbital mechanics concept]
[Why we approach LKO this way]
[kOS script with detailed comments]
[Tips or variations]
```

---

## Expected Output Shape: Do Mode

Checklist for a well-formed Do response:

- [ ] Brief or no introduction (0-2 sentences max)
- [ ] Code-first or numbered steps appear early
- [ ] Minimal inline comments (WHAT not WHY)
- [ ] 150-350 words typical
- [ ] Direct tone ("Here's the script:", "Run this:")

Example structural pattern:
```
[Optional one-liner context]
[kOS script with minimal comments]
[Brief usage note if needed]
```

---

## Verification Procedure

### Step 1: Test Teach Mode

1. Open KSP and launch the CAPCOM chat window
2. Open Settings (gear icon)
3. Select **Teach** radio button
4. Close settings
5. Send: "Write an ascent to LKO kOS script for a basic rocket"
6. Check `KSP.log` for: `[CapcomCore] ... mode=Teach`
7. Review response against Teach checklist above

### Step 2: Test Do Mode

1. Open Settings again
2. Select **Do** radio button
3. Close settings
4. Send the same prompt: "Write an ascent to LKO kOS script for a basic rocket"
5. Check `KSP.log` for: `[CapcomCore] ... mode=Do`
6. Review response against Do checklist above

### Step 3: Compare

Responses should be structurally different:
- Teach: More explanation, annotated code, educational framing
- Do: Less explanation, code-forward, actionable framing

---

## Pass/Fail Table

| Check | Pass | Fail |
|-------|------|------|
| Prompts differ between modes (check log) | | |
| Teach response has explanatory content before code | | |
| Do response leads with code or numbered steps | | |
| Both responses contain CAPCOM identity/tone | | |
| Both responses respect non-autopilot constraint | | |
| No crashes on toggle | | |
| Mode persists across messages (until changed) | | |

**Result:** PASS / FAIL

**Tester:** _____________

**Date:** _____________

**Notes:**

---

## Troubleshooting

### Mode not changing in log

1. Verify you closed settings panel after toggling
2. Check `CapcomSettings.Instance.OperationMode` is updating
3. Look for `PromptBuilder.BuildSystemPrompt()` calls in verbose log

### Responses look identical

1. Confirm different `MODE:` markers appear in log
2. LLM providers may not always follow instructions perfectly - this tests the *prompt*, not provider compliance
3. Try a more complex prompt that benefits more from explanation

### Toggle not visible in settings

1. Ensure `ChatPanel.DrawSettingsContent()` includes mode toggle
2. Check for UI layout errors in log

---

## Related Files

- `src/Core/Settings/CapcomSettings.cs` — `OperationMode` enum
- `src/UI/ChatPanel.cs` — Settings panel with radio buttons
- `src/Core/Prompt/PromptBuilder.cs` — Mode-specific prompt selection
- `tests/PromptBuilderTests.cs` — Unit tests for prompt generation
