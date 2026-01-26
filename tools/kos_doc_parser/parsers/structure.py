"""Parser for kOS structure documentation pages."""

import re
from typing import List, Optional, Tuple
from urllib.parse import urljoin

from bs4 import BeautifulSoup, Tag

from .base import BaseParser
from ..models import DocEntry, DocEntryType, DocAccessMode
from ..extractors import (
    clean_text,
    extract_page_title,
    extract_suffix_table,
    extract_section_content,
    extract_deprecation,
    extract_heading_anchor,
    parse_access_mode,
)


class StructureParser(BaseParser):
    """Parser for structure documentation pages."""

    def parse_page(self, url: str, html: str) -> List[DocEntry]:
        """Parse a structure documentation page."""
        entries = []
        soup = self.make_soup(html)

        # Extract structure name from title
        structure_name = self._extract_structure_name(soup, url)
        if not structure_name:
            return entries

        # Create entry for the structure itself
        structure_entry = self._create_structure_entry(soup, url, structure_name)
        if structure_entry:
            entries.append(structure_entry)

        # Find and parse suffix tables
        suffix_entries = self._parse_suffix_tables(soup, url, structure_name)
        entries.extend(suffix_entries)

        # Find and parse detailed suffix sections (for code examples)
        self._enrich_with_details(soup, url, entries, structure_name)

        return entries

    def _extract_structure_name(self, soup: BeautifulSoup, url: str) -> Optional[str]:
        """Extract the structure name from the page."""
        title = extract_page_title(soup)
        if not title:
            return None

        # Common patterns: "Vessel", "VESSEL", "Vessel Structure"
        # Strip " Structure" suffix
        name = title.replace(" Structure", "").replace(" structure", "")
        name = name.strip()

        # Normalize to uppercase for structure names
        return name.upper() if name else None

    def _create_structure_entry(
        self, soup: BeautifulSoup, url: str, structure_name: str
    ) -> Optional[DocEntry]:
        """Create a DocEntry for the structure itself."""
        # Get description from first paragraph(s) after h1
        h1 = soup.find("h1")
        description = ""
        snippet = None

        if h1:
            desc_parts = []
            sibling = h1.find_next_sibling()
            while sibling and sibling.name == "p":
                desc_parts.append(clean_text(sibling.get_text()))
                sibling = sibling.find_next_sibling()

            description = " ".join(desc_parts[:2])  # First 2 paragraphs

            # Look for code example
            code_div = soup.find("div", class_="highlight")
            if code_div:
                pre = code_div.find("pre")
                if pre:
                    snippet = pre.get_text().strip()[:500]  # Limit snippet length

        # Detect deprecation
        deprecated, deprecation_note = self._detect_page_deprecation(soup)

        # Extract tags from URL path
        tags = self._extract_tags_from_url(url)

        return DocEntry(
            id=structure_name,
            name=structure_name.title(),  # "Vessel" instead of "VESSEL"
            type=DocEntryType.STRUCTURE,
            description=description or f"The {structure_name} structure.",
            snippet=snippet,
            source_ref=url,
            tags=tags,
            deprecated=deprecated,
            deprecation_note=deprecation_note,
        )

    def _parse_suffix_tables(
        self, soup: BeautifulSoup, url: str, structure_name: str
    ) -> List[DocEntry]:
        """Parse suffix tables to create DocEntry objects."""
        entries = []

        # Find all tables that look like suffix tables
        for table in soup.find_all("table"):
            # Check if this looks like a suffix table
            header_row = table.find("tr")
            if not header_row:
                continue

            headers = [clean_text(th.get_text().lower()) for th in header_row.find_all(["th", "td"])]

            # Skip tables that don't look like suffix tables
            if not any("suffix" in h or "member" in h or "name" in h for h in headers):
                continue

            rows = extract_suffix_table(table)
            for row in rows:
                entry = self._create_suffix_entry(row, url, structure_name)
                if entry:
                    entries.append(entry)

        return entries

    def _create_suffix_entry(
        self, row: dict, url: str, structure_name: str
    ) -> Optional[DocEntry]:
        """Create a DocEntry from a suffix table row."""
        name = row.get("name", "")
        if not name:
            return None

        # Clean up method notation
        clean_name = name.replace("()", "").split("(")[0].strip()
        is_method = row.get("is_method", False) or "(" in name

        # Parse access mode
        access_text = row.get("access_text", "") or row.get("type", "")
        access_mode, detected_method = parse_access_mode(access_text)

        if detected_method:
            is_method = True

        if is_method:
            access = DocAccessMode.METHOD
        elif access_mode == "get/set":
            access = DocAccessMode.GET_SET
        elif access_mode == "get":
            access = DocAccessMode.GET
        elif access_mode == "set":
            access = DocAccessMode.SET
        else:
            # Default to GET for properties
            access = DocAccessMode.GET

        # Build ID
        entry_id = f"{structure_name}:{clean_name.upper()}"

        # Build source reference with anchor
        source_ref = url
        if row.get("anchor"):
            anchor = row["anchor"]
            if not anchor.startswith("#"):
                anchor = "#" + anchor
            source_ref = url + anchor

        # Parse return type
        return_type = row.get("type", "")
        # Clean up type text (remove "get", "set" if mixed in)
        return_type = re.sub(r"\b(get|set)\b", "", return_type, flags=re.IGNORECASE).strip()
        return_type = return_type or None

        # Create signature for methods
        signature = None
        if is_method:
            if "(" in name:
                signature = f"{clean_name.upper()}{name[name.find('('):]}"
            else:
                signature = f"{clean_name.upper()}()"

        return DocEntry(
            id=entry_id,
            name=clean_name.upper(),
            type=DocEntryType.SUFFIX,
            parent_structure=structure_name,
            return_type=return_type,
            access=access,
            signature=signature,
            description=row.get("description", ""),
            source_ref=source_ref,
            tags=self._infer_suffix_tags(clean_name, row.get("description", "")),
        )

    def _enrich_with_details(
        self,
        soup: BeautifulSoup,
        url: str,
        entries: List[DocEntry],
        structure_name: str,
    ):
        """Enrich entries with details from individual sections."""
        # Build a map of entries by name for quick lookup
        entry_map = {e.name: e for e in entries if e.type == DocEntryType.SUFFIX}

        # Find headings that match suffix patterns
        # e.g., "VESSEL:ALTITUDE", ":ALTITUDE", "ALTITUDE"
        for heading in soup.find_all(["h2", "h3", "h4"]):
            heading_text = clean_text(heading.get_text())
            heading_text = re.sub(r"[¶#]", "", heading_text)  # Remove pilcrow

            # Try to match to a known entry
            suffix_name = None
            if ":" in heading_text:
                # "VESSEL:ALTITUDE" format
                parts = heading_text.split(":")
                suffix_name = parts[-1].upper()
            else:
                suffix_name = heading_text.upper()

            if suffix_name not in entry_map:
                continue

            entry = entry_map[suffix_name]

            # Extract description and code from the section
            description, code_snippet = extract_section_content(heading)

            # Update entry if we found better content
            if description and len(description) > len(entry.description or ""):
                entry.description = description

            if code_snippet and not entry.snippet:
                entry.snippet = code_snippet[:500]  # Limit length

            # Check for deprecation
            section_deprecated, note = extract_deprecation(heading)
            if section_deprecated:
                entry.deprecated = True
                if note:
                    entry.deprecation_note = note

    def _detect_page_deprecation(self, soup: BeautifulSoup) -> Tuple[bool, Optional[str]]:
        """Detect if the entire page/structure is deprecated."""
        # Look for deprecated warning boxes
        for div in soup.find_all("div", class_=["deprecated", "warning"]):
            text = div.get_text().lower()
            if "deprecated" in text:
                return (True, clean_text(div.get_text()))

        return (False, None)

    def _extract_tags_from_url(self, url: str) -> List[str]:
        """Extract category tags based on URL path."""
        tags = []

        if "/vessels/" in url:
            tags.extend(["vessel"])
        if "/celestial_bodies/" in url:
            tags.extend(["body", "celestial"])
        if "/orbits/" in url:
            tags.extend(["orbit"])
        if "/collections/" in url:
            tags.extend(["collection"])
        if "/communication/" in url:
            tags.extend(["communication"])
        if "/misc/" in url:
            tags.extend(["misc"])
        if "/gui/" in url:
            tags.extend(["gui"])
        if "/volumes_and_files/" in url:
            tags.extend(["io", "file"])

        tags.append("core")  # All structures are core
        return list(set(tags))

    def _infer_suffix_tags(self, name: str, description: str) -> List[str]:
        """Infer tags from suffix name and description."""
        tags = []
        name_lower = name.lower()
        desc_lower = (description or "").lower()

        # Keyword-based tag inference
        tag_keywords = {
            "orbit": ["orbit", "apoapsis", "periapsis", "eccentricity", "inclination"],
            "position": ["position", "altitude", "latitude", "longitude", "geoposition"],
            "velocity": ["velocity", "speed", "groundspeed", "airspeed"],
            "control": ["throttle", "steering", "control", "rcs", "sas"],
            "resource": ["fuel", "resource", "electric", "oxidizer", "monopropellant"],
            "part": ["part", "engine", "sensor", "module"],
            "crew": ["crew", "kerbal"],
            "science": ["science", "experiment", "data"],
            "time": ["time", "eta", "period", "epoch"],
        }

        for tag, keywords in tag_keywords.items():
            if any(kw in name_lower or kw in desc_lower for kw in keywords):
                tags.append(tag)

        return list(set(tags))
