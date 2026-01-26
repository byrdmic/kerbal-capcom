"""Data models matching the C# DocEntry schema."""

from dataclasses import dataclass, field
from typing import List, Optional
from enum import Enum


class DocEntryType(Enum):
    """Entry type for kOS documentation."""
    STRUCTURE = "structure"
    SUFFIX = "suffix"
    FUNCTION = "function"
    KEYWORD = "keyword"
    CONSTANT = "constant"
    COMMAND = "command"


class DocAccessMode(Enum):
    """Access mode for suffixes and methods."""
    NONE = None
    GET = "get"
    SET = "set"
    GET_SET = "get/set"
    METHOD = "method"


@dataclass
class DocEntry:
    """A single entry in the kOS documentation index."""

    # Required fields
    id: str
    name: str
    type: DocEntryType

    # Optional fields
    parent_structure: Optional[str] = None
    return_type: Optional[str] = None
    access: DocAccessMode = DocAccessMode.NONE
    signature: Optional[str] = None
    description: Optional[str] = None
    snippet: Optional[str] = None
    source_ref: Optional[str] = None
    tags: List[str] = field(default_factory=list)
    aliases: List[str] = field(default_factory=list)
    related: List[str] = field(default_factory=list)
    deprecated: bool = False
    deprecation_note: Optional[str] = None

    def to_dict(self) -> dict:
        """Convert to JSON-serializable dictionary matching C# property names."""
        result = {
            "id": self.id,
            "name": self.name,
            "type": self.type.value,
        }

        # Only include optional fields if they have values
        if self.parent_structure:
            result["parentStructure"] = self.parent_structure
        else:
            result["parentStructure"] = None

        if self.return_type:
            result["returnType"] = self.return_type
        else:
            result["returnType"] = None

        if self.access != DocAccessMode.NONE:
            result["access"] = self.access.value
        else:
            result["access"] = None

        if self.signature:
            result["signature"] = self.signature
        else:
            result["signature"] = None

        if self.description:
            result["description"] = self.description
        else:
            result["description"] = None

        if self.snippet:
            result["snippet"] = self.snippet
        else:
            result["snippet"] = None

        if self.source_ref:
            result["sourceRef"] = self.source_ref
        else:
            result["sourceRef"] = None

        if self.tags:
            result["tags"] = self.tags

        if self.aliases:
            result["aliases"] = self.aliases

        if self.related:
            result["related"] = self.related

        if self.deprecated:
            result["deprecated"] = self.deprecated
            if self.deprecation_note:
                result["deprecationNote"] = self.deprecation_note

        return result


@dataclass
class DocIndex:
    """The complete documentation index."""

    schema_version: str
    content_version: str
    kos_min_version: str
    generated_at: str
    source_url: str
    entries: List[DocEntry] = field(default_factory=list)
    tags: dict = field(default_factory=dict)

    def to_dict(self) -> dict:
        """Convert to JSON-serializable dictionary."""
        return {
            "schemaVersion": self.schema_version,
            "contentVersion": self.content_version,
            "kosMinVersion": self.kos_min_version,
            "generatedAt": self.generated_at,
            "sourceUrl": self.source_url,
            "entries": [e.to_dict() for e in self.entries],
            "tags": self.tags,
        }
