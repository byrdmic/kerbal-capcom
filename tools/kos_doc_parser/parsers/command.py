"""Parser for kOS command documentation pages."""

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


class CommandParser(BaseParser):
    """Parser for command documentation pages."""

    # Known kOS commands to look for
    KNOWN_COMMANDS = [
        "PRINT", "CLEARSCREEN", "STAGE", "REBOOT", "SHUTDOWN", "LOG",
        "COPY", "DELETE", "RENAME", "SWITCH", "CD", "LIST", "EDIT",
        "TOGGLE", "ACTIVATE", "DEACTIVATE", "AG1", "AG2", "AG3", "AG4",
        "AG5", "AG6", "AG7", "AG8", "AG9", "AG10", "LIGHTS", "BRAKES",
        "GEAR", "RCS", "SAS", "ABORT", "LEGS", "CHUTES", "PANELS", "LADDERS",
        "BAYS", "INTAKES", "DEPLOYDRILLS", "DRILLS", "FUELCELLS", "ISRU",
        "RADIATORS", "DEPLOY", "UNDEPLOY",
    ]

    def parse_page(self, url: str, html: str) -> List[DocEntry]:
        """Parse a command documentation page."""
        entries = []
        soup = self.make_soup(html)

        # Method 1: Find sections for known commands
        for heading in soup.find_all(["h2", "h3", "h4"]):
            heading_text = clean_text(heading.get_text())
            heading_text = re.sub(r"[¶#]", "", heading_text).upper()

            # Check if this heading matches a known command
            command = self._match_command(heading_text)
            if command:
                entry = self._create_command_entry(heading, url, command)
                if entry and not any(e.id == entry.id for e in entries):
                    entries.append(entry)

        # Method 2: Look for definition list style
        for dt in soup.find_all("dt"):
            dt_text = clean_text(dt.get_text())
            dt_text = re.sub(r"[¶#]", "", dt_text).upper()

            command = self._match_command(dt_text)
            if command:
                if any(e.id == command for e in entries):
                    continue

                entry = self._create_command_entry_from_dt(dt, url, command)
                if entry:
                    entries.append(entry)

        return entries

    def _match_command(self, text: str) -> Optional[str]:
        """Check if text matches a known command."""
        # Clean up text
        text = text.strip().split()[0] if text else ""

        if text in self.KNOWN_COMMANDS:
            return text

        # Check for action group commands (AG1-AG10)
        if re.match(r"AG\d+", text):
            return text

        return None

    def _create_command_entry(
        self, heading: Tag, url: str, command: str
    ) -> Optional[DocEntry]:
        """Create a DocEntry for a command."""
        # Extract description and code from following content
        description, snippet = extract_section_content(heading)

        # Check for deprecation
        deprecated, deprecation_note = extract_deprecation(heading)

        # Build source reference with anchor
        anchor_id = extract_heading_anchor(heading)
        source_ref = url
        if anchor_id:
            source_ref = f"{url}#{anchor_id}"

        # Extract signature from heading
        signature = self._extract_signature(heading, command)

        return DocEntry(
            id=command,
            name=command,
            type=DocEntryType.COMMAND,
            signature=signature,
            description=description[:500] if description else None,
            snippet=snippet[:500] if snippet else None,
            source_ref=source_ref,
            tags=self._infer_command_tags(command, url),
            deprecated=deprecated,
            deprecation_note=deprecation_note,
        )

    def _create_command_entry_from_dt(
        self, dt: Tag, url: str, command: str
    ) -> Optional[DocEntry]:
        """Create a DocEntry from a definition term style command."""
        # Get description from dd sibling
        dd = dt.find_next_sibling("dd")
        description = clean_text(dd.get_text()) if dd else ""

        # Look for code example in dd
        snippet = None
        if dd:
            pre = dd.find("pre")
            if pre:
                snippet = pre.get_text().strip()

        # Check for deprecation
        deprecated, deprecation_note = extract_deprecation(dt)

        # Build source reference
        dt_id = dt.get("id")
        source_ref = url
        if dt_id:
            source_ref = f"{url}#{dt_id}"

        # Extract signature from dt text
        dt_text = clean_text(dt.get_text())
        signature = dt_text if len(dt_text) > len(command) else f"{command}."

        return DocEntry(
            id=command,
            name=command,
            type=DocEntryType.COMMAND,
            signature=signature,
            description=description[:500] if description else None,
            snippet=snippet[:500] if snippet else None,
            source_ref=source_ref,
            tags=self._infer_command_tags(command, url),
            deprecated=deprecated,
            deprecation_note=deprecation_note,
        )

    def _extract_signature(self, heading: Tag, command: str) -> Optional[str]:
        """Extract signature pattern from heading."""
        heading_text = clean_text(heading.get_text())
        heading_text = re.sub(r"[¶#]", "", heading_text)

        # If heading has more than command name, use it as signature
        if len(heading_text) > len(command):
            return heading_text

        # Default signatures for common commands
        default_signatures = {
            "PRINT": "PRINT expression. | PRINT expression AT (col, row).",
            "CLEARSCREEN": "CLEARSCREEN.",
            "STAGE": "STAGE.",
            "REBOOT": "REBOOT.",
            "SHUTDOWN": "SHUTDOWN.",
            "LOG": "LOG expression TO filename.",
            "TOGGLE": "TOGGLE identifier.",
            "LIGHTS": "LIGHTS ON. | LIGHTS OFF.",
            "GEAR": "GEAR ON. | GEAR OFF.",
            "BRAKES": "BRAKES ON. | BRAKES OFF.",
            "RCS": "RCS ON. | RCS OFF.",
            "SAS": "SAS ON. | SAS OFF.",
            "ABORT": "ABORT ON. | ABORT OFF.",
        }

        return default_signatures.get(command, f"{command}.")

    def _infer_command_tags(self, command: str, url: str) -> List[str]:
        """Infer tags from command and URL."""
        tags = ["command"]

        # URL-based categorization
        if "/terminal" in url or "/io" in url:
            tags.append("io")
            tags.append("terminal")
        if "/flight" in url:
            tags.append("flight")
            tags.append("control")
        if "/systems" in url:
            tags.append("systems")
        if "/file" in url:
            tags.append("io")
            tags.append("file")

        # Command-based categorization
        io_commands = ["PRINT", "CLEARSCREEN", "LOG"]
        if command in io_commands:
            tags.append("io")
            tags.append("terminal")

        staging_commands = ["STAGE"]
        if command in staging_commands:
            tags.append("staging")
            tags.append("flight")

        toggle_commands = ["LIGHTS", "GEAR", "BRAKES", "RCS", "SAS", "ABORT",
                          "LEGS", "CHUTES", "PANELS", "LADDERS", "BAYS", "INTAKES",
                          "DEPLOYDRILLS", "DRILLS", "FUELCELLS", "ISRU", "RADIATORS"]
        if command in toggle_commands:
            tags.append("systems")
            tags.append("control")

        # Action groups
        if command.startswith("AG"):
            tags.append("action")
            tags.append("control")

        return list(set(tags))
