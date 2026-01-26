"""Configuration constants for the kOS documentation parser."""

# Base URL for kOS documentation
BASE_URL = "https://ksp-kos.github.io/KOS/"

# Table of contents page
TOC_URL = BASE_URL + "contents.html"

# URL patterns for different doc categories
URL_PATTERNS = {
    "structures": "structures/",
    "math": "math/",
    "language": "language/",
    "commands": "commands/",
    "bindings": "bindings/",
    "addons": "addons/",
}

# Rate limiting
REQUEST_DELAY = 0.5  # seconds between requests

# Cache settings
CACHE_DIR = ".cache"
CACHE_EXPIRY_HOURS = 24

# Output schema version (must match C# SUPPORTED_SCHEMA_MAJOR)
SCHEMA_VERSION = "1.0.0"

# kOS version we're documenting
KOS_VERSION = "1.4.0.0"

# User agent for requests
USER_AGENT = "KSPCapcom-DocParser/1.0"
