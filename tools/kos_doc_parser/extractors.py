"""HTML extraction utilities for kOS documentation."""

import re
from typing import Dict, List, Optional, Tuple
from bs4 import BeautifulSoup, Tag


def clean_text(text: str) -> str:
    """Clean extracted text by normalizing whitespace."""
    if not text:
        return ""
    # Replace multiple whitespace with single space
    text = re.sub(r"\s+", " ", text)
    return text.strip()


def extract_code_block(element: Tag) -> Optional[str]:
    """Extract code from a pre/code block."""
    if not element:
        return None

    # Handle pre > code structure
    code = element.find("code") if element.name == "pre" else element
    if code:
        return code.get_text().strip()

    return element.get_text().strip()


def find_following_code_block(element: Tag) -> Optional[str]:
    """Find the next code block after an element."""
    sibling = element.find_next_sibling()
    while sibling:
        if sibling.name == "pre":
            return extract_code_block(sibling)
        if sibling.name == "div" and "highlight" in sibling.get("class", []):
            pre = sibling.find("pre")
            if pre:
                return extract_code_block(pre)
        # Stop at headings or major section breaks
        if sibling.name in ["h1", "h2", "h3", "h4", "h5", "h6", "hr"]:
            break
        sibling = sibling.find_next_sibling()
    return None


def extract_suffix_table(table: Tag) -> List[Dict]:
    """Extract suffix information from a documentation table.

    kOS docs use tables with columns: Suffix, Type, Description
    or: Suffix, Type, Get/Set, Description
    """
    rows = []
    tbody = table.find("tbody") or table

    for tr in tbody.find_all("tr"):
        cells = tr.find_all(["td", "th"])
        if len(cells) < 2:
            continue

        # Skip header rows
        if all(c.name == "th" for c in cells):
            continue

        row_data = {}

        if len(cells) >= 3:
            # Standard format: Suffix, Type, Description
            suffix_cell = cells[0]
            type_cell = cells[1]

            # Check for Get/Set column
            if len(cells) >= 4:
                access_cell = cells[2]
                desc_cell = cells[3]
                row_data["access_text"] = clean_text(access_cell.get_text())
            else:
                desc_cell = cells[2]
                row_data["access_text"] = ""

            # Extract suffix name and detect if it's a method
            suffix_text = clean_text(suffix_cell.get_text())
            suffix_link = suffix_cell.find("a")

            row_data["name"] = suffix_text
            row_data["is_method"] = "(" in suffix_text
            row_data["type"] = clean_text(type_cell.get_text())
            row_data["description"] = clean_text(desc_cell.get_text())

            if suffix_link and suffix_link.get("href"):
                row_data["anchor"] = suffix_link["href"]

            rows.append(row_data)

    return rows


def parse_access_mode(text: str) -> Tuple[str, bool]:
    """Parse access mode text into (access_mode, is_method) tuple.

    Returns: (access_mode, is_method)
    - access_mode: "get", "set", "get/set", "method", or None
    - is_method: True if text indicates a method
    """
    if not text:
        return (None, False)

    text_lower = text.lower().strip()

    # Check for method indicators
    if "method" in text_lower:
        return ("method", True)

    # Check for get/set combinations
    has_get = "get" in text_lower
    has_set = "set" in text_lower

    if has_get and has_set:
        return ("get/set", False)
    elif has_get:
        return ("get", False)
    elif has_set:
        return ("set", False)

    return (None, False)


def extract_deprecation(element: Tag) -> Tuple[bool, Optional[str]]:
    """Check if an element indicates deprecation.

    Returns: (is_deprecated, deprecation_note)
    """
    text = element.get_text() if element else ""

    # Look for deprecation markers
    if "(Deprecated)" in text:
        return (True, None)

    # Look for "Deprecated since version X"
    match = re.search(r"Deprecated since version ([^:]+)(?::\s*(.+))?", text, re.IGNORECASE)
    if match:
        version = match.group(1).strip()
        note = match.group(2).strip() if match.group(2) else None
        return (True, note or f"Deprecated since version {version}")

    # Look for deprecated class/warning boxes
    if element and element.name == "div":
        classes = element.get("class", [])
        if any("deprecated" in c.lower() for c in classes):
            return (True, clean_text(text))

    return (False, None)


def extract_page_title(soup: BeautifulSoup) -> Optional[str]:
    """Extract the main page title."""
    # Try h1 first
    h1 = soup.find("h1")
    if h1:
        # Remove pilcrow/permalink markers
        text = h1.get_text()
        text = re.sub(r"[¶#]", "", text)
        return clean_text(text)

    # Fall back to title tag
    title = soup.find("title")
    if title:
        text = title.get_text()
        # Remove site suffix
        text = text.split("—")[0].split("-")[0]
        return clean_text(text)

    return None


def extract_section_content(heading: Tag) -> Tuple[str, Optional[str]]:
    """Extract description text and code example from a section.

    Returns: (description, code_snippet)
    """
    description_parts = []
    code_snippet = None

    sibling = heading.find_next_sibling()
    while sibling:
        # Stop at next heading
        if sibling.name in ["h1", "h2", "h3", "h4", "h5", "h6"]:
            break

        # Collect paragraph text
        if sibling.name == "p":
            description_parts.append(clean_text(sibling.get_text()))

        # Capture first code block
        if sibling.name == "pre" and not code_snippet:
            code_snippet = extract_code_block(sibling)
        elif sibling.name == "div" and "highlight" in sibling.get("class", []):
            if not code_snippet:
                pre = sibling.find("pre")
                if pre:
                    code_snippet = extract_code_block(pre)

        sibling = sibling.find_next_sibling()

    description = " ".join(description_parts)
    return (description, code_snippet)


def extract_heading_anchor(heading: Tag) -> Optional[str]:
    """Extract the anchor ID from a heading element."""
    # Check for id attribute
    if heading.get("id"):
        return heading["id"]

    # Check for anchor link inside heading
    anchor = heading.find("a", class_="headerlink")
    if anchor and anchor.get("href"):
        href = anchor["href"]
        if href.startswith("#"):
            return href[1:]

    return None
