# Hero Flow: LKO Ascent Script Acceptance Transcript

Validates the canonical "hero flow" for LKO ascent script generation with grounded mode. This transcript documents the end-to-end user journey and serves as a regression artifact.

**Roadmap Reference:** `[[Projects/Kerbal Capcom/Roadmap#VS12 — Hero Script: LKO Ascent for This Rocket]]`

---

## Purpose

The hero flow is the primary use case for KSP Capcom:
1. Player loads a craft in VAB
2. Opens Capcom chat panel
3. Clicks "Ascent" button (or sends canonical prompt)
4. Receives a working kOS script tailored to their craft

This transcript provides a structured way to verify the complete flow works correctly with grounded mode enabled.

---

## Related Tests

- **[SMOKE_TEST.md](./SMOKE_TEST.md)** — Basic mod functionality. Run first.
- **[ASCENT_ARCHETYPE_TESTS.md](./ASCENT_ARCHETYPE_TESTS.md)** — Per-archetype validation across craft types.
- **[TEACH_DO_TRANSCRIPT.md](./TEACH_DO_TRANSCRIPT.md)** — Teach/Do mode validation.

---

## Test Input

### Canonical Prompt

The exact prompt used by the Ascent button (from `ChatPanel.cs:49`):

```
Write a kOS script for LKO ascent for this craft
```

This prompt is fixed to ensure deterministic behavior across tests and transcripts.

### Representative Craft: Low TWR Stack

For regression testing, use a craft matching the **Low TWR** archetype:

| Property | Value |
|----------|-------|
| TWR (ASL) | 1.2 |
| TWR (Vacuum) | 1.5 |
| Staging Pattern | Stack |
| Delta-V | ~3500 m/s |
| Stage Count | 2 |
| Engine Type | Liquid |

**Example Build:** Mk1 Pod + FL-T800 + LV-T30 (2-stage)

### Craft Metrics JSON Structure

The craft metrics block injected into the prompt follows this structure (from `EditorCraftSnapshot.ToCraftMetricsBlock()`):

```json:craft-metrics
{
  "context": "editor",
  "craftName": "Low TWR Test Rocket",
  "facility": "VAB",
  "twr": {
    "asl": 1.2,
    "vacuum": 1.5,
    "engineCount": 1,
    "canLaunchFromKerbin": true
  },
  "deltaV": {
    "total": 3500,
    "stageCount": 2,
    "perStage": null
  },
  "engines": {
    "total": 1,
    "solid": 0,
    "liquid": 1,
    "nuclear": 0,
    "avgIspVac": 310,
    "avgIspAtm": 265
  },
  "controlAuthority": "good",
  "stagingWarnings": [],
  "staging": {
    "stageCount": 2,
    "pattern": "stack",
    "isSingleStage": false,
    "stages": [
      {"index": 0, "hasEngine": true, "hasDecoupler": false},
      {"index": 1, "hasEngine": true, "hasDecoupler": true}
    ]
  }
}
```

---

## Context Injection

The following elements are injected into the prompt by `PromptBuilder`:

### 1. CAPCOM Identity
- Mission control persona
- KSP terminology guidance

### 2. Non-Autopilot Constraint
> "You do NOT execute code or pilot the craft directly. The scripts you provide are suggestions that the player must choose to use."

### 3. Mode Instructions
- Teach mode: Explanatory, educational responses
- Do mode: Concise, actionable responses

### 4. Grounded Mode Instructions
- Must use `search_kos_docs` tool before generating scripts
- Only use verified kOS identifiers from search results
- Include `## References` section citing used APIs

### 5. LKO Ascent Guidance (from `PromptBuilder.cs:137-156`)

```
ASCENT SCRIPT GUIDANCE:
When generating an LKO ascent script:

GRAVITY TURN:
- TWR >= 1.5: aggressive turn starting ~100m
- TWR 1.0-1.5: gentler turn starting ~250m
- Target 70-80km apoapsis for Kerbin LKO

STAGING LOGIC (check staging.isSingleStage in craft metrics):
- If isSingleStage is true: Do NOT include any STAGE commands
- For multi-stage crafts:
  - Always guard with STAGE:READY before calling STAGE
  - Use WHEN trigger or check: IF NOT STAGE:READY { WAIT UNTIL STAGE:READY. }
  - Detect staging need via: MAXTHRUST = 0 or fuel depletion
  - Add 0.5s minimum delay between stages to prevent rapid staging
  - Reference staging.stages array to know expected stage count
- Pattern hints (staging.pattern):
  - 'stack': Stage when current stage exhausts fuel/thrust
  - 'asparagus': May need altitude-based or symmetrical staging
  - 'unknown': Use conservative thrust-loss detection

Include inline comments for user-adjustable values (turn altitude, target apoapsis).
```

### 6. Craft Metrics Block
The `json:craft-metrics` block shown above is appended to provide craft-specific context.

---

## Expected Tool Calls (Grounded Mode)

When grounded mode is enabled, the LLM should invoke `search_kos_docs` to verify kOS APIs before using them.

### Expected Search Queries

| Query | Purpose |
|-------|---------|
| `LOCK STEERING` | Steering control |
| `LOCK THROTTLE` | Throttle control |
| `STAGE` | Staging commands |
| `APOAPSIS` | Orbital parameters |
| `ALTITUDE` | Flight telemetry |
| `HEADING` | Direction functions |
| `SHIP` | Vessel properties |

### Telemetry Log Format

From `KosDocTool.cs:100`, search telemetry is logged as:

```
TELEM|SEARCH|query=<query>|topN=<limit>|ms=<duration>|results=<count>
```

Example:
```
[KSPCapcom] TELEM|SEARCH|query=LOCK STEERING|topN=5|ms=12|results=3
```

---

## Expected Output Shape

The following checklist validates structural correctness without depending on specific LLM-generated content.

### Structural Checklist

| # | Check | Pass | Fail |
|---|-------|------|------|
| 1 | Single kOS code block present | | |
| 2 | Code block uses ` ```kos ` fence | | |
| 3 | Contains `LOCK STEERING` command | | |
| 4 | Contains `LOCK THROTTLE` command | | |
| 5 | Gravity turn altitude >= 200m (appropriate for TWR 1.2) | | |
| 6 | STAGE commands have `STAGE:READY` guards | | |
| 7 | Craft TWR mentioned in response or comments | | |
| 8 | `## References` section present | | |
| 9 | All identifiers in References correspond to kOS APIs used | | |
| 10 | No unverified kOS APIs (grounded mode) | | |

### Gravity Turn Altitude Expectations

Based on TWR guidance in `PromptBuilder.cs:141-142`:

| TWR Range | Expected Turn Start |
|-----------|---------------------|
| >= 1.5 | ~100m (aggressive) |
| 1.0-1.5 | ~250m (gentle) |

For the Low TWR test craft (TWR 1.2), expect turn altitude **>= 200m**.

### Staging Logic Expectations

For a 2-stage stack craft (`isSingleStage: false`, `pattern: "stack"`):

- STAGE commands **should be present**
- STAGE commands **must have STAGE:READY guards**
- Staging trigger should detect thrust loss or fuel depletion

---

## Verification Procedure

### Step-by-Step Process

1. **Build and deploy** KSPCapcom (see SMOKE_TEST.md sections 1-2)

2. **Launch KSP** and enter VAB

3. **Load test craft** matching Low TWR archetype
   - Verify TWR 1.0-1.5 in craft metrics

4. **Open Capcom chat panel** (toolbar button or hotkey)
   - Verify craft metrics JSON appears in panel or logs

5. **Enable grounded mode** in settings (if not default)

6. **Click Ascent button** (or send canonical prompt manually):
   ```
   Write a kOS script for LKO ascent for this craft
   ```

7. **Wait for response** (UI should remain responsive)

8. **Validate output structure** using checklist above

9. **Check KSP.log** for telemetry:
   - `TELEM|SEARCH` lines should appear
   - No exceptions related to KSPCapcom

---

## Pass/Fail Checklist

Complete this checklist during each test run:

| Check | Pass | Fail | Notes |
|-------|------|------|-------|
| Craft loaded in VAB | | | |
| Craft metrics JSON populated | | | |
| TWR shows 1.0-1.5 range | | | |
| Staging pattern shows "Stack" | | | |
| Grounded mode enabled | | | |
| `search_kos_docs` tool called | | | Check TELEM logs |
| Single kOS code block in response | | | |
| `LOCK STEERING` present | | | |
| `LOCK THROTTLE` present | | | |
| Turn altitude >= 200m | | | |
| STAGE has `STAGE:READY` guards | | | |
| `## References` section present | | | |
| No UI freeze during generation | | | |
| No exceptions in KSP.log | | | |

**Verdict:** [ ] PASS  [ ] FAIL

---

## Regression Artifact Template

Use this template to record test runs for regression tracking:

```markdown
## Test Run: [YYYY-MM-DD]

**Commit:** [git hash]
**PromptVersion:** [X.Y.Z]
**Grounded Mode:** ON

### Craft
- Name: [craft name]
- TWR (ASL): [X.X]
- Delta-V: [XXXX m/s]
- Pattern: [stack/asparagus/singlestage]

### Structural Checksum
- Code lines: [N]
- Turn altitude: [XXXm]
- STAGE count: [N]
- References count: [N]

### Tool Calls
- search_kos_docs invocations: [N]
- Queries: [list]

### Result: [PASS/FAIL]

### Notes
[Any observations or deviations]
```

---

## Failure Modes

### Common Issues and Troubleshooting

| Symptom | Suspected Cause | Investigation |
|---------|-----------------|---------------|
| No code block in response | Prompt incomplete or model confusion | Check full prompt in logs |
| Turn starts too early (<150m) | TWR not passed correctly | Verify craft metrics JSON has correct TWR |
| Missing STAGE:READY guards | Staging guidance not in prompt | Check PromptBuilder output |
| No `## References` section | Grounded mode not enabled | Verify settings, check tool calls |
| `search_kos_docs` not called | Grounded mode disabled or tool missing | Check tool registration, settings |
| Unverified kOS APIs present | Tool results ignored | Check if results are included in prompt |
| UI freezes during generation | Async handling broken | Check for blocking calls in ChatPanel |
| Empty craft metrics | Craft not loaded or snapshot failed | Check EditorCraftSnapshot.Capture logs |

### SSTO False Positive

If the craft has multiple stages but `isSingleStage` returns true:
1. Check `StagingSummary.StageCount`
2. Verify staging icons in VAB show correct count
3. Look for parts incorrectly classified

### Grounded Mode Not Affecting Output

1. Verify `CapcomSettings.GroundedMode` is true
2. Check that `KosDocTool` is registered in tool list
3. Look for `TELEM|SEARCH` lines in KSP.log
4. Compare output with grounded mode off to verify difference

---

## Version History

| Date | PromptVersion | Changes |
|------|---------------|---------|
| [Initial] | 1.4.0 | Created hero flow transcript |
