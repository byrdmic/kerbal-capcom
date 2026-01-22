# CAPCOM System Prompts

This directory contains example outputs from the PromptBuilder for manual inspection and debugging.

## Files

- `example_prompt_teach.txt` - Full system prompt in TEACH mode (explanatory, educational)
- `example_prompt_do.txt` - Full system prompt in DO mode (actionable steps, concise)

## Purpose

These files serve as:
1. **Documentation** - Human-readable reference for what CAPCOM "sees" as its instructions
2. **Acceptance testing** - Verify prompts contain required elements (CAPCOM identity, no-autopilot constraint, mode-specific instructions)
3. **Debugging** - Compare expected vs actual prompts when troubleshooting LLM behavior

## Key Elements to Verify

When reviewing these prompts, check for:

- **CAPCOM Identity**: References to "CAPCOM", "capsule communicator", "Mission Control"
- **No-Autopilot Constraint**: "do NOT pilot the spacecraft directly", emphasis on guidance over control
- **Mode Instructions**: "MODE: TEACH" or "MODE: DO" with appropriate behavioral guidance
- **KSP Context**: Terminology like "periapsis", "delta-v", "kOS", "Kerbals"

## Regenerating Examples

To regenerate these files with current prompt content:

```csharp
// In a test or utility method:
var teachPrompt = PromptBuilder.BuildPromptForMode(OperationMode.Teach);
var doPrompt = PromptBuilder.BuildPromptForMode(OperationMode.Do);
File.WriteAllText("example_prompt_teach.txt", teachPrompt);
File.WriteAllText("example_prompt_do.txt", doPrompt);
```

Or run the `GenerateExamplePrompts` test in `PromptBuilderTests.cs` (if available).

## Version

These examples correspond to PromptBuilder v1.1.0.
