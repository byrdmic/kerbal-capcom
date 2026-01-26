# Building the kOS Documentation Index

This document describes how to regenerate the kOS documentation index (`kos_docs.json`) used by the KSP Capcom mod for contextual kOS assistance.

## Prerequisites

- **Python 3.8+** with pip
- Internet connection (to fetch kOS documentation)

## Quick Start

```bash
cd tools/kos_doc_parser
python -m kos_doc_parser
```

This will:
1. Fetch the latest kOS documentation from https://ksp-kos.github.io/KOS/
2. Parse structures, functions, keywords, commands, and constants
3. Apply the tag taxonomy and metadata enrichment
4. Write the index to `deploy/GameData/KSPCapcom/Plugins/kos_docs.json`

## Command-Line Options

```bash
python -m kos_doc_parser [options]

Options:
  -o, --output PATH     Output file path (default: ../../deploy/GameData/KSPCapcom/Plugins/kos_docs.json)
  --cache-dir PATH      Cache directory for HTTP responses (default: .cache)
  --no-cache            Bypass cache and fetch fresh content
  -v, --verbose         Enable verbose output
  --validate-only       Only validate existing JSON file, don't fetch
  --pretty              Pretty-print JSON output (default: true)
```

## Examples

```bash
# Regenerate with verbose output
python -m kos_doc_parser -v

# Regenerate without using cache (fresh fetch)
python -m kos_doc_parser --no-cache

# Output to a different location
python -m kos_doc_parser -o ./test_output.json
```

## Schema Version History

| Version | Changes |
|---------|---------|
| 1.0.0   | Initial schema with basic entry fields |
| 1.1.0   | Added `category` and `usageFrequency` fields; enhanced tag taxonomy |

## Tag Taxonomy

Tags are organized into three tiers:

### Domain Tags (Primary)
- `orbit` - Orbital mechanics and trajectory
- `vessel` - Vessel properties and state
- `control` - Throttle, steering, and autopilot
- `navigation` - Heading, waypoints, and directions
- `staging` - Stage management and separation
- `fuel` - Propellant and resource consumption
- `communication` - Antennas and CommNet
- `science` - Experiments and science data
- `part` - Part modules and components
- `body` - Celestial bodies and planets
- `time` - Time, scheduling, and delays
- `math` - Mathematical functions and operations
- `io` - File I/O and terminal output
- `gui` - Graphical user interface elements

### Concept Tags (Secondary)
- `position` - Positions and coordinates
- `velocity` - Velocity and speed
- `maneuver` - Maneuver nodes and planning
- `autopilot` - Automated flight control
- `resource` - Resources (fuel, electricity, etc.)
- `trajectory` - Flight paths and predictions
- `vector` - Vector operations
- `direction` - Direction and heading
- `rotation` - Rotation and attitude

### Usage Tags
- `core` - Fundamental/essential feature
- `advanced` - Complex or specialized feature
- `deprecated` - Deprecated feature

## Categories

Entries are grouped into these categories:

- **Vessel Properties** - VESSEL, SHIP, and related structures
- **Orbital Mechanics** - ORBIT and orbital calculations
- **Celestial Bodies** - BODY, ATMOSPHERE, GEOPOSITION
- **Parts & Modules** - PART, ENGINE, SENSOR, etc.
- **Resources** - RESOURCE, fuel tracking
- **Flight Control** - CONTROL, STEERINGMANAGER, PIDLOOP
- **Maneuvers & Navigation** - MANEUVERNODE, DIRECTION, HEADING
- **Math & Vectors** - VECTOR, mathematical functions
- **Collections** - LIST, LEXICON, ITERATOR
- **I/O & Communication** - FILE, VOLUME, MESSAGE
- **GUI Elements** - GUI widgets and styling
- **Time** - TIME, TIMESPAN, scheduling
- **Science** - Experiments and science data
- **Career** - Contracts and career mode
- **Built-in Functions** - Math functions, constructors
- **Language Keywords** - LOCK, IF, FOR, etc.
- **Commands** - PRINT, RUN, etc.
- **Constants** - PROGRADE, RETROGRADE, etc.

## Usage Frequency

Each entry is classified as:
- **common** - Frequently used items (THROTTLE, STEERING, basic orbit properties)
- **moderate** - Moderately used items (most structure suffixes)
- **rare** - Infrequently used items (deprecated, obscure GUI, specialized addons)

## Validation

The parser performs these validations:
- All entries have at least 2 tags
- All entries have a category
- All entries have a usage frequency
- Cross-references point to existing entries
- No duplicate entry IDs

## Output Format

The generated JSON has this structure:

```json
{
  "schemaVersion": "1.1.0",
  "contentVersion": "1.4.0.0",
  "kosMinVersion": "1.4.0.0",
  "generatedAt": "2024-01-15T10:30:00Z",
  "sourceUrl": "https://ksp-kos.github.io/KOS/",
  "entries": [
    {
      "id": "VESSEL:ALTITUDE",
      "name": "ALTITUDE",
      "type": "suffix",
      "parentStructure": "VESSEL",
      "returnType": "Scalar",
      "access": "get",
      "description": "The altitude above sea level...",
      "snippet": "PRINT SHIP:ALTITUDE.",
      "sourceRef": "https://...",
      "tags": ["vessel", "position", "navigation"],
      "category": "Vessel Properties",
      "usageFrequency": "common",
      "related": ["VESSEL:APOAPSIS", "VESSEL:PERIAPSIS"]
    }
  ],
  "tags": {
    "control": "Throttle, steering, and autopilot",
    "orbit": "Orbital mechanics and trajectory"
  }
}
```

## Troubleshooting

### "ModuleNotFoundError: No module named 'kos_doc_parser'"
Run from the `tools/kos_doc_parser` directory, or add it to PYTHONPATH.

### Rate limiting / connection errors
The parser caches HTTP responses by default. Use `--no-cache` to bypass.

### Missing entries
Essential entries (PROGRADE, THROTTLE, etc.) are added automatically if not found in the documentation.

### File too large
The index should be under 1MB. If significantly larger, check for duplicate entries.
