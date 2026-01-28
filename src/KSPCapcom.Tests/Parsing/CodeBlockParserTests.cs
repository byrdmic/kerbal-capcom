using System.Linq;
using NUnit.Framework;
using KSPCapcom.Parsing;

namespace KSPCapcom.Tests.Parsing
{
    [TestFixture]
    public class CodeBlockParserTests
    {
        private CodeBlockParser _parser;

        [SetUp]
        public void SetUp()
        {
            _parser = new CodeBlockParser();
        }

        #region Empty/Null Input Tests

        [Test]
        public void Parse_EmptyString_ReturnsEmptyContent()
        {
            var result = _parser.Parse("");

            Assert.That(result.HasCodeBlocks, Is.False);
            Assert.That(result.CodeBlockCount, Is.EqualTo(0));
            Assert.That(result.Segments, Is.Empty);
        }

        [Test]
        public void Parse_NullString_ReturnsEmptyContent()
        {
            var result = _parser.Parse(null);

            Assert.That(result.HasCodeBlocks, Is.False);
            Assert.That(result.CodeBlockCount, Is.EqualTo(0));
            Assert.That(result.Segments, Is.Empty);
        }

        [Test]
        public void Parse_WhitespaceOnlyString_ReturnsEmptyContent()
        {
            var result = _parser.Parse("   \t\n\r\n   ");

            // Whitespace-only prose is stripped
            Assert.That(result.HasCodeBlocks, Is.False);
            Assert.That(result.CodeBlockCount, Is.EqualTo(0));
        }

        #endregion

        #region Single Code Block Tests

        [Test]
        public void Parse_SingleCodeBlock_ExtractsCorrectly()
        {
            var text = @"```kos
SET x TO 5.
PRINT x.
```";

            var result = _parser.Parse(text);

            Assert.That(result.HasCodeBlocks, Is.True);
            Assert.That(result.CodeBlockCount, Is.EqualTo(1));
            Assert.That(result.Segments.Count, Is.EqualTo(1));

            var codeBlock = result.Segments[0] as CodeBlockSegment;
            Assert.That(codeBlock, Is.Not.Null);
            Assert.That(codeBlock.Language, Is.EqualTo("kos"));
            Assert.That(codeBlock.RawCode, Does.Contain("SET x TO 5."));
            Assert.That(codeBlock.RawCode, Does.Contain("PRINT x."));
            Assert.That(codeBlock.IsKosLikely, Is.True);
        }

        [Test]
        public void Parse_SingleCodeBlock_NoLanguage_ExtractsCorrectly()
        {
            var text = @"```
SET x TO 5.
LOCK THROTTLE TO 1.
```";

            var result = _parser.Parse(text);

            Assert.That(result.HasCodeBlocks, Is.True);
            Assert.That(result.CodeBlockCount, Is.EqualTo(1));

            var codeBlock = result.Segments[0] as CodeBlockSegment;
            Assert.That(codeBlock, Is.Not.Null);
            Assert.That(codeBlock.Language, Is.Null);
            Assert.That(codeBlock.RawCode, Does.Contain("SET x TO 5."));
            // Should still detect as kOS via heuristics
            Assert.That(codeBlock.IsKosLikely, Is.True);
        }

        [Test]
        public void Parse_SingleCodeBlock_KerboscriptLanguage_DetectedAsKos()
        {
            var text = @"```kerboscript
PRINT ""Hello"".
```";

            var result = _parser.Parse(text);

            Assert.That(result.HasCodeBlocks, Is.True);

            var codeBlock = result.Segments[0] as CodeBlockSegment;
            Assert.That(codeBlock.Language, Is.EqualTo("kerboscript"));
            Assert.That(codeBlock.IsKosLikely, Is.True);
        }

        [Test]
        public void Parse_SingleCodeBlock_CaseInsensitiveLanguage()
        {
            var text = @"```KOS
PRINT ""Hello"".
```";

            var result = _parser.Parse(text);

            var codeBlock = result.Segments[0] as CodeBlockSegment;
            Assert.That(codeBlock.Language, Is.EqualTo("kos"));
            Assert.That(codeBlock.IsKosLikely, Is.True);
        }

        #endregion

        #region Multiple Code Blocks Tests

        [Test]
        public void Parse_MultipleCodeBlocks_PreservesSurroundingProse()
        {
            var text = @"Here is the first script:

```kos
SET x TO 5.
```

And here is the second:

```kos
PRINT x.
```

That's all!";

            var result = _parser.Parse(text);

            Assert.That(result.HasCodeBlocks, Is.True);
            Assert.That(result.CodeBlockCount, Is.EqualTo(2));

            // Should have: prose, code, prose, code, prose
            Assert.That(result.Segments.Count, Is.EqualTo(5));

            Assert.That(result.Segments[0], Is.TypeOf<ProseSegment>());
            Assert.That(result.Segments[1], Is.TypeOf<CodeBlockSegment>());
            Assert.That(result.Segments[2], Is.TypeOf<ProseSegment>());
            Assert.That(result.Segments[3], Is.TypeOf<CodeBlockSegment>());
            Assert.That(result.Segments[4], Is.TypeOf<ProseSegment>());

            // Verify prose content
            var prose1 = result.Segments[0] as ProseSegment;
            Assert.That(prose1.Content, Does.Contain("first script"));

            var prose2 = result.Segments[2] as ProseSegment;
            Assert.That(prose2.Content, Does.Contain("second"));

            var prose3 = result.Segments[4] as ProseSegment;
            Assert.That(prose3.Content, Does.Contain("That's all"));
        }

        [Test]
        public void Parse_AdjacentCodeBlocks_NoProseInBetween()
        {
            var text = @"```kos
SET x TO 5.
```
```kos
PRINT x.
```";

            var result = _parser.Parse(text);

            Assert.That(result.CodeBlockCount, Is.EqualTo(2));
            // Two code blocks with optional whitespace-only prose stripped
            Assert.That(result.Segments.Count(s => s is CodeBlockSegment), Is.EqualTo(2));
        }

        #endregion

        #region Unterminated Fence Tests

        [Test]
        public void Parse_UnterminatedFence_TreatsAsProse()
        {
            var text = @"Here is some code:

```kos
SET x TO 5.
PRINT x.";

            var result = _parser.Parse(text);

            // Unterminated fence should not match as code block
            Assert.That(result.HasCodeBlocks, Is.False);
            Assert.That(result.CodeBlockCount, Is.EqualTo(0));

            // Everything should be treated as prose
            Assert.That(result.Segments.Count, Is.EqualTo(1));
            Assert.That(result.Segments[0], Is.TypeOf<ProseSegment>());
        }

        [Test]
        public void Parse_MismatchedFences_OnlyValidBlocksExtracted()
        {
            var text = @"```kos
SET x TO 5.
```

Some prose

```
This fence has no closing";

            var result = _parser.Parse(text);

            // Should only get the first valid code block
            Assert.That(result.CodeBlockCount, Is.EqualTo(1));

            var codeBlock = result.Segments.OfType<CodeBlockSegment>().First();
            Assert.That(codeBlock.RawCode, Does.Contain("SET x TO 5."));
        }

        #endregion

        #region kOS Heuristic Detection Tests

        [Test]
        public void IsLikelyKosCode_WithKosKeywords_ReturnsTrue()
        {
            // Contains SET and LOCK - two keywords
            var code = @"SET x TO 5.
LOCK THROTTLE TO 1.";

            Assert.That(_parser.IsLikelyKosCode(code), Is.True);
        }

        [Test]
        public void IsLikelyKosCode_WithSingleKeyword_ReturnsFalse()
        {
            // Only one keyword - not enough
            var code = "SET x TO 5.";

            Assert.That(_parser.IsLikelyKosCode(code), Is.False);
        }

        [Test]
        public void IsLikelyKosCode_WithShipColon_ReturnsTrue()
        {
            var code = @"PRINT SHIP:ALTITUDE.
WAIT 1.";

            Assert.That(_parser.IsLikelyKosCode(code), Is.True);
        }

        [Test]
        public void IsLikelyKosCode_WithMultipleKeywords_ReturnsTrue()
        {
            var code = @"LOCK STEERING TO HEADING(90, 90).
LOCK THROTTLE TO 1.
UNTIL SHIP:APOAPSIS > 80000 {
    WAIT 0.1.
}
PRINT ""Done"".";

            Assert.That(_parser.IsLikelyKosCode(code), Is.True);
        }

        [Test]
        public void IsLikelyKosCode_CaseInsensitive()
        {
            var code = @"set x to 5.
lock throttle to 1.";

            Assert.That(_parser.IsLikelyKosCode(code), Is.True);
        }

        [Test]
        public void IsLikelyKosCode_EmptyCode_ReturnsFalse()
        {
            Assert.That(_parser.IsLikelyKosCode(""), Is.False);
            Assert.That(_parser.IsLikelyKosCode(null), Is.False);
            Assert.That(_parser.IsLikelyKosCode("   "), Is.False);
        }

        [Test]
        public void IsLikelyKosCode_NonKosCode_ReturnsFalse()
        {
            // Python code - no kOS keywords
            var code = @"def hello():
    x = 5
    print(""Hello World"")";

            Assert.That(_parser.IsLikelyKosCode(code), Is.False);
        }

        [Test]
        public void IsLikelyKosCode_WordBoundary_NoFalsePositives()
        {
            // "SETTINGS" contains "SET" but shouldn't match
            var code = @"var SETTINGS = {};
var LOCKING_MECHANISM = true;";

            Assert.That(_parser.IsLikelyKosCode(code), Is.False);
        }

        #endregion

        #region Index Tracking Tests

        [Test]
        public void Parse_TracksCorrectStartEndIndexes()
        {
            var text = "Intro\n```kos\ncode\n```\nOutro";

            var result = _parser.Parse(text);

            // Should have: prose, code, prose
            Assert.That(result.Segments.Count, Is.EqualTo(3));

            var intro = result.Segments[0] as ProseSegment;
            Assert.That(intro.StartIndex, Is.EqualTo(0));

            var codeBlock = result.Segments[1] as CodeBlockSegment;
            Assert.That(codeBlock.StartIndex, Is.GreaterThan(intro.EndIndex).Or.EqualTo(intro.EndIndex));

            var outro = result.Segments[2] as ProseSegment;
            Assert.That(outro.StartIndex, Is.GreaterThanOrEqualTo(codeBlock.EndIndex));
        }

        #endregion

        #region Real-World Message Tests

        [Test]
        public void Parse_TypicalAssistantMessage_ParsesCorrectly()
        {
            var text = @"Here's a simple launch script for your rocket:

```kos
// Launch script
SET targetAlt TO 80000.

LOCK STEERING TO HEADING(90, 90).
LOCK THROTTLE TO 1.

UNTIL SHIP:ALTITUDE > targetAlt {
    IF MAXTHRUST = 0 {
        STAGE.
    }
    WAIT 0.1.
}

LOCK THROTTLE TO 0.
PRINT ""Reached target altitude"".
```

This script will:
1. Set a target altitude of 80km
2. Point the rocket straight up
3. Apply full throttle
4. Auto-stage when needed
5. Coast once at altitude";

            var result = _parser.Parse(text);

            Assert.That(result.HasCodeBlocks, Is.True);
            Assert.That(result.CodeBlockCount, Is.EqualTo(1));
            Assert.That(result.Segments.Count, Is.EqualTo(3)); // intro, code, explanation

            var codeBlock = result.Segments[1] as CodeBlockSegment;
            Assert.That(codeBlock.IsKosLikely, Is.True);
            Assert.That(codeBlock.RawCode, Does.Contain("SET targetAlt TO 80000."));
            Assert.That(codeBlock.RawCode, Does.Contain("LOCK STEERING"));
            Assert.That(codeBlock.RawCode, Does.Contain("UNTIL SHIP:ALTITUDE"));
        }

        [Test]
        public void Parse_MessageWithNoCodeBlocks_ReturnsProseOnly()
        {
            var text = @"Sure! Here's how you can approach the problem:

1. First, calculate the TWR
2. Then adjust your throttle accordingly
3. Finally, monitor your altitude

Let me know if you need more help!";

            var result = _parser.Parse(text);

            Assert.That(result.HasCodeBlocks, Is.False);
            Assert.That(result.CodeBlockCount, Is.EqualTo(0));
            Assert.That(result.Segments.Count, Is.EqualTo(1));
            Assert.That(result.Segments[0], Is.TypeOf<ProseSegment>());
        }

        #endregion

        #region Syntax Result Integration

        [Test]
        public void CodeBlockSegment_SyntaxResult_InitiallyNull()
        {
            var text = @"```kos
SET x TO 5.
```";

            var result = _parser.Parse(text);
            var codeBlock = result.Segments[0] as CodeBlockSegment;

            Assert.That(codeBlock.SyntaxResult, Is.Null);
        }

        [Test]
        public void CodeBlockSegment_SyntaxResult_CanBeSet()
        {
            var text = @"```kos
SET x TO 5.
```";

            var result = _parser.Parse(text);
            var codeBlock = result.Segments[0] as CodeBlockSegment;

            // Create a mock syntax result
            var syntaxResult = new KSPCapcom.Validation.KosSyntaxResult();

            codeBlock.SyntaxResult = syntaxResult;

            Assert.That(codeBlock.SyntaxResult, Is.SameAs(syntaxResult));
        }

        #endregion

        #region Edge Cases

        [Test]
        public void Parse_CodeBlockWithTrailingWhitespace_TrimsCode()
        {
            var text = "```kos\nSET x TO 5.\n\n   \n```";

            var result = _parser.Parse(text);
            var codeBlock = result.Segments[0] as CodeBlockSegment;

            // Trailing whitespace should be trimmed
            Assert.That(codeBlock.RawCode, Does.EndWith("."));
            Assert.That(codeBlock.RawCode, Does.Not.EndWith("\n"));
        }

        [Test]
        public void Parse_CodeBlockWithLeadingIndentation_PreservesIndentation()
        {
            var text = @"```kos
    SET x TO 5.
    PRINT x.
```";

            var result = _parser.Parse(text);
            var codeBlock = result.Segments[0] as CodeBlockSegment;

            // Leading indentation should be preserved
            Assert.That(codeBlock.RawCode, Does.StartWith("    SET"));
        }

        [Test]
        public void Parse_EmptyCodeBlock_ReturnsEmptyRawCode()
        {
            var text = @"```kos
```";

            var result = _parser.Parse(text);

            Assert.That(result.HasCodeBlocks, Is.True);
            var codeBlock = result.Segments[0] as CodeBlockSegment;
            Assert.That(codeBlock.RawCode, Is.Empty);
        }

        [Test]
        public void Parse_CodeBlockWithSpacesAfterLanguage_ParsesCorrectly()
        {
            // Some LLMs add spaces or tabs after the language
            var text = "```kos   \nSET x TO 5.\n```";

            var result = _parser.Parse(text);

            Assert.That(result.HasCodeBlocks, Is.True);
            var codeBlock = result.Segments[0] as CodeBlockSegment;
            Assert.That(codeBlock.Language, Is.EqualTo("kos"));
        }

        #endregion
    }
}
