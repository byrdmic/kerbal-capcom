"""Base parser class for kOS documentation."""

from abc import ABC, abstractmethod
from typing import List, Optional

from bs4 import BeautifulSoup

from ..models import DocEntry
from ..fetcher import Fetcher


class BaseParser(ABC):
    """Base class for documentation parsers."""

    def __init__(self, fetcher: Fetcher):
        self.fetcher = fetcher
        self.errors: List[str] = []
        self.entries_count = 0

    @abstractmethod
    def parse_page(self, url: str, html: str) -> List[DocEntry]:
        """Parse a single documentation page.

        Args:
            url: The page URL
            html: The page HTML content

        Returns:
            List of DocEntry objects extracted from the page
        """
        pass

    def parse_pages(self, urls: List[str]) -> List[DocEntry]:
        """Parse multiple documentation pages.

        Args:
            urls: List of page URLs to parse

        Returns:
            List of all DocEntry objects extracted
        """
        all_entries = []

        for url in urls:
            html = self.fetcher.fetch(url)
            if not html:
                self.errors.append(f"Failed to fetch: {url}")
                continue

            try:
                entries = self.parse_page(url, html)
                all_entries.extend(entries)
                self.entries_count += len(entries)
            except Exception as e:
                self.errors.append(f"Error parsing {url}: {e}")

        return all_entries

    def make_soup(self, html: str) -> BeautifulSoup:
        """Create a BeautifulSoup object from HTML."""
        return BeautifulSoup(html, "lxml")

    def log_error(self, message: str):
        """Log an error message."""
        self.errors.append(message)
        print(f"  ERROR: {message}")
