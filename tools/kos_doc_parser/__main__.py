"""CLI entry point for the kOS documentation parser."""

import argparse
import sys
from pathlib import Path

from .fetcher import Fetcher
from .discovery import PageDiscovery
from .parsers import (
    StructureParser,
    FunctionParser,
    KeywordParser,
    CommandParser,
    ConstantParser,
)
from .postprocess import (
    add_essential_entries,
    deduplicate_entries,
    build_related_links,
    infer_additional_tags,
    build_tag_index,
    validate_entries,
)
from .output import create_index, write_json, generate_summary
from .config import BASE_URL


def main():
    parser = argparse.ArgumentParser(
        description="Parse kOS documentation and generate JSON index"
    )
    parser.add_argument(
        "--output", "-o",
        default="../../deploy/GameData/KSPCapcom/Plugins/kos_docs.json",
        help="Output file path (default: ../../deploy/GameData/KSPCapcom/Plugins/kos_docs.json)"
    )
    parser.add_argument(
        "--cache-dir",
        default=".cache",
        help="Cache directory for HTTP responses (default: .cache)"
    )
    parser.add_argument(
        "--no-cache",
        action="store_true",
        help="Bypass cache and fetch fresh content"
    )
    parser.add_argument(
        "--verbose", "-v",
        action="store_true",
        help="Enable verbose output"
    )
    parser.add_argument(
        "--validate-only",
        action="store_true",
        help="Only validate existing JSON file, don't fetch"
    )
    parser.add_argument(
        "--pretty",
        action="store_true",
        default=True,
        help="Pretty-print JSON output (default: true)"
    )

    args = parser.parse_args()

    # Resolve output path relative to script location
    script_dir = Path(__file__).parent
    output_path = Path(args.output)
    if not output_path.is_absolute():
        output_path = script_dir / output_path

    print(f"kOS Documentation Parser")
    print(f"Output: {output_path}")
    print()

    # Initialize fetcher
    fetcher = Fetcher(
        cache_dir=str(script_dir / args.cache_dir),
        bypass_cache=args.no_cache
    )

    # Discover pages
    discovery = PageDiscovery(fetcher)
    pages = discovery.discover()

    total_pages = sum(len(urls) for urls in pages.values())
    print(f"\nDiscovered {total_pages} documentation pages")
    print()

    # Initialize parsers
    structure_parser = StructureParser(fetcher)
    function_parser = FunctionParser(fetcher)
    keyword_parser = KeywordParser(fetcher)
    command_parser = CommandParser(fetcher)
    constant_parser = ConstantParser(fetcher)

    all_entries = []
    all_errors = []

    # Parse structure pages
    print("Parsing structures...")
    struct_urls = discovery.get_structure_pages()
    struct_entries = structure_parser.parse_pages(struct_urls)
    all_entries.extend(struct_entries)
    all_errors.extend(structure_parser.errors)
    print(f"  Found {len(struct_entries)} entries from {len(struct_urls)} pages")

    # Parse math/function pages
    print("Parsing functions...")
    math_urls = discovery.get_math_pages()
    func_entries = function_parser.parse_pages(math_urls)
    all_entries.extend(func_entries)
    all_errors.extend(function_parser.errors)
    print(f"  Found {len(func_entries)} entries from {len(math_urls)} pages")

    # Parse language pages (keywords)
    print("Parsing keywords...")
    lang_urls = discovery.get_language_pages()
    keyword_entries = keyword_parser.parse_pages(lang_urls)
    all_entries.extend(keyword_entries)
    all_errors.extend(keyword_parser.errors)
    print(f"  Found {len(keyword_entries)} entries from {len(lang_urls)} pages")

    # Parse command pages
    print("Parsing commands...")
    cmd_urls = discovery.get_command_pages()
    command_entries = command_parser.parse_pages(cmd_urls)
    all_entries.extend(command_entries)
    all_errors.extend(command_parser.errors)
    print(f"  Found {len(command_entries)} entries from {len(cmd_urls)} pages")

    # Parse constants from math/direction pages
    print("Parsing constants...")
    const_urls = [u for u in math_urls if "direction" in u.lower()]
    const_entries = constant_parser.parse_pages(const_urls)
    all_entries.extend(const_entries)
    all_errors.extend(constant_parser.errors)
    print(f"  Found {len(const_entries)} entries from {len(const_urls)} pages")

    # Parse bindings for more constants/keywords
    print("Parsing bindings...")
    binding_urls = discovery.get_binding_pages()
    binding_keyword_entries = keyword_parser.parse_pages(binding_urls)
    binding_const_entries = constant_parser.parse_pages(binding_urls)
    all_entries.extend(binding_keyword_entries)
    all_entries.extend(binding_const_entries)
    print(f"  Found {len(binding_keyword_entries) + len(binding_const_entries)} entries from {len(binding_urls)} pages")

    print()

    # Post-processing
    print("Post-processing...")

    print("  Adding essential fallback entries...")
    all_entries = add_essential_entries(all_entries, BASE_URL)

    print("  Deduplicating entries...")
    all_entries = deduplicate_entries(all_entries)

    print("  Building related links...")
    build_related_links(all_entries)

    print("  Inferring additional tags...")
    infer_additional_tags(all_entries)

    print("  Building tag index...")
    tags = build_tag_index(all_entries)

    print("  Validating entries...")
    warnings = validate_entries(all_entries)

    print()

    # Create and write index
    index = create_index(all_entries, tags)
    write_json(index, str(output_path), pretty=args.pretty)

    # Print summary
    print()
    print(generate_summary(index))

    # Print errors and warnings
    if all_errors:
        print()
        print(f"Errors ({len(all_errors)}):")
        for error in all_errors[:20]:  # Limit output
            print(f"  - {error}")
        if len(all_errors) > 20:
            print(f"  ... and {len(all_errors) - 20} more errors")

    if warnings and args.verbose:
        print()
        print(f"Warnings ({len(warnings)}):")
        for warning in warnings[:20]:
            print(f"  - {warning}")
        if len(warnings) > 20:
            print(f"  ... and {len(warnings) - 20} more warnings")

    # Print fetcher stats
    stats = fetcher.get_stats()
    print()
    print(f"Fetcher stats: {stats['requests']} requests, {stats['cache_hits']} cache hits")

    # Exit with error if no entries
    if len(all_entries) == 0:
        print("\nERROR: No entries extracted!")
        return 1

    print()
    print("Done!")
    return 0


if __name__ == "__main__":
    sys.exit(main())
