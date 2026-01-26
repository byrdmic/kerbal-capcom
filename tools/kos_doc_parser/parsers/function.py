"""Parser for kOS function documentation pages."""

import re
from typing import List, Optional

from bs4 import BeautifulSoup, Tag

from .base import BaseParser
from ..models import DocEntry, DocEntryType, DocAccessMode
from ..extractors import (
    clean_text,
    extract_section_content,
    extract_deprecation,
    extract_code_block,
)


class FunctionParser(BaseParser):
    """Parser for function documentation pages (math, etc.)."""

    def parse_page(self, url: str, html: str) -> List[DocEntry]:
        """Parse a function documentation page."""
        entries = []
        soup = self.make_soup(html)

        # Functions are typically documented in sections with headers like:
        # "ABS(value)" or "FUNCTION ABS()" or heading followed by definition

        # Method 1: Find headings with function signatures
        for heading in soup.find_all(["h2", "h3", "h4"]):
            heading_text = clean_text(heading.get_text())
            heading_text = re.sub(r"[¶#]", "", heading_text)

            # Look for function pattern: FUNCNAME(args) or FUNCNAME
            match = re.match(r"^([A-Z_][A-Z0-9_]*)\s*(\([^)]*\))?", heading_text)
            if not match:
                continue

            func_name = match.group(1)
            args = match.group(2) or ""

            entry = self._create_function_entry(heading, url, func_name, args)
            if entry:
                entries.append(entry)

        # Method 2: Look for definition list (dl/dt/dd) style documentation
        for dt in soup.find_all("dt"):
            dt_text = clean_text(dt.get_text())
            dt_text = re.sub(r"[¶#]", "", dt_text)

            # Look for function pattern
            match = re.match(r"^([A-Z_][A-Z0-9_]*)\s*\(([^)]*)\)", dt_text)
            if not match:
                continue

            func_name = match.group(1)
            args = f"({match.group(2)})"

            entry = self._create_function_entry_from_dt(dt, url, func_name, args)
            if entry:
                # Check if we already have this function
                if not any(e.id == entry.id for e in entries):
                    entries.append(entry)

        return entries

    def _create_function_entry(
        self, heading: Tag, url: str, func_name: str, args: str
    ) -> Optional[DocEntry]:
        """Create a DocEntry from a heading-based function definition."""
        # Extract description and code from following content
        description, snippet = extract_section_content(heading)

        # Check for deprecation
        deprecated, deprecation_note = extract_deprecation(heading)

        # Build source reference with anchor
        heading_id = heading.get("id")
        source_ref = url
        if heading_id:
            source_ref = f"{url}#{heading_id}"
        else:
            # Try to find anchor link
            anchor = heading.find("a", class_="headerlink")
            if anchor and anchor.get("href"):
                source_ref = url + anchor["href"]

        # Parse return type from description or following content
        return_type = self._infer_return_type(description)

        # Build signature
        if args:
            signature = f"{func_name}{args}"
        else:
            signature = f"{func_name}()"

        return DocEntry(
            id=f"FUNCTION:{func_name}",
            name=func_name,
            type=DocEntryType.FUNCTION,
            return_type=return_type,
            access=DocAccessMode.METHOD,
            signature=signature,
            description=description,
            snippet=snippet[:500] if snippet else None,
            source_ref=source_ref,
            tags=self._infer_function_tags(func_name, description, url),
            deprecated=deprecated,
            deprecation_note=deprecation_note,
        )

    def _create_function_entry_from_dt(
        self, dt: Tag, url: str, func_name: str, args: str
    ) -> Optional[DocEntry]:
        """Create a DocEntry from a definition term style function definition."""
        # Get description from dd sibling
        dd = dt.find_next_sibling("dd")
        description = clean_text(dd.get_text()) if dd else ""

        # Look for code example in dd
        snippet = None
        if dd:
            pre = dd.find("pre")
            if pre:
                snippet = extract_code_block(pre)

        # Check for deprecation
        deprecated, deprecation_note = extract_deprecation(dt)

        # Build source reference with anchor
        dt_id = dt.get("id")
        source_ref = url
        if dt_id:
            source_ref = f"{url}#{dt_id}"

        # Parse return type
        return_type = self._infer_return_type(description)

        # Build signature
        signature = f"{func_name}{args}"

        return DocEntry(
            id=f"FUNCTION:{func_name}",
            name=func_name,
            type=DocEntryType.FUNCTION,
            return_type=return_type,
            access=DocAccessMode.METHOD,
            signature=signature,
            description=description[:500] if description else None,
            snippet=snippet[:500] if snippet else None,
            source_ref=source_ref,
            tags=self._infer_function_tags(func_name, description, url),
            deprecated=deprecated,
            deprecation_note=deprecation_note,
        )

    def _infer_return_type(self, description: str) -> Optional[str]:
        """Infer return type from description text."""
        if not description:
            return None

        desc_lower = description.lower()

        # Common return type patterns
        type_patterns = [
            (r"returns?\s+(?:a\s+)?scalar", "Scalar"),
            (r"returns?\s+(?:a\s+)?vector", "Vector"),
            (r"returns?\s+(?:a\s+)?direction", "Direction"),
            (r"returns?\s+(?:a\s+)?string", "String"),
            (r"returns?\s+(?:a\s+)?boolean", "Boolean"),
            (r"returns?\s+(?:a\s+)?list", "List"),
            (r"returns?\s+(?:a\s+)?true|false", "Boolean"),
            (r"returns?\s+(?:a\s+)?number", "Scalar"),
            (r"returns?\s+(?:the\s+)?angle", "Scalar"),
        ]

        for pattern, return_type in type_patterns:
            if re.search(pattern, desc_lower):
                return return_type

        return "Scalar"  # Default for math functions

    def _infer_function_tags(
        self, name: str, description: str, url: str
    ) -> List[str]:
        """Infer tags from function name, description, and URL."""
        tags = ["function"]
        name_lower = name.lower()
        desc_lower = (description or "").lower()

        # URL-based categorization
        if "/math/" in url:
            tags.append("math")
        if "/basic" in url:
            tags.append("basic")
        if "/trig" in url or "/trigonometric" in url:
            tags.append("trigonometry")
        if "/vector" in url:
            tags.append("vector")
        if "/direction" in url:
            tags.append("direction")

        # Name-based categorization
        trig_funcs = ["sin", "cos", "tan", "asin", "acos", "atan", "atan2"]
        if any(name_lower == func for func in trig_funcs):
            tags.append("trigonometry")

        basic_funcs = ["abs", "round", "floor", "ceiling", "sqrt", "mod", "min", "max"]
        if any(name_lower == func for func in basic_funcs):
            tags.append("basic")

        # Description-based inference
        if "angle" in desc_lower or "degree" in desc_lower or "radian" in desc_lower:
            tags.append("angle")
        if "vector" in desc_lower:
            tags.append("vector")
        if "random" in desc_lower:
            tags.append("random")

        return list(set(tags))
