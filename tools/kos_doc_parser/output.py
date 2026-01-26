"""JSON output generation for kOS documentation."""

import json
from datetime import datetime
from pathlib import Path
from typing import List

from .models import DocEntry, DocIndex
from .config import SCHEMA_VERSION, KOS_VERSION, BASE_URL


def create_index(entries: List[DocEntry], tags: dict) -> DocIndex:
    """Create a DocIndex from entries and tags."""
    return DocIndex(
        schema_version=SCHEMA_VERSION,
        content_version=KOS_VERSION,
        kos_min_version=KOS_VERSION,
        generated_at=datetime.utcnow().strftime("%Y-%m-%dT%H:%M:%SZ"),
        source_url=BASE_URL,
        entries=entries,
        tags=tags,
    )


def write_json(index: DocIndex, output_path: str, pretty: bool = True) -> None:
    """Write the documentation index to a JSON file."""
    output_path = Path(output_path)
    output_path.parent.mkdir(parents=True, exist_ok=True)

    data = index.to_dict()

    with open(output_path, "w", encoding="utf-8") as f:
        if pretty:
            json.dump(data, f, indent=2, ensure_ascii=False)
        else:
            json.dump(data, f, ensure_ascii=False)

    print(f"Wrote {len(index.entries)} entries to {output_path}")


def generate_summary(index: DocIndex) -> str:
    """Generate a human-readable summary of the index."""
    lines = [
        "kOS Documentation Index Summary",
        "=" * 40,
        f"Schema Version: {index.schema_version}",
        f"Content Version: {index.content_version}",
        f"Generated At: {index.generated_at}",
        f"Total Entries: {len(index.entries)}",
        "",
        "Entries by Type:",
    ]

    # Count by type
    type_counts = {}
    for entry in index.entries:
        type_name = entry.type.value
        type_counts[type_name] = type_counts.get(type_name, 0) + 1

    for type_name, count in sorted(type_counts.items()):
        lines.append(f"  {type_name}: {count}")

    # Count by tag
    lines.append("")
    lines.append("Most Common Tags:")

    tag_counts = {}
    for entry in index.entries:
        for tag in entry.tags:
            tag_counts[tag] = tag_counts.get(tag, 0) + 1

    sorted_tags = sorted(tag_counts.items(), key=lambda x: -x[1])[:15]
    for tag, count in sorted_tags:
        lines.append(f"  {tag}: {count}")

    # Structure coverage
    lines.append("")
    lines.append("Structure Coverage:")

    structures = [e for e in index.entries if e.type.value == "structure"]
    for struct in sorted(structures, key=lambda x: x.id)[:10]:
        suffix_count = sum(1 for e in index.entries
                          if e.type.value == "suffix" and e.parent_structure == struct.id)
        lines.append(f"  {struct.id}: {suffix_count} suffixes")

    if len(structures) > 10:
        lines.append(f"  ... and {len(structures) - 10} more structures")

    return "\n".join(lines)
