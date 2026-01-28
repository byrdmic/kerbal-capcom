# Ascent Script Archetype Testing Checklist

Validation that LKO ascent script generation produces correct, safe scripts across different craft configurations. Run after changes to `PromptBuilder.cs`, `StagingSummary.cs`, or ascent-related prompts.

**Target:** Windows / KSP 1.x with kOS installed

---

## Prerequisites

- KSP installed with kOS mod
- KSPCapcom mod loaded (verify via SMOKE_TEST.md sections 1-3)
- At least one craft per archetype saved in VAB (see Archetype Definitions)
- API key configured and working

---

## 1. Archetype Definitions

| Name | TWR Range (ASL) | Staging Pattern | Delta-V Budget | Expected Behavior |
|------|-----------------|-----------------|----------------|-------------------|
| Low TWR | 1.0–1.3 | Stack | ~3500 m/s | Gentle gravity turn starting ~250m, gradual pitch program |
| High TWR | 2.0+ | Stack | ~4000 m/s | Aggressive turn starting ~100m, but throttle management to avoid flip |
| Asparagus | 1.4–1.8 | Asparagus | ~5000 m/s | Symmetrical staging guards, no uncontrolled separation, booster-aware logic |
| SSTO | 1.5–2.0 | SingleStage | ~4500 m/s | **NO STAGE commands** in output, closed-cycle ascent profile |

### Reference Craft Suggestions

Build or download craft matching these profiles:

| Archetype | Example Build |
|-----------|---------------|
| Low TWR | Mk1 pod + FL-T800 + LV-T30 (TWR ~1.2) |
| High TWR | Mk1 pod + FL-T400 + Mainsail (TWR ~2.5) |
| Asparagus | Core + 4x radial boosters with fuel lines |
| SSTO | Rapier-based spaceplane or NERVs |

---

## 2. Smoke Test Checklist (Grounded Mode ON)

Grounded mode ensures scripts only use verified kOS identifiers from the documentation index.

### 2.1 Low TWR Craft

| Step | Action | Pass | Fail |
|------|--------|------|------|
| 2.1.1 | Load Low TWR craft in VAB | Craft loads, parts visible | Load error |
| 2.1.2 | Open Capcom chat panel | Panel opens, craft metrics populated | Panel fails to open |
| 2.1.3 | Verify TWR in metrics shows 1.0–1.3 range | TWR displayed correctly | Wrong TWR or missing |
| 2.1.4 | Verify staging pattern shows "Stack" | Pattern detected correctly | Wrong pattern |
| 2.1.5 | Send: "Generate a kOS ascent script to 80km LKO" | Script generated with gentle turn (~250m start) | No response or error |
| 2.1.6 | Check gravity turn altitude | Turn starts ≥200m | Turn starts too early (<100m) |
| 2.1.7 | Check pitch program | Gradual reduction (not aggressive) | Pitch drops too fast |
| 2.1.8 | Check for staging commands | STAGE commands present with guards | Missing staging or no guards |
| 2.1.9 | No unverified kOS identifiers | All commands from docs index | Hallucinated APIs present |
| 2.1.10 | No UI freeze during generation | UI remains responsive | UI hangs |

### 2.2 High TWR Craft

| Step | Action | Pass | Fail |
|------|--------|------|------|
| 2.2.1 | Load High TWR craft in VAB | Craft loads, parts visible | Load error |
| 2.2.2 | Open Capcom chat panel | Panel opens, craft metrics populated | Panel fails to open |
| 2.2.3 | Verify TWR in metrics shows 2.0+ | TWR displayed correctly | Wrong TWR or missing |
| 2.2.4 | Verify staging pattern shows "Stack" | Pattern detected correctly | Wrong pattern |
| 2.2.5 | Send: "Generate a kOS ascent script to 80km LKO" | Script generated with aggressive turn | No response or error |
| 2.2.6 | Check gravity turn altitude | Turn starts ~100m (earlier than Low TWR) | Turn starts same as Low TWR |
| 2.2.7 | Check throttle management | Includes throttle reduction or limiting | Full throttle throughout |
| 2.2.8 | Check pitch program | Steeper initial pitch but not vertical flip | Pitch program causes flip risk |
| 2.2.9 | Check for staging commands | STAGE commands present with guards | Missing staging or no guards |
| 2.2.10 | No unverified kOS identifiers | All commands from docs index | Hallucinated APIs present |

### 2.3 Asparagus Staging Craft

| Step | Action | Pass | Fail |
|------|--------|------|------|
| 2.3.1 | Load Asparagus craft in VAB | Craft loads, parts visible | Load error |
| 2.3.2 | Open Capcom chat panel | Panel opens, craft metrics populated | Panel fails to open |
| 2.3.3 | Verify TWR in metrics shows 1.4–1.8 | TWR displayed correctly | Wrong TWR or missing |
| 2.3.4 | Verify staging pattern shows "Asparagus" | Pattern detected correctly | Shows "Stack" or wrong |
| 2.3.5 | Send: "Generate a kOS ascent script to 80km LKO" | Script generated with booster awareness | No response or error |
| 2.3.6 | Check staging logic | Multiple stage events with fuel checks | Single staging event only |
| 2.3.7 | Check symmetry guards | Staging waits for symmetric depletion | No symmetry awareness |
| 2.3.8 | Check for RCS/separation notes | Comments about clean separation | No separation guidance |
| 2.3.9 | Check TWR recalculation | Script accounts for TWR change post-staging | Assumes constant TWR |
| 2.3.10 | No unverified kOS identifiers | All commands from docs index | Hallucinated APIs present |

### 2.4 SSTO Craft

| Step | Action | Pass | Fail |
|------|--------|------|------|
| 2.4.1 | Load SSTO craft in VAB/SPH | Craft loads, parts visible | Load error |
| 2.4.2 | Open Capcom chat panel | Panel opens, craft metrics populated | Panel fails to open |
| 2.4.3 | Verify TWR in metrics shows 1.5–2.0 | TWR displayed correctly | Wrong TWR or missing |
| 2.4.4 | Verify staging pattern shows "SingleStage" | Pattern detected correctly | Shows "Stack" or wrong |
| 2.4.5 | Send: "Generate a kOS ascent script to 80km LKO" | Script generated for closed-cycle | No response or error |
| 2.4.6 | **Check for STAGE commands** | **NO STAGE commands in output** | **STAGE command present = FAIL** |
| 2.4.7 | Check engine mode switching | Comments about mode switch if applicable | No mode awareness |
| 2.4.8 | Check atmosphere handling | Different profile for atmo vs vacuum | Single profile throughout |
| 2.4.9 | Check fuel monitoring | Continuous fuel checks without staging | Staging-based fuel logic |
| 2.4.10 | No unverified kOS identifiers | All commands from docs index | Hallucinated APIs present |

---

## 3. Smoke Test Checklist (Grounded Mode OFF)

With grounded mode disabled, the LLM may use kOS APIs not in the documentation index. Document any differences.

### 3.1 General Differences to Note

| Aspect | Grounded ON | Grounded OFF (Expected) |
|--------|-------------|-------------------------|
| API coverage | Docs-verified only | May include undocumented APIs |
| Syntax style | Conservative, proven patterns | May use advanced/experimental features |
| Error handling | Basic, known-good patterns | May include sophisticated error handling |
| Comments | Reference documentation | May explain without doc links |

### 3.2 Per-Archetype (Grounded OFF)

Repeat sections 2.1–2.4 with Grounded Mode OFF. For each archetype, note:

| Step | Action | Pass | Fail |
|------|--------|------|------|
| 3.2.1 | Toggle Grounded Mode OFF in settings | Setting changes | Toggle fails |
| 3.2.2 | Generate script for [archetype] | Script generates | Error |
| 3.2.3 | Compare to Grounded ON output | Note differences | - |
| 3.2.4 | Check for hallucinated APIs | Document any unverified APIs found | - |
| 3.2.5 | Verify core logic still correct | Gravity turn, staging correct | Wrong flight profile |

**Note:** Failures in Grounded OFF mode are less severe than Grounded ON failures, but should still be documented for prompt tuning.

---

## 4. Cross-Archetype Regression Checks

Run these after any archetype passes individual tests to verify system-wide behavior.

| Step | Action | Pass | Fail |
|------|--------|------|------|
| 4.1 | Load each archetype in sequence | All load correctly | Any fails to load |
| 4.2 | Craft metrics JSON populated for each | All show TWR, delta-V, pattern | Any missing data |
| 4.3 | Staging pattern detected correctly for each | Matches archetype definition | Misdetection |
| 4.4 | Generate scripts for all 4 without closing panel | All generate | Any fails or freezes |
| 4.5 | No memory leak (check KSP.log) | No out-of-memory warnings | Memory warnings |
| 4.6 | No exceptions in log | Clean log | Exceptions present |

---

## 5. Failure Modes Per Archetype

### 5.1 Low TWR Failures

| Symptom | Suspected Cause | Investigation |
|---------|-----------------|---------------|
| Turn starts too early (<150m) | TWR not passed to prompt | Check `PromptBuilder` TWR injection |
| Pitch too aggressive | Wrong TWR bracket detected | Check TWR threshold logic |
| Missing staging | Stack pattern not detected | Check `StagingSummary` output |
| Script incomplete | Token limit hit | Check response truncation |

### 5.2 High TWR Failures

| Symptom | Suspected Cause | Investigation |
|---------|-----------------|---------------|
| Turn starts same as Low TWR | TWR guidance not differentiated | Check TWR bracket handling in prompt |
| No throttle management | High-TWR guidance missing | Check prompt for throttle instructions |
| Flip risk not addressed | Missing stability guidance | Add prograde-lock before turn |
| Overly conservative turn | Brackets misconfigured | Check TWR thresholds |

### 5.3 Asparagus Failures

| Symptom | Suspected Cause | Investigation |
|---------|-----------------|---------------|
| Pattern shows "Stack" | Detection logic too simple | Check `StagingSummary.DetectPattern()` |
| No symmetry guards | Asparagus guidance missing | Check prompt templates |
| Single staging event | Booster stages not counted | Check stage enumeration |
| Asymmetric staging | No symmetry wait logic | Add fuel-balance checks |

### 5.4 SSTO Failures

| Symptom | Suspected Cause | Investigation |
|---------|-----------------|---------------|
| **STAGE command present** | `isSingleStage` not detected | Check `StagingSummary.isSingleStage` |
| No mode switching guidance | Engine types not analyzed | Check engine type detection |
| Uses staging-based fuel logic | SSTO guidance ignored | Check prompt conditional |
| Missing closed-cycle note | SSTO template incomplete | Update prompt template |

---

## 6. Test Transcript Template

Use this template to record test runs for regression tracking.

```markdown
## Test Run: [YYYY-MM-DD HH:MM]

**Tester:** [name]
**Build:** [commit hash or version]
**KSP Version:** [1.x.x]
**kOS Version:** [x.x.x]

### Craft Under Test
- **Name:** [craft name]
- **Archetype:** [Low TWR / High TWR / Asparagus / SSTO]
- **TWR (ASL/Vac):** [x.xx / x.xx]
- **Delta-V:** [xxxx m/s]
- **Detected Pattern:** [Stack / Asparagus / SingleStage]
- **Grounded Mode:** [ON / OFF]

### Generated Script (excerpt)
```kos
// Paste relevant portions here
// Focus on: gravity turn, staging, throttle management
```

### Checklist Results
| Check | Result |
|-------|--------|
| TWR guidance appropriate | [PASS/FAIL] |
| Staging logic correct | [PASS/FAIL] |
| No unverified APIs | [PASS/FAIL] |
| No UI freeze | [PASS/FAIL] |
| SSTO: No STAGE commands | [N/A or PASS/FAIL] |

### Verdict: [PASS / FAIL]

### Notes
[Any observations, unexpected behaviors, or suggestions]

### Log Excerpts (if failure)
```
[Paste relevant KSP.log lines]
```
```

---

## Quick Pass Summary

After completing all archetype tests:

- [ ] Low TWR: Gentle turn, appropriate staging
- [ ] High TWR: Aggressive turn, throttle management
- [ ] Asparagus: Symmetry-aware staging
- [ ] SSTO: **Zero STAGE commands**
- [ ] All archetypes: No unverified kOS APIs (Grounded ON)
- [ ] All archetypes: No UI freezes
- [ ] Cross-archetype: No memory leaks or exceptions

If all boxes checked: **PASS**

Any unchecked: Document failure using transcript template above.

---

## Related Tests

- **[SMOKE_TEST.md](./SMOKE_TEST.md)** — Basic mod functionality. Run first.
- **[TEACH_DO_TRANSCRIPT.md](./TEACH_DO_TRANSCRIPT.md)** — Teach/Do mode validation.

---

## Troubleshooting

### Craft metrics not appearing
1. Verify craft has engines with thrust data
2. Check `ReadinessMetrics` is populating correctly
3. Look for exceptions in KSP.log during panel open

### Wrong staging pattern detected
1. Check fuel line connections in VAB
2. Verify `StagingSummary.DetectPattern()` logic
3. Test with simpler craft first

### Scripts always look the same regardless of TWR
1. Verify TWR is being passed to prompt builder
2. Check prompt template has TWR-conditional sections
3. Enable verbose logging to see prompt content

### SSTO outputs STAGE commands
1. Check `StagingSummary.isSingleStage` returns true
2. Verify prompt has SSTO-specific conditional
3. Check if multiple engine types confuse detection

### Grounded mode not affecting output
1. Verify grounded mode setting is being read
2. Check that kOS docs search tool is being invoked
3. Compare tool call logs between modes
