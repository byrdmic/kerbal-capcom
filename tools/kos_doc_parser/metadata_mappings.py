"""Metadata mappings for kOS documentation entries.

Provides category assignments and usage frequency classifications
for improved retrieval relevance.
"""

from typing import Dict, Set, Optional

# Category definitions
# Maps structure names (or prefixes) to human-readable categories
STRUCTURE_CATEGORIES: Dict[str, str] = {
    # Vessel Properties
    "VESSEL": "Vessel Properties",
    "SHIP": "Vessel Properties",
    "ORBITABLE": "Vessel Properties",
    "VESSELALT": "Vessel Properties",
    "BOUNDS": "Vessel Properties",
    "CREWMEMBER": "Vessel Properties",

    # Orbital Mechanics
    "ORBIT": "Orbital Mechanics",
    "ORBITETA": "Orbital Mechanics",
    "ORBITINFO": "Orbital Mechanics",

    # Celestial Bodies
    "BODY": "Celestial Bodies",
    "ATMOSPHERE": "Celestial Bodies",
    "GEOPOSITION": "Celestial Bodies",
    "LATLNG": "Celestial Bodies",
    "WAYPOINT": "Celestial Bodies",

    # Parts & Modules
    "PART": "Parts & Modules",
    "PARTMODULE": "Parts & Modules",
    "ENGINE": "Parts & Modules",
    "GIMBAL": "Parts & Modules",
    "DECOUPLER": "Parts & Modules",
    "SEPARATOR": "Parts & Modules",
    "DOCKINGPORT": "Parts & Modules",
    "SENSOR": "Parts & Modules",
    "LAUNCHCLAMP": "Parts & Modules",
    "RCS": "Parts & Modules",
    "SOLARPANEL": "Parts & Modules",
    "CONSUMEDRESOURCE": "Parts & Modules",

    # Resources
    "RESOURCE": "Resources",
    "AGGREGATERESOURCE": "Resources",
    "STAGEVALUES": "Resources",

    # Flight Control
    "CONTROL": "Flight Control",
    "STEERINGMANAGER": "Flight Control",
    "PIDLOOP": "Flight Control",

    # Maneuvers & Navigation
    "MANEUVERNODE": "Maneuvers & Navigation",
    "NODE": "Maneuvers & Navigation",
    "DIRECTION": "Maneuvers & Navigation",
    "HEADING": "Maneuvers & Navigation",

    # Math & Vectors
    "VECTOR": "Math & Vectors",
    "SCALAR": "Math & Vectors",
    "CONSTANT": "Math & Vectors",

    # Collections
    "LIST": "Collections",
    "LEXICON": "Collections",
    "ITERATOR": "Collections",
    "RANGE": "Collections",
    "QUEUE": "Collections",
    "STACK": "Collections",
    "UNIQUESET": "Collections",

    # I/O & Communication
    "FILE": "I/O & Communication",
    "VOLUME": "I/O & Communication",
    "VOLUMEFILE": "I/O & Communication",
    "VOLUMEDIRECTORY": "I/O & Communication",
    "VOLUMEITEM": "I/O & Communication",
    "CORE": "I/O & Communication",
    "PROCESSOR": "I/O & Communication",
    "CONNECTION": "I/O & Communication",
    "MESSAGE": "I/O & Communication",
    "MESSAGEQUEUE": "I/O & Communication",

    # GUI
    "GUI": "GUI Elements",
    "WIDGET": "GUI Elements",
    "BOX": "GUI Elements",
    "BUTTON": "GUI Elements",
    "LABEL": "GUI Elements",
    "TEXTFIELD": "GUI Elements",
    "POPUPMENU": "GUI Elements",
    "SLIDER": "GUI Elements",
    "SCROLLBOX": "GUI Elements",
    "SPACING": "GUI Elements",
    "SKIN": "GUI Elements",
    "STYLE": "GUI Elements",
    "STYLESTATE": "GUI Elements",
    "STYLERECTSTYLE": "GUI Elements",
    "TIPDISPLAY": "GUI Elements",

    # Time
    "TIME": "Time",
    "TIMESPAN": "Time",
    "TIMESTAMP": "Time",
    "KUNIVERSE": "Time",

    # Science
    "SCIENCEDATA": "Science",
    "SCIENCEEXPERIMENT": "Science",
    "SCIENCEEXPERIMENTMODULE": "Science",

    # Career/Contracts
    "CAREER": "Career",
    "CONTRACT": "Career",
    "CONTRACTPARAMETER": "Career",
    "NOTE": "Career",

    # Colors & Styling
    "RGBA": "Colors & Styling",
    "HSVA": "Colors & Styling",
    "HIGHLIGHT": "Colors & Styling",
    "VECDRAW": "Colors & Styling",
    "VECDRAWARGS": "Colors & Styling",

    # Addons
    "ADDON": "Addons",
    "ADDONLIST": "Addons",
}

# Category for entry types
TYPE_CATEGORIES: Dict[str, str] = {
    "function": "Built-in Functions",
    "keyword": "Language Keywords",
    "command": "Commands",
    "constant": "Constants",
}

# High-frequency/commonly used entries
# These are the items that users query most often
COMMON_ENTRIES: Set[str] = {
    # Core control
    "THROTTLE",
    "STEERING",
    "LOCK",
    "UNLOCK",
    "STAGE",
    "SAS",
    "RCS",
    "GEAR",
    "BRAKES",
    "LIGHTS",

    # Direction constants
    "PROGRADE",
    "RETROGRADE",
    "NORMAL",
    "ANTINORMAL",
    "RADIAL",
    "ANTIRADIAL",
    "SRFPROGRADE",
    "SRFRETROGRADE",
    "UP",
    "NORTH",

    # Essential vessel suffixes
    "VESSEL:ALTITUDE",
    "VESSEL:APOAPSIS",
    "VESSEL:PERIAPSIS",
    "VESSEL:VELOCITY",
    "VESSEL:FACING",
    "VESSEL:MASS",
    "VESSEL:THRUST",
    "VESSEL:MAXTHRUST",
    "VESSEL:ORBIT",
    "VESSEL:BODY",
    "VESSEL:STATUS",
    "VESSEL:PARTS",
    "VESSEL:ENGINES",

    # Ship shortcuts
    "SHIP",
    "SHIP:ALTITUDE",
    "ALTITUDE",
    "APOAPSIS",
    "PERIAPSIS",
    "VELOCITY",

    # Orbit suffixes
    "ORBIT:APOAPSIS",
    "ORBIT:PERIAPSIS",
    "ORBIT:ECCENTRICITY",
    "ORBIT:INCLINATION",
    "ORBIT:PERIOD",
    "ORBIT:SEMIMAJORAXIS",
    "ORBIT:ETA",
    "ORBIT:BODY",

    # Body suffixes
    "BODY:NAME",
    "BODY:MASS",
    "BODY:RADIUS",
    "BODY:MU",
    "BODY:ATM",
    "BODY:ATMOSPHERE",

    # ETA shortcuts
    "ETA:APOAPSIS",
    "ETA:PERIAPSIS",
    "ETA:TRANSITION",

    # Maneuver nodes
    "MANEUVERNODE",
    "NODE",
    "NEXTNODE",
    "HASNODE",
    "ADD",
    "REMOVE",

    # Math functions
    "ABS",
    "MIN",
    "MAX",
    "SQRT",
    "ROUND",
    "FLOOR",
    "CEILING",
    "MOD",
    "SIN",
    "COS",
    "TAN",
    "ARCSIN",
    "ARCCOS",
    "ARCTAN",
    "ARCTAN2",

    # Vector functions
    "V",
    "R",
    "Q",
    "HEADING",
    "LOOKDIRUP",
    "VCRS",
    "VDOT",
    "VANG",
    "VXCL",

    # Vector suffixes
    "VECTOR:MAG",
    "VECTOR:NORMALIZED",
    "VECTOR:X",
    "VECTOR:Y",
    "VECTOR:Z",

    # Time
    "TIME",
    "TIME:SECONDS",
    "WAIT",
    "WARP",
    "KUNIVERSE",

    # Flow control
    "IF",
    "ELSE",
    "UNTIL",
    "FOR",
    "WHEN",
    "ON",
    "RETURN",
    "BREAK",
    "PRESERVE",

    # I/O
    "PRINT",
    "LOG",
    "CLEARSCREEN",
    "RUN",
    "RUNPATH",

    # Lists
    "LIST",
    "LIST:ADD",
    "LIST:REMOVE",
    "LIST:LENGTH",
    "LIST:CLEAR",

    # Target
    "TARGET",
    "HASTARGET",

    # Part access
    "PART:NAME",
    "PART:MASS",
    "PART:MODULES",
    "PART:TAG",
    "PART:STAGE",
    "PARTSTAGGED",
    "PARTSNAMED",
    "PARTSDUBBED",
    "PARTSINGROUP",

    # Engine suffixes
    "ENGINE:THRUST",
    "ENGINE:MAXTHRUST",
    "ENGINE:ISP",
    "ENGINE:ACTIVATE",
    "ENGINE:SHUTDOWN",

    # Resources
    "RESOURCE:AMOUNT",
    "RESOURCE:CAPACITY",
    "STAGE:LIQUIDFUEL",
    "STAGE:OXIDIZER",
    "STAGE:MONOPROPELLANT",
    "STAGE:ELECTRICCHARGE",

    # Action groups
    "AG1",
    "AG2",
    "AG3",
    "AG4",
    "AG5",
    "AG6",
    "AG7",
    "AG8",
    "AG9",
    "AG10",
    "ABORT",
}

# Rare/specialized entries
# These are infrequently used or deprecated
RARE_ENTRIES: Set[str] = {
    # Deprecated items
    "SURFACESPEED",  # Deprecated in favor of GROUNDSPEED
    "VERTICALSPEED",  # Use VESSEL:VERTICALSPEED
    "TERMVELOCITY",  # Deprecated

    # Obscure GUI elements
    "STYLESTATE",
    "STYLERECTSTYLE",
    "STYLERECTOFFSET",
    "TIPDISPLAY",
    "SKIN:ADD",

    # Specialized addons
    "ADDON:AGX",
    "ADDON:IR",
    "ADDON:KAC",
    "ADDON:RT",
    "ADDON:SCANSAT",

    # Low-level processor stuff
    "PROCESSOR:MODE",
    "PROCESSOR:BOOTFILENAME",

    # Uncommon math
    "CONSTANT:AVOGADRO",
    "CONSTANT:BOLTZMANN",
    "CONSTANT:IDEALGAS",

    # Volume management (specialized)
    "VOLUME:FREESPACE",
    "VOLUME:POWERREQUIREMENT",
    "VOLUME:FILES",
    "VOLUMEFILE:WRITE",
    "VOLUMEFILE:WRITELN",
    "VOLUMEFILE:READALL",

    # Message queue (advanced IPC)
    "MESSAGE:SENT",
    "MESSAGE:RECEIVEDAT",
    "MESSAGE:SENDER",
    "MESSAGEQUEUE:EMPTY",
    "MESSAGEQUEUE:LENGTH",

    # Career mode (rarely used in automation)
    "CAREER:CANTRACKOBJECTS",
    "CAREER:PATCHLIMIT",
    "CONTRACT:STATE",
    "CONTRACT:DEADLINE",

    # Highlight (debugging/visualization)
    "HIGHLIGHT:ENABLED",
    "HIGHLIGHT:COLOR",
}


def get_category_for_entry(entry) -> Optional[str]:
    """Determine the category for an entry.

    Args:
        entry: A DocEntry object

    Returns:
        Category string or None if no category applies
    """
    from .models import DocEntryType

    # Check type-based category first
    if entry.type == DocEntryType.FUNCTION:
        return TYPE_CATEGORIES.get("function", "Built-in Functions")
    if entry.type == DocEntryType.KEYWORD:
        return TYPE_CATEGORIES.get("keyword", "Language Keywords")
    if entry.type == DocEntryType.COMMAND:
        return TYPE_CATEGORIES.get("command", "Commands")
    if entry.type == DocEntryType.CONSTANT:
        return TYPE_CATEGORIES.get("constant", "Constants")

    # For structures and suffixes, check parent or self
    structure_name = entry.parent_structure or entry.name

    # Direct match
    if structure_name.upper() in STRUCTURE_CATEGORIES:
        return STRUCTURE_CATEGORIES[structure_name.upper()]

    # Prefix match (for things like GUI:BUTTON -> GUI)
    parts = structure_name.upper().split(":")
    if parts[0] in STRUCTURE_CATEGORIES:
        return STRUCTURE_CATEGORIES[parts[0]]

    # Check if entry ID starts with known structure
    id_upper = entry.id.upper()
    for struct, category in STRUCTURE_CATEGORIES.items():
        if id_upper.startswith(struct + ":") or id_upper == struct:
            return category

    return None


def get_usage_frequency(entry) -> str:
    """Determine the usage frequency for an entry.

    Args:
        entry: A DocEntry object

    Returns:
        "common", "moderate", or "rare"
    """
    entry_id = entry.id.upper()
    name_upper = entry.name.upper()

    # Check if deprecated
    if entry.deprecated:
        return "rare"

    # Check common entries (exact match first)
    if entry_id in COMMON_ENTRIES or name_upper in COMMON_ENTRIES:
        return "common"

    # Check rare entries
    if entry_id in RARE_ENTRIES or name_upper in RARE_ENTRIES:
        return "rare"

    # Check if parent structure is common (suffixes of common things are moderate)
    if entry.parent_structure:
        parent_upper = entry.parent_structure.upper()
        if parent_upper in COMMON_ENTRIES or f"{parent_upper}:{name_upper}" in COMMON_ENTRIES:
            return "moderate"

    # Check prefix patterns for common items
    common_prefixes = ["VESSEL:", "SHIP:", "ORBIT:", "BODY:", "VECTOR:", "DIRECTION:"]
    for prefix in common_prefixes:
        if entry_id.startswith(prefix):
            return "moderate"

    # GUI and addon items are generally specialized
    if "GUI" in entry_id or "ADDON" in entry_id:
        return "rare"

    # Default to moderate
    return "moderate"


# Lists all categories for documentation
ALL_CATEGORIES = sorted(set(STRUCTURE_CATEGORIES.values()) | set(TYPE_CATEGORIES.values()))
