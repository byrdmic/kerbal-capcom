"""Parser for kOS constant documentation pages."""

import re
from typing import List, Optional

from bs4 import BeautifulSoup, Tag

from .base import BaseParser
from ..models import DocEntry, DocEntryType, DocAccessMode
from ..extractors import (
    clean_text,
    extract_section_content,
    extract_deprecation,
    extract_heading_anchor,
)


class ConstantParser(BaseParser):
    """Parser for constant documentation pages (directions, etc.)."""

    # Known kOS constants
    KNOWN_CONSTANTS = {
        # Direction constants
        "PROGRADE": ("Direction", "A direction pointing along the vessel's orbital velocity."),
        "RETROGRADE": ("Direction", "A direction pointing opposite to orbital velocity."),
        "NORMAL": ("Direction", "A direction pointing perpendicular to the orbit plane."),
        "ANTINORMAL": ("Direction", "A direction pointing opposite to normal."),
        "RADIAL": ("Direction", "A direction pointing away from the body being orbited."),
        "ANTIRADIAL": ("Direction", "A direction pointing toward the body being orbited."),
        "TARGET": ("Structure", "The current target vessel or body."),
        "SRFPROGRADE": ("Direction", "Surface prograde direction."),
        "SRFRETROGRADE": ("Direction", "Surface retrograde direction."),
        # Body constants
        "KERBIN": ("Body", "The planet Kerbin."),
        "MUN": ("Body", "The Mun, Kerbin's first moon."),
        "MINMUS": ("Body", "Minmus, Kerbin's second moon."),
        "DUNA": ("Body", "The planet Duna."),
        "EVE": ("Body", "The planet Eve."),
        "JOOL": ("Body", "The gas giant Jool."),
        "SUN": ("Body", "The star Kerbol (the Sun)."),
        # Ship reference
        "SHIP": ("Vessel", "Reference to the vessel running the script."),
        "HASTARGET": ("Boolean", "True if a target is set."),
    }

    def parse_page(self, url: str, html: str) -> List[DocEntry]:
        """Parse a page that may contain constant definitions."""
        entries = []
        soup = self.make_soup(html)

        # Method 1: Look for headings matching known constants
        for heading in soup.find_all(["h2", "h3", "h4"]):
            heading_text = clean_text(heading.get_text())
            heading_text = re.sub(r"[¶#]", "", heading_text).upper()

            # Direct match
            if heading_text in self.KNOWN_CONSTANTS:
                entry = self._create_constant_entry(heading, url, heading_text)
                if entry:
                    entries.append(entry)
                continue

            # Word match (e.g., "PROGRADE" in "Using PROGRADE")
            for const_name in self.KNOWN_CONSTANTS:
                if const_name in heading_text:
                    if any(e.id == const_name for e in entries):
                        continue
                    entry = self._create_constant_entry(heading, url, const_name)
                    if entry:
                        entries.append(entry)
                    break

        # Method 2: Search for known constants in any section
        body_text = soup.get_text().upper()
        for const_name, (return_type, default_desc) in self.KNOWN_CONSTANTS.items():
            if const_name in body_text:
                # Don't duplicate
                if any(e.id == const_name for e in entries):
                    continue

                # Try to find the section for this constant
                entry = self._find_and_create_constant_entry(soup, url, const_name)
                if entry:
                    entries.append(entry)

        return entries

    def _create_constant_entry(
        self, heading: Tag, url: str, const_name: str
    ) -> Optional[DocEntry]:
        """Create a DocEntry for a constant from a heading."""
        # Get known type and default description
        return_type, default_desc = self.KNOWN_CONSTANTS.get(const_name, (None, None))

        # Extract description and code from following content
        description, snippet = extract_section_content(heading)
        if not description:
            description = default_desc

        # Check for deprecation
        deprecated, deprecation_note = extract_deprecation(heading)

        # Build source reference with anchor
        anchor_id = extract_heading_anchor(heading)
        source_ref = url
        if anchor_id:
            source_ref = f"{url}#{anchor_id}"

        return DocEntry(
            id=const_name,
            name=const_name,
            type=DocEntryType.CONSTANT,
            return_type=return_type,
            access=DocAccessMode.GET,
            description=description[:500] if description else None,
            snippet=snippet[:500] if snippet else None,
            source_ref=source_ref,
            tags=self._infer_constant_tags(const_name, return_type),
            deprecated=deprecated,
            deprecation_note=deprecation_note,
        )

    def _find_and_create_constant_entry(
        self, soup: BeautifulSoup, url: str, const_name: str
    ) -> Optional[DocEntry]:
        """Find a constant in the page and create an entry for it."""
        # Get known type and default description
        return_type, default_desc = self.KNOWN_CONSTANTS.get(const_name, (None, None))

        # Try to find any heading or section containing this constant
        for element in soup.find_all(["h2", "h3", "h4", "dt", "p"]):
            text = element.get_text().upper()
            if const_name in text:
                # Found a relevant element
                if element.name in ["h2", "h3", "h4"]:
                    return self._create_constant_entry(element, url, const_name)
                else:
                    # Use default description
                    break

        # Create with defaults
        return DocEntry(
            id=const_name,
            name=const_name,
            type=DocEntryType.CONSTANT,
            return_type=return_type,
            access=DocAccessMode.GET,
            description=default_desc,
            source_ref=url,
            tags=self._infer_constant_tags(const_name, return_type),
        )

    def _infer_constant_tags(self, const_name: str, return_type: str) -> List[str]:
        """Infer tags from constant name and type."""
        tags = ["constant"]

        # Direction constants
        direction_constants = ["PROGRADE", "RETROGRADE", "NORMAL", "ANTINORMAL",
                               "RADIAL", "ANTIRADIAL", "SRFPROGRADE", "SRFRETROGRADE"]
        if const_name in direction_constants:
            tags.extend(["direction", "navigation", "orbit"])

        # Body constants
        body_constants = ["KERBIN", "MUN", "MINMUS", "DUNA", "EVE", "JOOL", "SUN"]
        if const_name in body_constants:
            tags.extend(["body", "celestial"])

        # Ship/vessel constants
        if const_name in ["SHIP", "TARGET"]:
            tags.extend(["vessel"])

        if return_type:
            if return_type == "Direction":
                if "direction" not in tags:
                    tags.append("direction")
            elif return_type == "Body":
                if "body" not in tags:
                    tags.append("body")
            elif return_type == "Vessel":
                if "vessel" not in tags:
                    tags.append("vessel")

        return list(set(tags))
