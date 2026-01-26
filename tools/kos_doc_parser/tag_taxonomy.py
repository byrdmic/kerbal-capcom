"""Tag taxonomy for kOS documentation entries.

Defines domain-specific tags and rules for automatic tag assignment
to improve retrieval quality.
"""

from typing import Dict, List, Set
import re

# Domain tags - primary categorization
DOMAIN_TAGS = {
    "orbit": "Orbital mechanics and trajectory",
    "vessel": "Vessel properties and state",
    "control": "Throttle, steering, and autopilot",
    "navigation": "Heading, waypoints, and directions",
    "staging": "Stage management and separation",
    "fuel": "Propellant and resource consumption",
    "communication": "Antennas and CommNet",
    "science": "Experiments and science data",
    "part": "Part modules and components",
    "body": "Celestial bodies and planets",
    "time": "Time, scheduling, and delays",
    "math": "Mathematical functions and operations",
    "io": "File I/O and terminal output",
    "gui": "Graphical user interface elements",
}

# Concept tags - secondary categorization
CONCEPT_TAGS = {
    "position": "Positions and coordinates",
    "velocity": "Velocity and speed",
    "maneuver": "Maneuver nodes and planning",
    "autopilot": "Automated flight control",
    "resource": "Resources (fuel, electricity, etc.)",
    "trajectory": "Flight paths and predictions",
    "vector": "Vector operations",
    "direction": "Direction and heading",
    "rotation": "Rotation and attitude",
}

# Usage tags - frequency/importance hints
USAGE_TAGS = {
    "core": "Fundamental/essential feature",
    "advanced": "Complex or specialized feature",
    "deprecated": "Deprecated feature",
}

# All tag descriptions combined
ALL_TAG_DESCRIPTIONS = {
    **DOMAIN_TAGS,
    **CONCEPT_TAGS,
    **USAGE_TAGS,
    # Additional specific tags
    "flight": "In-flight operations",
    "collection": "Lists, lexicons, and iterators",
    "language": "kOS language constructs",
    "binding": "Variable bindings and locks",
    "trigger": "Event triggers (WHEN/ON)",
    "numeric": "Numeric values",
    "boolean": "Boolean values",
    "string": "String operations",
    "constant": "Constant values",
    "function": "Built-in functions",
    "command": "Commands and statements",
    "keyword": "Language keywords",
    "crew": "Kerbal crew management",
    "action": "Action groups",
    "systems": "Ship systems (SAS, RCS, etc.)",
    "docking": "Docking operations",
    "landing": "Landing and touchdown",
    "atmosphere": "Atmospheric flight",
}

# Pattern-based tag assignment rules
# Maps patterns to tags that should be assigned
TAG_PATTERNS: Dict[str, List[str]] = {
    # Structure patterns
    r"^VESSEL(:|$)": ["vessel", "core"],
    r"^SHIP(:|$)": ["vessel", "core"],
    r"^ORBIT(:|$)": ["orbit"],
    r"^BODY(:|$)": ["body"],
    r"^PART(:|$)": ["part"],
    r"^ENGINE(:|$)": ["part", "control"],
    r"^SENSOR(:|$)": ["part", "science"],
    r"^SCIENCE": ["science"],
    r"^ANTENNA": ["communication", "part"],
    r"^COMM": ["communication"],
    r"^DOCK": ["docking", "part"],
    r"^CREW": ["crew"],
    r"^KERBAL": ["crew"],
    r"^MANEUVER": ["maneuver", "orbit"],
    r"^NODE(:|$)": ["maneuver", "orbit"],
    r"^STAGE(:|$)": ["staging"],
    r"^RESOURCE": ["resource", "fuel"],
    r"^GUI": ["gui"],
    r"^WIDGET": ["gui"],
    r"^FILE": ["io"],
    r"^VOLUME": ["io"],
    r"^TIME": ["time"],
    r"^TIMESTAMP": ["time"],
    r"^LIST(:|$)": ["collection"],
    r"^LEXICON": ["collection"],
    r"^ITERATOR": ["collection"],
    r"^RANGE": ["collection"],
    r"^QUEUE": ["collection"],
    r"^STACK": ["collection"],
    r"^VECTOR(:|$)": ["vector", "math"],
    r"^DIRECTION(:|$)": ["direction", "navigation"],
    r"^HEADING": ["direction", "navigation"],
    r"^GEOPOSITION": ["position", "navigation", "body"],
    r"^LATLNG": ["position", "navigation"],
    r"^ATMOSPHERE": ["atmosphere", "body"],
    r"^WAYPOINT": ["navigation"],

    # Suffix patterns (ID contains colon)
    r":ALTITUDE$": ["vessel", "position"],
    r":APOAPSIS$": ["orbit"],
    r":PERIAPSIS$": ["orbit"],
    r":INCLINATION$": ["orbit"],
    r":ECCENTRICITY$": ["orbit"],
    r":SEMIMAJORAXIS$": ["orbit"],
    r":VELOCITY$": ["velocity"],
    r":POSITION$": ["position"],
    r":FACING$": ["direction", "rotation"],
    r":UP$": ["direction"],
    r":NORTH$": ["direction"],
    r":HEADING$": ["direction", "navigation"],
    r":THRUST$": ["control"],
    r":THROTTLE$": ["control"],
    r":MASS$": ["vessel"],
    r":DRYMASS$": ["vessel", "fuel"],
    r":WETMASS$": ["vessel", "fuel"],
    r":MAXTHRUST$": ["control", "part"],
    r":ISP$": ["control", "fuel"],
    r":STAGE$": ["staging"],
    r":RESOURCES$": ["resource"],
    r":FUEL$": ["fuel", "resource"],
    r":LIQUIDFUEL$": ["fuel", "resource"],
    r":OXIDIZER$": ["fuel", "resource"],
    r":MONOPROPELLANT$": ["fuel", "resource"],
    r":ELECTRICCHARGE$": ["resource"],
    r":CREW$": ["crew"],
    r":PARTS$": ["part"],
    r":ENGINES$": ["part", "control"],
    r":SENSORS$": ["part", "science"],
    r":ETA$": ["time", "orbit"],
    r":PERIOD$": ["time", "orbit"],
    r":BODY$": ["body"],
    r":SOI$": ["body", "orbit"],
    r":ATMOSPHERE$": ["atmosphere", "body"],
    r":HASATMOSPHERE$": ["atmosphere", "body"],
    r":STATUS$": ["vessel"],
    r":SITUATION$": ["vessel"],
    r":CONTROL$": ["control"],
    r":CONNECTION$": ["communication"],
    r":SIGNAL$": ["communication"],
    r":EXPERIMENTS$": ["science"],
    r":DATA$": ["science"],
    r":MAG$": ["vector", "math"],
    r":NORMALIZED$": ["vector", "math"],
    r":X$": ["vector", "position"],
    r":Y$": ["vector", "position"],
    r":Z$": ["vector", "position"],
    r":ROLL$": ["rotation", "direction"],
    r":PITCH$": ["rotation", "direction"],
    r":YAW$": ["rotation", "direction"],
    r":TARGET$": ["navigation"],
    r":HASTARGET$": ["navigation"],
}

# Keywords that indicate domain tags
KEYWORD_TAG_HINTS: Dict[str, List[str]] = {
    # Control keywords
    "THROTTLE": ["control", "core"],
    "STEERING": ["control", "navigation", "core"],
    "LOCK": ["binding", "core"],
    "UNLOCK": ["binding"],
    "SAS": ["control", "autopilot", "systems"],
    "RCS": ["control", "systems"],
    "GEAR": ["systems", "landing"],
    "LIGHTS": ["systems"],
    "BRAKES": ["systems", "landing"],
    "ABORT": ["systems", "staging"],
    "AG1": ["action", "systems"],
    "AG2": ["action", "systems"],
    "AG3": ["action", "systems"],
    "AG4": ["action", "systems"],
    "AG5": ["action", "systems"],
    "AG6": ["action", "systems"],
    "AG7": ["action", "systems"],
    "AG8": ["action", "systems"],
    "AG9": ["action", "systems"],
    "AG10": ["action", "systems"],

    # Navigation keywords
    "PROGRADE": ["direction", "navigation", "orbit"],
    "RETROGRADE": ["direction", "navigation", "orbit"],
    "NORMAL": ["direction", "navigation", "orbit"],
    "ANTINORMAL": ["direction", "navigation", "orbit"],
    "RADIAL": ["direction", "navigation", "orbit"],
    "ANTIRADIAL": ["direction", "navigation", "orbit"],
    "SRFPROGRADE": ["direction", "navigation", "flight"],
    "SRFRETROGRADE": ["direction", "navigation", "flight"],
    "TARGET": ["navigation"],

    # Flow control
    "WAIT": ["time", "core"],
    "WHEN": ["trigger"],
    "ON": ["trigger"],
    "UNTIL": ["language", "core"],
    "IF": ["language", "core"],
    "ELSE": ["language", "core"],
    "FOR": ["language", "collection"],
    "FROM": ["language"],
    "SET": ["language", "core"],
    "PRINT": ["io", "core"],
    "LOG": ["io"],
    "RUN": ["language", "core"],
    "RUNPATH": ["language", "io"],
    "RUNONCEPATH": ["language", "io"],
    "STAGE": ["staging", "core"],
    "REBOOT": ["systems"],
    "SHUTDOWN": ["systems"],

    # Math functions
    "ABS": ["math", "function"],
    "CEILING": ["math", "function"],
    "FLOOR": ["math", "function"],
    "ROUND": ["math", "function"],
    "SQRT": ["math", "function"],
    "LN": ["math", "function"],
    "LOG10": ["math", "function"],
    "MIN": ["math", "function"],
    "MAX": ["math", "function"],
    "MOD": ["math", "function"],
    "SIN": ["math", "function"],
    "COS": ["math", "function"],
    "TAN": ["math", "function"],
    "ARCSIN": ["math", "function"],
    "ARCCOS": ["math", "function"],
    "ARCTAN": ["math", "function"],
    "ARCTAN2": ["math", "function"],
    "RANDOM": ["math", "function"],

    # Vector/direction functions
    "V": ["vector", "math", "function"],
    "R": ["direction", "rotation", "function"],
    "Q": ["rotation", "function"],
    "HEADING": ["direction", "navigation", "function"],
    "LOOKDIRUP": ["direction", "rotation", "function"],
    "ANGLEAXIS": ["rotation", "math", "function"],
    "ROTATEFROMTO": ["rotation", "math", "function"],
    "VCRS": ["vector", "math", "function"],
    "VDOT": ["vector", "math", "function"],
    "VANG": ["vector", "math", "function"],
    "VXCL": ["vector", "math", "function"],
}

# Return type to tag mapping
RETURN_TYPE_TAGS: Dict[str, List[str]] = {
    "vector": ["vector"],
    "direction": ["direction"],
    "scalar": ["numeric"],
    "number": ["numeric"],
    "boolean": ["boolean"],
    "bool": ["boolean"],
    "string": ["string"],
    "list": ["collection"],
    "lexicon": ["collection"],
    "iterator": ["collection"],
    "timespan": ["time"],
    "timestamp": ["time"],
    "geoposition": ["position", "navigation"],
    "vessel": ["vessel"],
    "body": ["body"],
    "orbit": ["orbit"],
    "part": ["part"],
}


def get_tags_from_patterns(entry_id: str) -> Set[str]:
    """Get tags that match patterns in the entry ID."""
    tags = set()
    entry_upper = entry_id.upper()

    for pattern, pattern_tags in TAG_PATTERNS.items():
        if re.search(pattern, entry_upper):
            tags.update(pattern_tags)

    return tags


def get_tags_from_keyword(name: str) -> Set[str]:
    """Get tags for known keywords."""
    name_upper = name.upper()
    if name_upper in KEYWORD_TAG_HINTS:
        return set(KEYWORD_TAG_HINTS[name_upper])
    return set()


def get_tags_from_return_type(return_type: str) -> Set[str]:
    """Get tags based on return type."""
    if not return_type:
        return set()

    tags = set()
    type_lower = return_type.lower()

    for type_pattern, type_tags in RETURN_TYPE_TAGS.items():
        if type_pattern in type_lower:
            tags.update(type_tags)

    return tags


def get_tags_from_description(description: str) -> Set[str]:
    """Infer tags from description content."""
    if not description:
        return set()

    tags = set()
    desc_lower = description.lower()

    # Keywords to look for in descriptions
    description_hints = {
        "orbit": ["orbit", "orbital", "apoapsis", "periapsis", "inclination"],
        "maneuver": ["maneuver", "burn", "delta-v", "deltav"],
        "velocity": ["velocity", "speed"],
        "position": ["position", "coordinate", "location"],
        "autopilot": ["autopilot", "auto-pilot", "cooked control"],
        "trajectory": ["trajectory", "path", "prediction"],
        "atmosphere": ["atmosphere", "atmospheric", "air"],
        "docking": ["dock", "docking", "port"],
        "landing": ["land", "landing", "touchdown", "gear"],
        "staging": ["stage", "staging", "decouple", "separate"],
    }

    for tag, keywords in description_hints.items():
        for keyword in keywords:
            if keyword in desc_lower:
                tags.add(tag)
                break

    return tags


def assign_tags_to_entry(entry) -> List[str]:
    """Assign comprehensive tags to an entry based on all available information."""
    tags = set(entry.tags)  # Start with existing tags

    # Add tags from patterns (based on ID)
    tags.update(get_tags_from_patterns(entry.id))

    # Add tags from keyword hints (based on name)
    tags.update(get_tags_from_keyword(entry.name))

    # Add tags from return type
    tags.update(get_tags_from_return_type(entry.return_type))

    # Add tags from description
    tags.update(get_tags_from_description(entry.description))

    # Add type-based tags
    from .models import DocEntryType
    type_tags = {
        DocEntryType.STRUCTURE: ["structure"],
        DocEntryType.SUFFIX: [],  # Don't add redundant "suffix" tag
        DocEntryType.FUNCTION: ["function"],
        DocEntryType.KEYWORD: ["keyword"],
        DocEntryType.CONSTANT: ["constant"],
        DocEntryType.COMMAND: ["command"],
    }
    if entry.type in type_tags:
        tags.update(type_tags[entry.type])

    # Mark deprecated entries
    if entry.deprecated:
        tags.add("deprecated")

    # Ensure minimum tag count - add fallback if needed
    if len(tags) < 2:
        # Try to infer from parent structure
        if entry.parent_structure:
            parent_tags = get_tags_from_patterns(entry.parent_structure)
            tags.update(parent_tags)

        # Still not enough? Add generic fallback
        if len(tags) < 2:
            tags.add("misc")

    return sorted(list(tags))


def get_all_tag_descriptions() -> Dict[str, str]:
    """Get descriptions for all known tags."""
    return dict(ALL_TAG_DESCRIPTIONS)
