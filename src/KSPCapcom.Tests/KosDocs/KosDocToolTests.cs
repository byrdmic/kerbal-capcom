using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using KSPCapcom.KosDocs;

namespace KSPCapcom.Tests.KosDocs
{
    [TestFixture]
    public class KosDocToolTests
    {
        private KosDocIndex _index;
        private TestableKosDocTool _tool;

        [SetUp]
        public void SetUp()
        {
            _index = new KosDocIndex();
            PopulateTestIndex(_index);
            _tool = new TestableKosDocTool(_index);
        }

        #region Execute Tests

        [Test]
        public void Execute_ShipVelocity_ReturnsMatchingEntries()
        {
            // Act - search for VELOCITY which should find VESSEL:VELOCITY
            var result = _tool.Execute("VELOCITY");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries, Is.Not.Null);
            Assert.That(result.Entries.Count, Is.GreaterThan(0));
            Assert.That(result.Entries[0].Id, Does.Contain("VELOCITY").IgnoreCase);
        }

        [Test]
        public void Execute_NaturalLanguageQuery_ReturnsSensibleMatches()
        {
            // Act
            var result = _tool.Execute("altitude");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries, Is.Not.Null);
            Assert.That(result.Entries.Count, Is.GreaterThan(0));
            // Should find altitude-related entries
            Assert.That(result.Entries[0].Id, Does.Contain("ALTITUDE").IgnoreCase);
        }

        [Test]
        public void Execute_EmptyQuery_ReturnsError()
        {
            // Act
            var result = _tool.Execute("");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Is.Not.Null.And.Not.Empty);
            Assert.That(result.Entries, Is.Null);
        }

        [Test]
        public void Execute_NullQuery_ReturnsError()
        {
            // Act
            var result = _tool.Execute(null);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.Contain("empty").IgnoreCase);
        }

        [Test]
        public void Execute_ShortQuery_ReturnsError()
        {
            // Act (query less than MinQueryLength)
            var result = _tool.Execute("A");

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.Contain("at least"));
        }

        [Test]
        public void Execute_NoMatches_ReturnsSuccessWithEmptyList()
        {
            // Act
            var result = _tool.Execute("xyznonexistentquery");

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries, Is.Not.Null);
            Assert.That(result.Entries.Count, Is.EqualTo(0));
        }

        [Test]
        public void Execute_MaxResultsLimitsOutput()
        {
            // Act
            var result = _tool.Execute("VESSEL", 2);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.LessThanOrEqualTo(2));
        }

        [Test]
        public void Execute_MaxResultsClampedToLimit()
        {
            // Act - request more than MaxResultsLimit
            var result = _tool.Execute("VESSEL", 100);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.LessThanOrEqualTo(KosDocTool.MaxResultsLimit));
        }

        [Test]
        public void Execute_NegativeMaxResults_ClampedToOne()
        {
            // Act
            var result = _tool.Execute("VESSEL", -5);

            // Assert
            Assert.That(result.Success, Is.True);
            // Should still return results (clamped to 1)
        }

        #endregion

        #region ExecuteFromJson Tests

        [Test]
        public void ExecuteFromJson_ValidJson_ExecutesSuccessfully()
        {
            // Arrange
            var json = "{\"query\": \"ALTITUDE\"}";

            // Act
            var result = _tool.ExecuteFromJson(json);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.GreaterThan(0));
        }

        [Test]
        public void ExecuteFromJson_WithMaxResults_RespectsLimit()
        {
            // Arrange
            var json = "{\"query\": \"VESSEL\", \"max_results\": 2}";

            // Act
            var result = _tool.ExecuteFromJson(json);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.LessThanOrEqualTo(2));
        }

        [Test]
        public void ExecuteFromJson_MissingQuery_ReturnsError()
        {
            // Arrange
            var json = "{\"max_results\": 5}";

            // Act
            var result = _tool.ExecuteFromJson(json);

            // Assert
            Assert.That(result.Success, Is.False);
            Assert.That(result.Error, Does.Contain("query").IgnoreCase);
        }

        [Test]
        public void ExecuteFromJson_EmptyJson_ReturnsError()
        {
            // Act
            var result = _tool.ExecuteFromJson("");

            // Assert
            Assert.That(result.Success, Is.False);
        }

        [Test]
        public void ExecuteFromJson_NullJson_ReturnsError()
        {
            // Act
            var result = _tool.ExecuteFromJson(null);

            // Assert
            Assert.That(result.Success, Is.False);
        }

        #endregion

        #region Result Formatting Tests

        [Test]
        public void Result_ToJson_ContainsExpectedFields()
        {
            // Act
            var result = _tool.Execute("ALTITUDE");
            var json = result.ToJson();

            // Assert
            Assert.That(json, Does.Contain("\"success\":true"));
            Assert.That(json, Does.Contain("\"entries\":["));
            Assert.That(json, Does.Contain("\"id\""));
            Assert.That(json, Does.Contain("\"kind\""));
        }

        [Test]
        public void Result_ErrorToJson_ContainsErrorField()
        {
            // Act
            var result = _tool.Execute("");
            var json = result.ToJson();

            // Assert
            Assert.That(json, Does.Contain("\"success\":false"));
            Assert.That(json, Does.Contain("\"error\""));
        }

        [Test]
        public void Result_ToJson_ContainsSourceRef()
        {
            // Act
            var result = _tool.Execute("VESSEL", 1);
            var json = result.ToJson();

            // Assert - verify sourceRef is in the JSON output
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Count, Is.GreaterThan(0));
            Assert.That(json, Does.Contain("\"sourceRef\":"));
            Assert.That(json, Does.Contain("https://ksp-kos.github.io/KOS/"));
        }

        [Test]
        public void ResultEntry_FromDocEntry_MapsSourceRef()
        {
            // Arrange
            var entry = new DocEntry
            {
                Id = "TEST",
                Name = "TEST",
                Type = DocEntryType.Structure,
                Description = "Test entry",
                SourceRef = "https://ksp-kos.github.io/KOS/test.html#section"
            };

            // Act
            var resultEntry = KosDocResultEntry.FromDocEntry(entry);

            // Assert
            Assert.That(resultEntry.SourceRef, Is.EqualTo(entry.SourceRef));
        }

        [Test]
        public void ResultEntry_NullSourceRef_OmittedFromJson()
        {
            // Arrange - create entry without SourceRef
            var entry = new DocEntry
            {
                Id = "TEST_NO_SOURCE",
                Name = "TEST_NO_SOURCE",
                Type = DocEntryType.Function,
                Description = "Entry with no source reference",
                SourceRef = null
            };

            // Act
            var resultEntry = KosDocResultEntry.FromDocEntry(entry);
            var json = resultEntry.ToJson();

            // Assert - sourceRef should not be in the JSON
            Assert.That(resultEntry.SourceRef, Is.Null);
            Assert.That(json, Does.Not.Contain("\"sourceRef\""));
        }

        #endregion

        #region Truncation Tests

        [Test]
        public void ResultEntry_TruncatesLongDescription()
        {
            // Arrange - create entry with long description
            var longDescription = new string('x', 500);
            var entry = new DocEntry
            {
                Id = "TEST",
                Name = "TEST",
                Type = DocEntryType.Structure,
                Description = longDescription
            };

            // Act
            var resultEntry = KosDocResultEntry.FromDocEntry(entry);

            // Assert
            Assert.That(resultEntry.Description.Length, Is.LessThanOrEqualTo(KosDocSearchResult.MaxDescriptionLength));
            Assert.That(resultEntry.Description, Does.EndWith("..."));
        }

        [Test]
        public void ResultEntry_TruncatesLongSnippet()
        {
            // Arrange - create entry with long snippet
            var longSnippet = new string('x', 500);
            var entry = new DocEntry
            {
                Id = "TEST",
                Name = "TEST",
                Type = DocEntryType.Structure,
                Snippet = longSnippet
            };

            // Act
            var resultEntry = KosDocResultEntry.FromDocEntry(entry);

            // Assert
            Assert.That(resultEntry.Snippet.Length, Is.LessThanOrEqualTo(KosDocSearchResult.MaxSnippetLength));
            Assert.That(resultEntry.Snippet, Does.EndWith("..."));
        }

        [Test]
        public void ResultEntry_ShortTextNotTruncated()
        {
            // Arrange
            var shortText = "Short description";
            var entry = new DocEntry
            {
                Id = "TEST",
                Name = "TEST",
                Type = DocEntryType.Structure,
                Description = shortText
            };

            // Act
            var resultEntry = KosDocResultEntry.FromDocEntry(entry);

            // Assert
            Assert.That(resultEntry.Description, Is.EqualTo(shortText));
            Assert.That(resultEntry.Description, Does.Not.EndWith("..."));
        }

        #endregion

        #region Tool Definition Tests

        [Test]
        public void GetToolDefinitionJson_ReturnsValidJson()
        {
            // Act
            var json = KosDocTool.GetToolDefinitionJson();

            // Assert
            Assert.That(json, Is.Not.Null.And.Not.Empty);
            Assert.That(json, Does.Contain("\"type\": \"function\""));
            Assert.That(json, Does.Contain($"\"name\": \"{KosDocTool.ToolName}\""));
            Assert.That(json, Does.Contain("\"description\""));
            Assert.That(json, Does.Contain("\"parameters\""));
            Assert.That(json, Does.Contain("\"query\""));
        }

        [Test]
        public void ToolName_IsSearchKosDocs()
        {
            Assert.That(KosDocTool.ToolName, Is.EqualTo("search_kos_docs"));
        }

        #endregion

        #region Stability Tests

        [Test]
        public void Execute_SameQuery_ReturnsStableResults()
        {
            // Act - run the same query multiple times
            var result1 = _tool.Execute("VESSEL", 5);
            var result2 = _tool.Execute("VESSEL", 5);

            // Assert - results should be identical
            Assert.That(result1.Entries.Count, Is.EqualTo(result2.Entries.Count));
            for (int i = 0; i < result1.Entries.Count; i++)
            {
                Assert.That(result1.Entries[i].Id, Is.EqualTo(result2.Entries[i].Id));
            }
        }

        #endregion

        #region Representative Query Tests

        /// <summary>
        /// Tests that representative queries return expected top results.
        /// Covers identifiers, suffixes, commands, and functions.
        /// Note: The search algorithm matches entire query as substring, not individual words.
        /// </summary>
        [TestCase("velocity", "VELOCITY")]
        [TestCase("altitude", "ALTITUDE")]
        [TestCase("apoapsis", "APOAPSIS")]
        [TestCase("LOCK", "LOCK")]
        [TestCase("WAIT", "WAIT")]
        [TestCase("absolute", "ABS")] // Matches "absolute value" in description
        [TestCase("ROUND", "ROUND")]
        [TestCase("vessel", "VESSEL")]
        public void Execute_RepresentativeQuery_ReturnsExpectedTopResult(string query, string expectedIdContains)
        {
            // Act
            var result = _tool.Execute(query, 3);

            // Assert
            Assert.That(result.Success, Is.True, $"Query '{query}' should succeed");
            Assert.That(result.Entries, Is.Not.Empty, $"Query '{query}' should return results");
            Assert.That(result.Entries[0].Id, Does.Contain(expectedIdContains).IgnoreCase,
                $"Query '{query}' top result should contain '{expectedIdContains}', got '{result.Entries[0].Id}'");
        }

        [Test]
        public void Execute_TypoQuery_ReturnsNoMatchGracefully()
        {
            // Current implementation doesn't support fuzzy matching - document expected behavior
            var result = _tool.Execute("velocitty");

            Assert.That(result.Success, Is.True);
            // Expect no matches since we don't have fuzzy search
            Assert.That(result.Entries.Count, Is.EqualTo(0),
                "Typo queries return empty results (fuzzy search not implemented)");
        }

        [Test]
        public void Execute_NaturalLanguageQuery_FindsRelevantEntry()
        {
            // Note: Current search matches entire query as substring.
            // For natural language, extract the key term. Full phrase matching
            // would require word tokenization (future enhancement).
            // Here we test that extracting a single keyword works.
            var query = "velocity"; // Extracted keyword from "How do I get ship velocity?"

            // Act
            var result = _tool.Execute(query);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries.Any(e =>
                e.Id.IndexOf("VELOCITY", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                e.Description.IndexOf("velocity", System.StringComparison.OrdinalIgnoreCase) >= 0),
                Is.True, "Should find velocity-related entry from keyword query");
        }

        [Test]
        public void Execute_MultiWordQuery_ReturnsEmptyWithoutExactMatch()
        {
            // Document current behavior: multi-word queries match as exact phrase.
            // "ship velocity" won't match because no entry contains that exact substring.
            var result = _tool.Execute("ship velocity");

            Assert.That(result.Success, Is.True);
            // Current algorithm: no match unless exact phrase appears
            // This documents the limitation - could be enhanced with word tokenization later
            Assert.That(result.Entries.Count, Is.EqualTo(0),
                "Multi-word queries match as phrase, not individual words (current behavior)");
        }

        [Test]
        public void Execute_CommandQuery_ReturnsCommandType()
        {
            // Act - search for a command by its ID
            var result = _tool.Execute("LOCK", 3);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries, Is.Not.Empty);
            // Should find LOCK command
            Assert.That(result.Entries.Any(e => e.Id == "LOCK" && e.Kind == "command"),
                Is.True, "Should find LOCK command");
        }

        [Test]
        public void Execute_TagQuery_MatchesByTag()
        {
            // Act - search for a tag that exists on the LOCK command
            var result = _tool.Execute("steering", 3);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries, Is.Not.Empty);
            // Should find LOCK command via tag match
            Assert.That(result.Entries.Any(e => e.Id == "LOCK"),
                Is.True, "Should find LOCK command via 'steering' tag");
        }

        [Test]
        public void Execute_FunctionQuery_ReturnsFunctionType()
        {
            // Act - search for a function
            var result = _tool.Execute("ABS", 3);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries, Is.Not.Empty);
            Assert.That(result.Entries[0].Kind, Is.EqualTo("function"));
        }

        [Test]
        public void Execute_SuffixQuery_ReturnsSuffixType()
        {
            // Act - search for a suffix
            var result = _tool.Execute("VESSEL:ALTITUDE", 3);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries, Is.Not.Empty);
            Assert.That(result.Entries[0].Kind, Is.EqualTo("suffix"));
        }

        [Test]
        public void Execute_StructureQuery_ReturnsStructureType()
        {
            // Act
            var result = _tool.Execute("VESSEL", 3);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries, Is.Not.Empty);
            // VESSEL structure should be in results
            Assert.That(result.Entries.Any(e => e.Id == "VESSEL" && e.Kind == "structure"),
                Is.True, "Should find VESSEL structure");
        }

        [Test]
        public void Execute_AllResultsHaveSourceRef()
        {
            // Act - search that returns multiple results
            var result = _tool.Execute("VESSEL", 5);

            // Assert
            Assert.That(result.Success, Is.True);
            Assert.That(result.Entries, Is.Not.Empty);
            foreach (var entry in result.Entries)
            {
                Assert.That(entry.SourceRef, Is.Not.Null.And.Not.Empty,
                    $"Entry {entry.Id} should have a SourceRef");
                Assert.That(entry.SourceRef, Does.StartWith("https://ksp-kos.github.io/KOS/"),
                    $"Entry {entry.Id} SourceRef should be valid kOS docs URL");
            }
        }

        [Test]
        public void Execute_RankingIsDeterministic()
        {
            // Act - run identical query 10 times
            var results = new List<KosDocSearchResult>();
            for (int i = 0; i < 10; i++)
            {
                results.Add(_tool.Execute("velocity", 5));
            }

            // Assert - all results should be identical
            var firstResult = results[0];
            foreach (var result in results.Skip(1))
            {
                Assert.That(result.Entries.Count, Is.EqualTo(firstResult.Entries.Count));
                for (int i = 0; i < firstResult.Entries.Count; i++)
                {
                    Assert.That(result.Entries[i].Id, Is.EqualTo(firstResult.Entries[i].Id),
                        $"Ranking should be deterministic - mismatch at position {i}");
                }
            }
        }

        #endregion

        #region Helper Methods

        private void PopulateTestIndex(KosDocIndex index)
        {
            // Add VESSEL structure and suffixes
            index.AddEntry(new DocEntry
            {
                Id = "VESSEL",
                Name = "VESSEL",
                Type = DocEntryType.Structure,
                Description = "Represents a vessel in the game.",
                Tags = new List<string> { "vessel", "core" },
                SourceRef = "https://ksp-kos.github.io/KOS/structures/vessels/vessel.html"
            });

            index.AddEntry(new DocEntry
            {
                Id = "VESSEL:ALTITUDE",
                Name = "ALTITUDE",
                Type = DocEntryType.Suffix,
                ParentStructure = "VESSEL",
                Description = "The altitude of the vessel above sea level.",
                Snippet = "PRINT SHIP:ALTITUDE.",
                Tags = new List<string> { "vessel", "position" },
                SourceRef = "https://ksp-kos.github.io/KOS/structures/vessels/vessel.html#attribute:VESSEL:ALTITUDE"
            });

            index.AddEntry(new DocEntry
            {
                Id = "VESSEL:VELOCITY",
                Name = "VELOCITY",
                Type = DocEntryType.Suffix,
                ParentStructure = "VESSEL",
                Description = "The velocity vector of the vessel.",
                Snippet = "SET v TO SHIP:VELOCITY:SURFACE.",
                Tags = new List<string> { "vessel", "velocity" },
                SourceRef = "https://ksp-kos.github.io/KOS/structures/vessels/vessel.html#attribute:VESSEL:VELOCITY"
            });

            index.AddEntry(new DocEntry
            {
                Id = "VESSEL:APOAPSIS",
                Name = "APOAPSIS",
                Type = DocEntryType.Suffix,
                ParentStructure = "VESSEL",
                Description = "The apoapsis altitude of the vessel's orbit.",
                Tags = new List<string> { "vessel", "orbit" },
                SourceRef = "https://ksp-kos.github.io/KOS/structures/vessels/vessel.html#attribute:VESSEL:APOAPSIS"
            });

            // Add SHIP alias
            index.AddEntry(new DocEntry
            {
                Id = "SHIP",
                Name = "SHIP",
                Type = DocEntryType.Structure,
                Description = "Alias for VESSEL. Represents the active vessel.",
                Aliases = new List<string> { "VESSEL" },
                Tags = new List<string> { "vessel" },
                SourceRef = "https://ksp-kos.github.io/KOS/bindings.html#ship"
            });

            // Add some functions
            index.AddEntry(new DocEntry
            {
                Id = "ABS",
                Name = "ABS",
                Type = DocEntryType.Function,
                Description = "Returns the absolute value of a number.",
                Snippet = "PRINT ABS(-5).",
                Tags = new List<string> { "math", "absolute", "value" },
                SourceRef = "https://ksp-kos.github.io/KOS/math/basic.html#function:ABS"
            });

            index.AddEntry(new DocEntry
            {
                Id = "ROUND",
                Name = "ROUND",
                Type = DocEntryType.Function,
                Description = "Rounds a number to the nearest integer or specified decimal places.",
                Snippet = "PRINT ROUND(3.14159, 2). // prints 3.14",
                Tags = new List<string> { "math" },
                SourceRef = "https://ksp-kos.github.io/KOS/math/basic.html#function:ROUND"
            });

            // Add commands
            index.AddEntry(new DocEntry
            {
                Id = "LOCK",
                Name = "LOCK",
                Type = DocEntryType.Command,
                Description = "Lock a variable to an expression that is evaluated every tick.",
                Snippet = "LOCK STEERING TO PROGRADE.\nLOCK THROTTLE TO 1.",
                Tags = new List<string> { "control", "steering", "flight" },
                SourceRef = "https://ksp-kos.github.io/KOS/commands/flight/cooked.html#lock"
            });

            index.AddEntry(new DocEntry
            {
                Id = "WAIT",
                Name = "WAIT",
                Type = DocEntryType.Command,
                Description = "Pause script execution for a specified duration or until a condition is met.",
                Snippet = "WAIT 5. // wait 5 seconds\nWAIT UNTIL ALTITUDE > 10000.",
                Tags = new List<string> { "control", "flow", "pause", "execution" },
                SourceRef = "https://ksp-kos.github.io/KOS/commands/flow/wait.html"
            });
        }

        #endregion

        #region Test Helpers

        /// <summary>
        /// Testable KosDocTool that uses a provided index directly.
        /// </summary>
        private class TestableKosDocTool : KosDocTool
        {
            private readonly KosDocIndex _testIndex;

            public TestableKosDocTool(KosDocIndex index)
            {
                _testIndex = index;
            }

            public new KosDocSearchResult Execute(string query, int? maxResults = null)
            {
                // Validate query
                if (string.IsNullOrWhiteSpace(query))
                {
                    return KosDocSearchResult.Fail("Query cannot be empty");
                }

                if (query.Length < MinQueryLength)
                {
                    return KosDocSearchResult.Fail($"Query must be at least {MinQueryLength} characters");
                }

                // Validate and clamp maxResults
                int limit = maxResults ?? DefaultMaxResults;
                if (limit < 1)
                {
                    limit = 1;
                }
                else if (limit > MaxResultsLimit)
                {
                    limit = MaxResultsLimit;
                }

                // Perform search using test index
                var entries = _testIndex.Search(query, limit);
                return KosDocSearchResult.FromDocEntries(entries);
            }

            public new KosDocSearchResult ExecuteFromJson(string argumentsJson)
            {
                if (string.IsNullOrWhiteSpace(argumentsJson))
                {
                    return KosDocSearchResult.Fail("No arguments provided");
                }

                // Parse query
                string query = ExtractStringValue(argumentsJson, "query");
                if (string.IsNullOrEmpty(query))
                {
                    return KosDocSearchResult.Fail("Missing required parameter: query");
                }

                // Parse max_results (optional)
                int? maxResults = ExtractIntValue(argumentsJson, "max_results");

                return Execute(query, maxResults);
            }

            private static string ExtractStringValue(string json, string key)
            {
                var pattern = $"\"{key}\"";
                var keyIndex = json.IndexOf(pattern, System.StringComparison.Ordinal);
                if (keyIndex < 0) return null;

                var colonIndex = json.IndexOf(':', keyIndex + pattern.Length);
                if (colonIndex < 0) return null;

                var valueStart = colonIndex + 1;
                while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                    valueStart++;

                if (valueStart >= json.Length) return null;

                if (json[valueStart] == '"')
                {
                    return ExtractQuotedString(json, valueStart);
                }

                if (json.Substring(valueStart).StartsWith("null", System.StringComparison.Ordinal))
                {
                    return null;
                }

                return null;
            }

            private static string ExtractQuotedString(string json, int startIndex)
            {
                if (startIndex >= json.Length || json[startIndex] != '"')
                    return null;

                var sb = new System.Text.StringBuilder();
                int i = startIndex + 1;

                while (i < json.Length)
                {
                    char c = json[i];

                    if (c == '\\' && i + 1 < json.Length)
                    {
                        char next = json[i + 1];
                        switch (next)
                        {
                            case '"': sb.Append('"'); i += 2; break;
                            case '\\': sb.Append('\\'); i += 2; break;
                            case 'n': sb.Append('\n'); i += 2; break;
                            case 'r': sb.Append('\r'); i += 2; break;
                            case 't': sb.Append('\t'); i += 2; break;
                            default: sb.Append(next); i += 2; break;
                        }
                    }
                    else if (c == '"')
                    {
                        break;
                    }
                    else
                    {
                        sb.Append(c);
                        i++;
                    }
                }

                return sb.ToString();
            }

            private static int? ExtractIntValue(string json, string key)
            {
                var pattern = $"\"{key}\"";
                var keyIndex = json.IndexOf(pattern, System.StringComparison.Ordinal);
                if (keyIndex < 0) return null;

                var colonIndex = json.IndexOf(':', keyIndex + pattern.Length);
                if (colonIndex < 0) return null;

                var valueStart = colonIndex + 1;
                while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                    valueStart++;

                if (valueStart >= json.Length) return null;

                var sb = new System.Text.StringBuilder();
                while (valueStart < json.Length && (char.IsDigit(json[valueStart]) || json[valueStart] == '-'))
                {
                    sb.Append(json[valueStart]);
                    valueStart++;
                }

                if (sb.Length > 0 && int.TryParse(sb.ToString(), out int result))
                    return result;

                return null;
            }
        }

        #endregion
    }
}
