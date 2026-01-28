# Plan: Staging Logic Generation for LKO Ascent Scripts

## Context

This task implements staging logic generation for kOS ascent scripts in the KSP Capcom mod. The mod currently generates LKO ascent scripts via `ChatPanel.OnAscentScriptClick()` using a fixed prompt `ASCENT_SCRIPT_PROMPT`, but does **not** include staging logic derived from the craft's actual stage structure.

### Key Files

- `src/KSPCapcom/PromptBuilder.cs` - Builds system prompts; has `LKOAscentGuidance` constant and `BuildCraftContext()` method
- `src/KSPCapcom/Editor/EditorCraftSnapshot.cs` - Captures craft data including `StagingSummary` and `Decouplers`
- `src/KSPCapcom/Editor/StagingSummary.cs` - Contains `StageInfo` with `HasDecoupler`, `HasEngine` per stage
- `src/KSPCapcom/ChatPanel.cs` - Triggers ascent script generation; uses canonical prompt
- `src/KSPCapcom/Validation/KosIdentifierExtractor.cs` - Extracts kOS identifiers; already knows `STAGE` is a keyword

### Current State

1. `EditorCraftSnapshot` already captures staging metadata:
   - `Staging.StageCount` - total stages
   - `Staging.Stages[]` - per-stage info with `HasDecoupler` and `HasEngine`
   - `Decouplers` list with count

2. `ToCraftMetricsBlock()` outputs JSON but does **not** include detailed per-stage breakdown

3. `LKOAscentGuidance` in `PromptBuilder.cs` mentions TWR and gravity turn but has **no staging guidance**

4. Grounded mode validates kOS identifiers - `STAGE` is already in the Keywords set

---

## Instructions

### Phase 1: Enhance Craft Metrics to Include Staging Detail

1. **Update `EditorCraftSnapshot.ToCraftMetricsBlock()`** to include per-stage breakdown:
   ```json
   "staging": {
     "stageCount": 3,
     "stages": [
       {"stageNumber": 0, "hasEngine": true, "hasDecoupler": false},
       {"stageNumber": 1, "hasEngine": true, "hasDecoupler": true},
       {"stageNumber": 2, "hasEngine": false, "hasDecoupler": true}
     ],
     "isSingleStage": false
   }
   ```

2. **Add helper method `GetStagingPattern()`** to `EditorCraftSnapshot` that detects:
   - Single-stage (no decouplers, single engine stage)
   - Simple stack (linear stages with decouplers)
   - Asparagus/parallel (multiple engines in same stage with radial decouplers)
   - Unknown/complex (fall back to conservative)

### Phase 2: Add Staging Guidance to PromptBuilder

3. **Create new constant `StagingLogicGuidance`** in `PromptBuilder.cs` with explicit instructions:
   - For single-stage craft: **Do not include STAGE commands**
   - For multi-stage craft: Include staging with fuel depletion/thrust loss detection
   - Include guards: time delays, fuel checks, avoid repeated STAGE in tight loops
   - Reference the staging JSON block for stage count and composition

4. **Update `GetLKOAscentGuidance()`** to append `StagingLogicGuidance` when staging data is available

### Phase 3: Ensure Grounded Mode Compliance

5. **Verify `STAGE` keyword handling** in `KosIdentifierExtractor.cs`:
   - `STAGE` is already in Keywords set (line 32) - confirm it won't be flagged as needing docs lookup
   - Add tests confirming `STAGE` is recognized as a keyword

6. **Add kOS staging documentation snippets** to the offline docs index (if not present):
   - `STAGE` command
   - `STAGE:READY` suffix
   - `STAGE:NUMBER` suffix
   - Fuel resource checking patterns

### Phase 4: Add Tests

7. **Add unit tests for staging pattern detection**:
   - Test single-stage detection
   - Test multi-stage with decouplers
   - Test asparagus pattern hints

8. **Add prompt builder tests**:
   - Verify staging guidance included when craft has stages
   - Verify staging guidance omitted for single-stage craft

---

## Constraints

- **No autopilot behavior**: Output is a kOS script for the player to run, not direct control from the mod
- **Grounded mode**: Staging-related identifiers must be verified by retrieved docs or trigger retrieval
- **Safe staging**: Generated scripts must not spam STAGE; include guards and time delays
- **Craft-aware**: Use snapshot data, never hard-code stage counts

---

## Output Format

Changes will be made to:
1. `src/KSPCapcom/Editor/EditorCraftSnapshot.cs` - Enhanced JSON output
2. `src/KSPCapcom/PromptBuilder.cs` - New staging guidance constant and integration
3. `src/KSPCapcom.Tests/Editor/CraftMetricsTests.cs` - New staging tests
4. `src/KSPCapcom.Tests/PromptBuilderTests.cs` - Staging guidance tests

---

## Definition of Done

- [ ] `ToCraftMetricsBlock()` includes per-stage breakdown with `hasEngine` and `hasDecoupler`
- [ ] Staging pattern detection method exists and classifies single/stack/asparagus/unknown
- [ ] `PromptBuilder` includes staging-specific guidance when craft has multiple stages
- [ ] Single-stage crafts get explicit "do not stage" guidance
- [ ] Multi-stage crafts get fuel-depletion staging logic guidance with guards
- [ ] `STAGE` keyword validated as not requiring docs lookup in grounded mode
- [ ] Unit tests pass for staging pattern detection
- [ ] Unit tests pass for prompt builder staging guidance inclusion/exclusion
