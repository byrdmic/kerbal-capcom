"""Parser for kOS language keyword documentation pages."""

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


class KeywordParser(BaseParser):
    """Parser for language keyword documentation pages."""

    # Known kOS keywords to look for
    KNOWN_KEYWORDS = [
        "LOCK", "UNLOCK", "SET", "DECLARE", "LOCAL", "GLOBAL", "PARAMETER",
        "IF", "ELSE", "UNTIL", "FOR", "FROM", "WHEN", "ON", "PRESERVE",
        "RETURN", "BREAK", "FUNCTION", "CHOOSE", "SWITCH", "TO",
        "LAZYGLOBAL", "RUNPATH", "RUN", "RUNONCEPATH", "COMPILE",
        "WAIT", "AT", "AND", "OR", "NOT", "TRUE", "FALSE",
    ]

    def parse_page(self, url: str, html: str) -> List[DocEntry]:
        """Parse a language documentation page."""
        entries = []
        soup = self.make_soup(html)

        # Method 1: Find sections for known keywords
        for heading in soup.find_all(["h2", "h3", "h4"]):
            heading_text = clean_text(heading.get_text())
            heading_text = re.sub(r"[¶#]", "", heading_text).upper()

            # Check if this heading matches a known keyword
            keyword = self._match_keyword(heading_text)
            if keyword:
                entry = self._create_keyword_entry(heading, url, keyword)
                if entry and not any(e.id == entry.id for e in entries):
                    entries.append(entry)

        # Method 2: Look for headings with keyword-like patterns
        for heading in soup.find_all(["h2", "h3", "h4"]):
            heading_text = clean_text(heading.get_text())
            heading_text = re.sub(r"[¶#]", "", heading_text)

            # Pattern: keyword followed by syntax
            # e.g., "LOCK statement", "LOCK identifier TO expression"
            for keyword in self.KNOWN_KEYWORDS:
                if heading_text.upper().startswith(keyword):
                    # Already have this one?
                    if any(e.id == keyword for e in entries):
                        continue

                    entry = self._create_keyword_entry(heading, url, keyword)
                    if entry:
                        entries.append(entry)
                    break

        return entries

    def _match_keyword(self, text: str) -> Optional[str]:
        """Check if text matches a known keyword."""
        # Exact match
        if text in self.KNOWN_KEYWORDS:
            return text

        # Prefix match
        for keyword in self.KNOWN_KEYWORDS:
            if text == keyword or text.startswith(keyword + " "):
                return keyword

        return None

    def _create_keyword_entry(
        self, heading: Tag, url: str, keyword: str
    ) -> Optional[DocEntry]:
        """Create a DocEntry for a keyword."""
        # Extract description and code from following content
        description, snippet = extract_section_content(heading)

        # Check for deprecation
        deprecated, deprecation_note = extract_deprecation(heading)

        # Build source reference with anchor
        anchor_id = extract_heading_anchor(heading)
        source_ref = url
        if anchor_id:
            source_ref = f"{url}#{anchor_id}"

        # Extract signature from heading or first line
        signature = self._extract_signature(heading, keyword)

        # Infer return type for certain keywords
        return_type = self._infer_return_type(keyword)

        return DocEntry(
            id=keyword,
            name=keyword,
            type=DocEntryType.KEYWORD,
            return_type=return_type,
            signature=signature,
            description=description[:500] if description else None,
            snippet=snippet[:500] if snippet else None,
            source_ref=source_ref,
            tags=self._infer_keyword_tags(keyword, description),
            deprecated=deprecated,
            deprecation_note=deprecation_note,
        )

    def _extract_signature(self, heading: Tag, keyword: str) -> Optional[str]:
        """Extract signature pattern from heading or following content."""
        # Check heading text itself
        heading_text = clean_text(heading.get_text())
        heading_text = re.sub(r"[¶#]", "", heading_text)

        # If heading contains more than just keyword, it might be a signature
        if len(heading_text) > len(keyword) + 1:
            return heading_text

        # Look for signature pattern in next paragraph
        next_p = heading.find_next_sibling("p")
        if next_p:
            p_text = clean_text(next_p.get_text())
            # Look for syntax patterns like "LOCK identifier TO expression."
            if p_text.upper().startswith(keyword):
                # Take first sentence as signature
                first_sentence = p_text.split(".")[0] + "."
                if len(first_sentence) < 100:
                    return first_sentence

        # Default signatures for common keywords
        default_signatures = {
            "LOCK": "LOCK identifier TO expression.",
            "UNLOCK": "UNLOCK identifier.",
            "SET": "SET identifier TO expression.",
            "IF": "IF condition { statements }",
            "UNTIL": "UNTIL condition { statements }",
            "FOR": "FOR identifier IN collection { statements }",
            "WHEN": "WHEN condition THEN { statements }",
            "ON": "ON trigger { statements }",
            "FUNCTION": "FUNCTION name { statements }",
            "WAIT": "WAIT seconds. | WAIT UNTIL condition.",
            "RETURN": "RETURN expression.",
            "BREAK": "BREAK.",
            "PRESERVE": "PRESERVE.",
            "LOCAL": "LOCAL identifier IS expression.",
            "GLOBAL": "GLOBAL identifier IS expression.",
            "PARAMETER": "PARAMETER identifier.",
            "DECLARE": "DECLARE identifier.",
        }

        return default_signatures.get(keyword)

    def _infer_return_type(self, keyword: str) -> Optional[str]:
        """Infer return type for keywords that produce values."""
        value_keywords = {
            "TRUE": "Boolean",
            "FALSE": "Boolean",
        }
        return value_keywords.get(keyword)

    def _infer_keyword_tags(self, keyword: str, description: str) -> List[str]:
        """Infer tags from keyword and description."""
        tags = ["language"]
        desc_lower = (description or "").lower()

        # Control flow keywords
        control_flow = ["IF", "ELSE", "UNTIL", "FOR", "FROM", "WHEN", "ON", "WAIT", "BREAK", "RETURN", "CHOOSE", "SWITCH"]
        if keyword in control_flow:
            tags.append("control")

        # Variable keywords
        variable_keywords = ["SET", "LOCK", "UNLOCK", "DECLARE", "LOCAL", "GLOBAL", "PARAMETER"]
        if keyword in variable_keywords:
            tags.append("variables")

        # Binding keywords
        if keyword in ["LOCK", "UNLOCK"]:
            tags.append("binding")

        # Function keywords
        if keyword in ["FUNCTION", "RETURN", "PARAMETER"]:
            tags.append("function")

        # Trigger keywords
        if keyword in ["WHEN", "ON", "PRESERVE"]:
            tags.append("trigger")

        # File/program keywords
        if keyword in ["RUN", "RUNPATH", "RUNONCEPATH", "COMPILE"]:
            tags.append("program")
            tags.append("io")

        return list(set(tags))
