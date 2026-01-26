"""Page discovery from kOS documentation TOC."""

import re
from typing import Dict, List, Set
from urllib.parse import urljoin

from bs4 import BeautifulSoup

from .config import BASE_URL, TOC_URL
from .fetcher import Fetcher


class PageDiscovery:
    """Discovers documentation pages from the kOS TOC."""

    # URL path patterns for categorization
    CATEGORY_PATTERNS = {
        "structures": [
            r"structures/vessels/",
            r"structures/celestial_bodies/",
            r"structures/collections/",
            r"structures/misc/",
            r"structures/orbits/",
            r"structures/communication/",
            r"structures/volumes_and_files/",
            r"structures/gui/",
        ],
        "math": [
            r"math/",
        ],
        "language": [
            r"language/",
        ],
        "commands": [
            r"commands/",
        ],
        "bindings": [
            r"bindings/",
        ],
    }

    def __init__(self, fetcher: Fetcher):
        self.fetcher = fetcher
        self.discovered_pages: Dict[str, List[str]] = {
            "structures": [],
            "math": [],
            "language": [],
            "commands": [],
            "bindings": [],
            "other": [],
        }

    def discover(self) -> Dict[str, List[str]]:
        """Discover all documentation pages from the TOC."""
        print("Discovering documentation pages...")

        # Fetch TOC
        toc_html = self.fetcher.fetch(TOC_URL)
        if not toc_html:
            print("Failed to fetch TOC")
            return self.discovered_pages

        soup = BeautifulSoup(toc_html, "lxml")

        # Find all internal links
        seen_urls: Set[str] = set()

        for link in soup.find_all("a", href=True):
            href = link["href"]

            # Skip external links, anchors, and non-html links
            if href.startswith(("http://", "https://", "mailto:", "#")):
                continue
            if not href.endswith(".html") and ".html#" not in href:
                continue

            # Normalize URL
            full_url = urljoin(TOC_URL, href)

            # Remove anchor part for deduplication
            base_url = full_url.split("#")[0]

            if base_url in seen_urls:
                continue
            seen_urls.add(base_url)

            # Skip non-documentation pages
            if any(x in base_url for x in ["/search.html", "/genindex.html", "_sources/"]):
                continue

            # Categorize by URL pattern
            category = self._categorize_url(base_url)
            self.discovered_pages[category].append(base_url)

        # Print summary
        for category, urls in self.discovered_pages.items():
            print(f"  {category}: {len(urls)} pages")

        return self.discovered_pages

    def _categorize_url(self, url: str) -> str:
        """Categorize a URL by its path pattern."""
        for category, patterns in self.CATEGORY_PATTERNS.items():
            for pattern in patterns:
                if re.search(pattern, url):
                    return category
        return "other"

    def get_structure_pages(self) -> List[str]:
        """Get all structure documentation pages."""
        return self.discovered_pages.get("structures", [])

    def get_math_pages(self) -> List[str]:
        """Get all math documentation pages."""
        return self.discovered_pages.get("math", [])

    def get_language_pages(self) -> List[str]:
        """Get all language documentation pages."""
        return self.discovered_pages.get("language", [])

    def get_command_pages(self) -> List[str]:
        """Get all command documentation pages."""
        return self.discovered_pages.get("commands", [])

    def get_binding_pages(self) -> List[str]:
        """Get all binding documentation pages."""
        return self.discovered_pages.get("bindings", [])
