"""HTTP fetcher with caching and rate limiting."""

import hashlib
import os
import time
import json
from pathlib import Path
from typing import Optional
from datetime import datetime, timedelta

import requests

from .config import (
    CACHE_DIR,
    CACHE_EXPIRY_HOURS,
    REQUEST_DELAY,
    USER_AGENT,
)


class Fetcher:
    """HTTP fetcher with caching and rate limiting."""

    def __init__(self, cache_dir: Optional[str] = None, bypass_cache: bool = False):
        self.cache_dir = Path(cache_dir or CACHE_DIR)
        self.cache_dir.mkdir(parents=True, exist_ok=True)
        self.bypass_cache = bypass_cache
        self.last_request_time = 0
        self.session = requests.Session()
        self.session.headers.update({"User-Agent": USER_AGENT})
        self.request_count = 0
        self.cache_hit_count = 0

    def _get_cache_path(self, url: str) -> Path:
        """Get the cache file path for a URL."""
        url_hash = hashlib.md5(url.encode()).hexdigest()
        return self.cache_dir / f"{url_hash}.html"

    def _get_cache_meta_path(self, url: str) -> Path:
        """Get the cache metadata file path for a URL."""
        url_hash = hashlib.md5(url.encode()).hexdigest()
        return self.cache_dir / f"{url_hash}.meta.json"

    def _is_cache_valid(self, url: str) -> bool:
        """Check if cached content is still valid."""
        if self.bypass_cache:
            return False

        cache_path = self._get_cache_path(url)
        meta_path = self._get_cache_meta_path(url)

        if not cache_path.exists() or not meta_path.exists():
            return False

        try:
            with open(meta_path, "r") as f:
                meta = json.load(f)
            cached_at = datetime.fromisoformat(meta["cached_at"])
            expiry = timedelta(hours=CACHE_EXPIRY_HOURS)
            return datetime.now() - cached_at < expiry
        except (json.JSONDecodeError, KeyError, ValueError):
            return False

    def _read_cache(self, url: str) -> Optional[str]:
        """Read content from cache."""
        cache_path = self._get_cache_path(url)
        try:
            with open(cache_path, "r", encoding="utf-8") as f:
                return f.read()
        except (IOError, UnicodeDecodeError):
            return None

    def _write_cache(self, url: str, content: str):
        """Write content to cache."""
        cache_path = self._get_cache_path(url)
        meta_path = self._get_cache_meta_path(url)

        with open(cache_path, "w", encoding="utf-8") as f:
            f.write(content)

        with open(meta_path, "w") as f:
            json.dump({"url": url, "cached_at": datetime.now().isoformat()}, f)

    def _rate_limit(self):
        """Ensure we don't exceed the rate limit."""
        elapsed = time.time() - self.last_request_time
        if elapsed < REQUEST_DELAY:
            time.sleep(REQUEST_DELAY - elapsed)
        self.last_request_time = time.time()

    def fetch(self, url: str) -> Optional[str]:
        """Fetch a URL, using cache if available."""
        # Check cache first
        if self._is_cache_valid(url):
            content = self._read_cache(url)
            if content:
                self.cache_hit_count += 1
                return content

        # Rate limit and fetch
        self._rate_limit()
        self.request_count += 1

        try:
            response = self.session.get(url, timeout=30)
            response.raise_for_status()
            content = response.text

            # Cache the result
            self._write_cache(url, content)

            return content
        except requests.RequestException as e:
            print(f"Error fetching {url}: {e}")
            return None

    def get_stats(self) -> dict:
        """Get fetcher statistics."""
        return {
            "requests": self.request_count,
            "cache_hits": self.cache_hit_count,
        }
