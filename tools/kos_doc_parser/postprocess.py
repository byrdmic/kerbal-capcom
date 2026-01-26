"""Post-processing for extracted documentation entries."""

import re
from collections import defaultdict
from typing import Dict, List, Set, Tuple

from .models import DocEntry, DocEntryType, DocAccessMode
from .tag_taxonomy import assign_tags_to_entry, get_all_tag_descriptions
from .metadata_mappings import get_category_for_entry, get_usage_frequency


# Essential entries that should always be present with fallback definitions
ESSENTIAL_ENTRIES = {
    # Direction constants
    "RETROGRADE": {
        "type": DocEntryType.CONSTANT,
        "return_type": "Direction",
        "access": DocAccessMode.GET,
        "description": "A direction pointing opposite to the vessel's orbital velocity. Used for deceleration burns.",
        "snippet": "LOCK STEERING TO RETROGRADE.",
        "tags": ["constant", "direction", "navigation", "orbit"],
        "related": ["PROGRADE", "NORMAL", "RADIAL"],
    },
    "ANTINORMAL": {
        "type": DocEntryType.CONSTANT,
        "return_type": "Direction",
        "access": DocAccessMode.GET,
        "description": "A direction pointing opposite to normal, perpendicular to the orbit plane.",
        "tags": ["constant", "direction", "navigation", "orbit"],
        "related": ["NORMAL", "PROGRADE", "RETROGRADE"],
    },
    "RADIAL": {
        "type": DocEntryType.CONSTANT,
        "return_type": "Direction",
        "access": DocAccessMode.GET,
        "description": "A direction pointing away from the body being orbited (radially outward).",
        "tags": ["constant", "direction", "navigation", "orbit"],
        "related": ["ANTIRADIAL", "PROGRADE", "NORMAL"],
    },
    "ANTIRADIAL": {
        "type": DocEntryType.CONSTANT,
        "return_type": "Direction",
        "access": DocAccessMode.GET,
        "description": "A direction pointing toward the body being orbited (radially inward).",
        "tags": ["constant", "direction", "navigation", "orbit"],
        "related": ["RADIAL", "PROGRADE", "NORMAL"],
    },
    "SRFPROGRADE": {
        "type": DocEntryType.CONSTANT,
        "return_type": "Direction",
        "access": DocAccessMode.GET,
        "description": "Surface prograde - the direction of travel relative to the surface.",
        "tags": ["constant", "direction", "navigation", "flight"],
        "related": ["SRFRETROGRADE", "PROGRADE"],
    },
    "SRFRETROGRADE": {
        "type": DocEntryType.CONSTANT,
        "return_type": "Direction",
        "access": DocAccessMode.GET,
        "description": "Surface retrograde - opposite to the direction of travel relative to the surface.",
        "tags": ["constant", "direction", "navigation", "flight"],
        "related": ["SRFPROGRADE", "RETROGRADE"],
    },
    # Core structures
    "VECTOR": {
        "type": DocEntryType.STRUCTURE,
        "description": "A 3D vector with X, Y, Z components. Used for positions, velocities, and directions. Create with V(x,y,z) function.",
        "snippet": "SET myVec TO V(1, 2, 3).\nPRINT myVec:MAG. // magnitude",
        "tags": ["math", "vector", "core"],
        "related": ["DIRECTION", "FUNCTION:V"],
    },
    # Common keywords that might be missed
    "THROTTLE": {
        "type": DocEntryType.KEYWORD,
        "return_type": "Scalar",
        "description": "A special variable that controls the vessel's throttle. Lock it to a value between 0.0 (no thrust) and 1.0 (full thrust).",
        "signature": "LOCK THROTTLE TO value.",
        "snippet": "LOCK THROTTLE TO 1.0.\nWAIT UNTIL SHIP:APOAPSIS > 80000.\nLOCK THROTTLE TO 0.",
        "tags": ["control", "flight", "keyword"],
        "related": ["LOCK", "STEERING"],
    },
    "STEERING": {
        "type": DocEntryType.KEYWORD,
        "return_type": "Direction",
        "description": "A special variable that controls the vessel's attitude. Lock it to a direction to have the autopilot point the ship.",
        "signature": "LOCK STEERING TO direction.",
        "snippet": "LOCK STEERING TO PROGRADE.\nWAIT 5.\nLOCK STEERING TO HEADING(90, 45).",
        "tags": ["control", "navigation", "flight", "keyword"],
        "related": ["LOCK", "THROTTLE", "PROGRADE"],
    },
}


def add_essential_entries(entries: List[DocEntry], base_url: str) -> List[DocEntry]:
    """Add essential entries that might be missing from the parsed docs."""
    existing_ids = {e.id for e in entries}
    added = 0

    for entry_id, entry_data in ESSENTIAL_ENTRIES.items():
        if entry_id not in existing_ids:
            entry = DocEntry(
                id=entry_id,
                name=entry_id,
                type=entry_data["type"],
                return_type=entry_data.get("return_type"),
                access=entry_data.get("access", DocAccessMode.NONE),
                signature=entry_data.get("signature"),
                description=entry_data.get("description"),
                snippet=entry_data.get("snippet"),
                source_ref=base_url,
                tags=entry_data.get("tags", []),
                related=entry_data.get("related", []),
            )
            entries.append(entry)
            added += 1

    if added > 0:
        print(f"  Added {added} essential fallback entries")

    return entries


def deduplicate_entries(entries: List[DocEntry]) -> List[DocEntry]:
    """Remove duplicate entries, preferring more detailed versions."""
    entry_map: Dict[str, DocEntry] = {}

    for entry in entries:
        existing = entry_map.get(entry.id)

        if existing is None:
            entry_map[entry.id] = entry
        else:
            # Prefer entry with more content
            existing_score = _entry_quality_score(existing)
            new_score = _entry_quality_score(entry)

            if new_score > existing_score:
                entry_map[entry.id] = entry
            else:
                # Merge some fields
                _merge_entry_fields(existing, entry)

    return list(entry_map.values())


def _entry_quality_score(entry: DocEntry) -> int:
    """Calculate a quality score for an entry."""
    score = 0

    if entry.description:
        score += min(len(entry.description), 200)
    if entry.snippet:
        score += 50
    if entry.tags:
        score += len(entry.tags) * 5
    if entry.related:
        score += len(entry.related) * 3
    if entry.source_ref and "#" in entry.source_ref:
        score += 10  # Has anchor

    return score


def _merge_entry_fields(target: DocEntry, source: DocEntry):
    """Merge non-empty fields from source into target."""
    if not target.snippet and source.snippet:
        target.snippet = source.snippet
    if not target.description and source.description:
        target.description = source.description

    # Merge tags
    for tag in source.tags:
        if tag not in target.tags:
            target.tags.append(tag)

    # Merge related
    for related in source.related:
        if related not in target.related:
            target.related.append(related)


def build_related_links(entries: List[DocEntry]) -> None:
    """Build cross-references between related entries."""
    entry_by_id: Dict[str, DocEntry] = {e.id: e for e in entries}
    entry_by_name: Dict[str, List[DocEntry]] = defaultdict(list)

    for entry in entries:
        entry_by_name[entry.name.upper()].append(entry)

    for entry in entries:
        related_ids: Set[str] = set(entry.related)

        # Link suffixes to their parent structure
        if entry.parent_structure:
            if entry.parent_structure in entry_by_id:
                related_ids.add(entry.parent_structure)

        # Link structures to their suffixes
        if entry.type == DocEntryType.STRUCTURE:
            for other in entries:
                if other.parent_structure == entry.id and other.id != entry.id:
                    # Don't add all suffixes, just a few key ones
                    if len([r for r in related_ids if r.startswith(entry.id + ":")]) < 5:
                        related_ids.add(other.id)

        # Link complementary pairs
        complementary_pairs = [
            ("PROGRADE", "RETROGRADE"),
            ("NORMAL", "ANTINORMAL"),
            ("RADIAL", "ANTIRADIAL"),
            ("LOCK", "UNLOCK"),
            ("APOAPSIS", "PERIAPSIS"),
            ("ETA:APOAPSIS", "ETA:PERIAPSIS"),
        ]

        for pair in complementary_pairs:
            if entry.id == pair[0] and pair[1] in entry_by_id:
                related_ids.add(pair[1])
            elif entry.id == pair[1] and pair[0] in entry_by_id:
                related_ids.add(pair[0])

        # Check description for references to other entries
        if entry.description:
            desc_upper = entry.description.upper()
            for other_id, other_entry in entry_by_id.items():
                if other_id == entry.id:
                    continue
                if other_entry.name.upper() in desc_upper:
                    # Verify it's a word boundary match
                    pattern = r"\b" + re.escape(other_entry.name.upper()) + r"\b"
                    if re.search(pattern, desc_upper):
                        related_ids.add(other_id)

        # Update entry's related list (limit to avoid overwhelming)
        entry.related = list(related_ids)[:10]


def infer_additional_tags(entries: List[DocEntry]) -> None:
    """Add additional tags based on entry relationships and content."""
    for entry in entries:
        tags_to_add = set()

        # Tag all suffixes with their parent's tags
        if entry.parent_structure:
            parent = next((e for e in entries if e.id == entry.parent_structure), None)
            if parent:
                for tag in parent.tags:
                    if tag not in ["core"]:  # Don't propagate some tags
                        tags_to_add.add(tag)

        # Add tags based on return type
        if entry.return_type:
            type_lower = entry.return_type.lower()
            if "vector" in type_lower:
                tags_to_add.add("vector")
            if "direction" in type_lower:
                tags_to_add.add("direction")
            if "scalar" in type_lower or "number" in type_lower:
                tags_to_add.add("numeric")
            if "list" in type_lower:
                tags_to_add.add("collection")
            if "boolean" in type_lower or "bool" in type_lower:
                tags_to_add.add("boolean")

        # Add inferred tags
        for tag in tags_to_add:
            if tag not in entry.tags:
                entry.tags.append(tag)


def build_tag_index(entries: List[DocEntry]) -> Dict[str, str]:
    """Build a tag description index from all tags used."""
    # Collect all tags
    all_tags: Set[str] = set()
    for entry in entries:
        all_tags.update(entry.tags)

    # Standard tag descriptions
    tag_descriptions = {
        "vessel": "Vessel properties and control",
        "orbit": "Orbital mechanics",
        "body": "Celestial bodies",
        "celestial": "Celestial bodies",
        "navigation": "Steering and waypoints",
        "control": "Throttle, staging, RCS",
        "resource": "Fuel, electricity",
        "crew": "Kerbals and crew",
        "science": "Experiments and data",
        "part": "Part modules",
        "math": "Mathematical functions",
        "vector": "Vector operations",
        "direction": "Direction and heading",
        "language": "kOS language constructs",
        "io": "Files and communication",
        "file": "File operations",
        "terminal": "Terminal output",
        "time": "Time and scheduling",
        "core": "Fundamental structures",
        "flight": "Flight operations",
        "staging": "Stage management",
        "collection": "Lists, lexicons, etc.",
        "trigger": "Event triggers",
        "binding": "Lock bindings",
        "variables": "Variable management",
        "function": "Built-in functions",
        "command": "Commands",
        "keyword": "Language keywords",
        "constant": "Constants",
        "systems": "Ship systems",
        "action": "Action groups",
        "basic": "Basic math operations",
        "trigonometry": "Trigonometric functions",
        "angle": "Angle calculations",
        "random": "Random number generation",
        "program": "Program execution",
        "numeric": "Numeric values",
        "boolean": "Boolean values",
        "communication": "Communication",
        "gui": "GUI elements",
        "misc": "Miscellaneous",
    }

    # Build index with only tags that are actually used
    return {tag: tag_descriptions.get(tag, tag.title()) for tag in sorted(all_tags)}


def validate_entries(entries: List[DocEntry]) -> List[str]:
    """Validate entries and return list of warnings."""
    warnings = []

    for entry in entries:
        # Check required fields
        if not entry.id:
            warnings.append(f"Entry missing ID: {entry}")
        if not entry.name:
            warnings.append(f"Entry missing name: {entry.id}")

        # Check for overly long fields
        if entry.description and len(entry.description) > 1000:
            warnings.append(f"Entry {entry.id} has very long description ({len(entry.description)} chars)")

        # Check for missing descriptions
        if not entry.description:
            warnings.append(f"Entry {entry.id} has no description")

        # Check suffix entries have parent
        if entry.type == DocEntryType.SUFFIX and not entry.parent_structure:
            warnings.append(f"Suffix {entry.id} has no parent structure")

        # Check methods have signatures
        if entry.access and entry.access.value == "method" and not entry.signature:
            warnings.append(f"Method {entry.id} has no signature")

    return warnings


def assign_domain_tags(entries: List[DocEntry]) -> None:
    """Apply comprehensive domain taxonomy tags to all entries.

    Uses pattern matching, keyword hints, and return type analysis
    to ensure every entry has at least 2 relevant tags.
    """
    for entry in entries:
        entry.tags = assign_tags_to_entry(entry)


def assign_categories(entries: List[DocEntry]) -> None:
    """Assign category labels to all entries based on type and structure."""
    uncategorized = 0

    for entry in entries:
        category = get_category_for_entry(entry)
        if category:
            entry.category = category
        else:
            # Fallback category based on type
            if entry.type == DocEntryType.STRUCTURE:
                entry.category = "Structures"
            elif entry.type == DocEntryType.SUFFIX:
                entry.category = "Structure Members"
            else:
                entry.category = "Miscellaneous"
                uncategorized += 1

    if uncategorized > 0:
        print(f"    {uncategorized} entries assigned to Miscellaneous category")


def assign_usage_frequency(entries: List[DocEntry]) -> None:
    """Assign usage frequency hints to all entries."""
    counts = {"common": 0, "moderate": 0, "rare": 0}

    for entry in entries:
        frequency = get_usage_frequency(entry)
        entry.usage_frequency = frequency
        counts[frequency] += 1

    print(f"    Frequency distribution: {counts['common']} common, {counts['moderate']} moderate, {counts['rare']} rare")


def validate_cross_references(entries: List[DocEntry]) -> Tuple[List[str], List[str]]:
    """Validate that all cross-references point to existing entries.

    Returns:
        Tuple of (errors, warnings)
    """
    errors = []
    warnings = []

    # Build ID set
    entry_ids = {e.id for e in entries}

    for entry in entries:
        # Check related references
        invalid_related = []
        for related_id in entry.related:
            if related_id not in entry_ids:
                invalid_related.append(related_id)

        if invalid_related:
            # Silently remove invalid references instead of warning
            entry.related = [r for r in entry.related if r in entry_ids]

        # Check parent structure reference
        if entry.parent_structure and entry.parent_structure not in entry_ids:
            warnings.append(f"Entry {entry.id} references non-existent parent: {entry.parent_structure}")

    return errors, warnings


def validate_tag_coverage(entries: List[DocEntry]) -> List[str]:
    """Validate that all entries have sufficient tags.

    Returns:
        List of entries with insufficient tags
    """
    insufficient = []

    for entry in entries:
        if len(entry.tags) < 2:
            insufficient.append(f"Entry {entry.id} has only {len(entry.tags)} tags: {entry.tags}")

    return insufficient


def validate_metadata_completeness(entries: List[DocEntry]) -> Dict[str, int]:
    """Check metadata completeness across all entries.

    Returns:
        Dictionary with counts of missing fields
    """
    missing = {
        "category": 0,
        "usage_frequency": 0,
        "description": 0,
        "tags_insufficient": 0,
    }

    for entry in entries:
        if not entry.category:
            missing["category"] += 1
        if not entry.usage_frequency:
            missing["usage_frequency"] += 1
        if not entry.description:
            missing["description"] += 1
        if len(entry.tags) < 2:
            missing["tags_insufficient"] += 1

    return missing


def build_enhanced_tag_index(entries: List[DocEntry]) -> Dict[str, str]:
    """Build a comprehensive tag description index from taxonomy."""
    # Get all tag descriptions from taxonomy
    tag_descriptions = get_all_tag_descriptions()

    # Collect all tags actually used
    used_tags: Set[str] = set()
    for entry in entries:
        used_tags.update(entry.tags)

    # Return only used tags with their descriptions
    return {
        tag: tag_descriptions.get(tag, tag.title())
        for tag in sorted(used_tags)
    }
