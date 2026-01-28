using System.Linq;
using NUnit.Framework;
using KSPCapcom.Validation;

namespace KSPCapcom.Tests.Validation
{
    [TestFixture]
    public class KosSyntaxCheckerTests
    {
        private KosSyntaxChecker _checker;

        [SetUp]
        public void SetUp()
        {
            _checker = new KosSyntaxChecker();
        }

        #region Empty/Null Input Tests

        [Test]
        public void Check_EmptyScript_ReturnsNoIssues()
        {
            var result = _checker.Check("");

            Assert.That(result.HasIssues, Is.False);
            Assert.That(result.Issues, Is.Empty);
        }

        [Test]
        public void Check_NullScript_ReturnsNoIssues()
        {
            var result = _checker.Check(null);

            Assert.That(result.HasIssues, Is.False);
        }

        [Test]
        public void Check_WhitespaceOnlyScript_ReturnsNoIssues()
        {
            var result = _checker.Check("   \t\n\r\n   ");

            Assert.That(result.HasIssues, Is.False);
        }

        #endregion

        #region Balanced Braces Tests

        [Test]
        public void Check_BalancedBraces_ReturnsNoIssues()
        {
            var script = @"IF TRUE {
    PRINT ""Hello"".
}";

            var result = _checker.Check(script);

            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.UnbalancedBrace), Is.False);
        }

        [Test]
        public void Check_MissingClosingBrace_ReturnsIssue()
        {
            var script = @"IF TRUE {
    PRINT ""Hello"".
";

            var result = _checker.Check(script);

            Assert.That(result.HasIssues, Is.True);
            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.UnbalancedBrace), Is.True);
        }

        [Test]
        public void Check_ExtraClosingBrace_ReturnsIssue()
        {
            var script = @"IF TRUE {
    PRINT ""Hello"".
}
}";

            var result = _checker.Check(script);

            Assert.That(result.HasIssues, Is.True);
            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.UnbalancedBrace), Is.True);
        }

        [Test]
        public void Check_NestedBraces_Balanced_ReturnsNoIssues()
        {
            var script = @"IF TRUE {
    IF FALSE {
        PRINT ""nested"".
    }
}";

            var result = _checker.Check(script);

            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.UnbalancedBrace), Is.False);
        }

        [Test]
        public void Check_BracesInString_Ignored()
        {
            // Braces inside strings should not be counted
            var script = @"PRINT ""This has { braces }"".";

            var result = _checker.Check(script);

            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.UnbalancedBrace), Is.False);
        }

        [Test]
        public void Check_BracesInComment_Ignored()
        {
            // Braces inside comments should not be counted
            var script = @"// This comment has { an opening brace
PRINT ""Hello"".";

            var result = _checker.Check(script);

            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.UnbalancedBrace), Is.False);
        }

        [Test]
        public void Check_BracesInBlockComment_Ignored()
        {
            var script = @"/* This comment has { braces } */
PRINT ""Hello"".";

            var result = _checker.Check(script);

            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.UnbalancedBrace), Is.False);
        }

        #endregion

        #region Balanced Parentheses Tests

        [Test]
        public void Check_BalancedParentheses_ReturnsNoIssues()
        {
            var script = @"SET x TO (1 + 2) * (3 + 4).";

            var result = _checker.Check(script);

            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.UnbalancedParenthesis), Is.False);
        }

        [Test]
        public void Check_MissingClosingParen_ReturnsIssue()
        {
            var script = @"SET x TO (1 + 2.";

            var result = _checker.Check(script);

            Assert.That(result.HasIssues, Is.True);
            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.UnbalancedParenthesis), Is.True);
        }

        [Test]
        public void Check_ExtraClosingParen_ReturnsIssue()
        {
            var script = @"SET x TO 1 + 2).";

            var result = _checker.Check(script);

            Assert.That(result.HasIssues, Is.True);
            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.UnbalancedParenthesis), Is.True);
        }

        [Test]
        public void Check_ParensInString_Ignored()
        {
            var script = @"PRINT ""(unbalanced"".";

            var result = _checker.Check(script);

            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.UnbalancedParenthesis), Is.False);
        }

        #endregion

        #region Markdown Artifact Tests

        [Test]
        public void Check_MarkdownBacktick_ReturnsIssue()
        {
            var script = @"SET x TO `SHIP:ALTITUDE`.";

            var result = _checker.Check(script);

            Assert.That(result.HasIssues, Is.True);
            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.MarkdownBacktick), Is.True);
        }

        [Test]
        public void Check_MarkdownBacktickInComment_Ignored()
        {
            // Backticks in comments are fine (might be documentation)
            var script = @"// Use `SHIP:ALTITUDE` for altitude
PRINT SHIP:ALTITUDE.";

            var result = _checker.Check(script);

            // The backtick is after the comment marker, so it should be ignored
            // However, current implementation checks the whole line, so this might flag
            // Let's verify our expectation
            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.MarkdownBacktick), Is.False);
        }

        [Test]
        public void Check_MarkdownBullet_ReturnsIssue()
        {
            var script = @"- SET x TO 5.
- PRINT x.";

            var result = _checker.Check(script);

            Assert.That(result.HasIssues, Is.True);
            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.MarkdownBullet), Is.True);
        }

        [Test]
        public void Check_AsteriskMultiplication_NotBullet()
        {
            // Asterisk in the middle of a line is not a bullet
            var script = @"SET x TO 5 * 3.";

            var result = _checker.Check(script);

            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.MarkdownBullet), Is.False);
        }

        [Test]
        public void Check_MarkdownHeader_ReturnsIssue()
        {
            var script = @"# Script Header
SET x TO 5.";

            var result = _checker.Check(script);

            Assert.That(result.HasIssues, Is.True);
            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.MarkdownHeader), Is.True);
        }

        [Test]
        public void Check_MarkdownH2Header_ReturnsIssue()
        {
            var script = @"## Section
PRINT ""Hello"".";

            var result = _checker.Check(script);

            Assert.That(result.HasIssues, Is.True);
            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.MarkdownHeader), Is.True);
        }

        #endregion

        #region Smart Quote Tests

        [Test]
        public void Check_SmartDoubleQuoteLeft_ReturnsIssue()
        {
            // \u201C is left double quote
            var script = "PRINT \u201CHello\".";

            var result = _checker.Check(script);

            Assert.That(result.HasIssues, Is.True);
            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.SmartQuote), Is.True);
        }

        [Test]
        public void Check_SmartDoubleQuoteRight_ReturnsIssue()
        {
            // \u201D is right double quote
            var script = "PRINT \"Hello\u201D.";

            var result = _checker.Check(script);

            Assert.That(result.HasIssues, Is.True);
            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.SmartQuote), Is.True);
        }

        [Test]
        public void Check_SmartSingleQuote_ReturnsIssue()
        {
            // \u2018 is left single quote, \u2019 is right single quote
            var script = "SET name TO \u2018test\u2019.";

            var result = _checker.Check(script);

            Assert.That(result.HasIssues, Is.True);
            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.SmartQuote), Is.True);
        }

        [Test]
        public void Check_StraightQuotes_NoIssue()
        {
            var script = "PRINT \"Hello World\".";

            var result = _checker.Check(script);

            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.SmartQuote), Is.False);
        }

        #endregion

        #region Missing Terminator Tests

        [Test]
        public void Check_ProperTerminator_NoIssue()
        {
            var script = @"SET x TO 5.
PRINT x.";

            var result = _checker.Check(script);

            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.MissingTerminator), Is.False);
        }

        [Test]
        public void Check_MissingTerminator_ReturnsIssue()
        {
            var script = @"SET x TO 5.
PRINT x";

            var result = _checker.Check(script);

            Assert.That(result.HasIssues, Is.True);
            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.MissingTerminator), Is.True);
        }

        [Test]
        public void Check_EndsWithBrace_NoTerminatorIssue()
        {
            var script = @"IF TRUE {
    PRINT ""Hello"".
}";

            var result = _checker.Check(script);

            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.MissingTerminator), Is.False);
        }

        [Test]
        public void Check_TrailingComment_TerminatorBeforeComment()
        {
            var script = @"PRINT ""Hello"". // This is a comment";

            var result = _checker.Check(script);

            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.MissingTerminator), Is.False);
        }

        [Test]
        public void Check_LineContinuation_NoTerminatorIssue()
        {
            // Lines ending with operators are continuations
            var script = @"SET x TO 1 +
    2 +
    3.";

            var result = _checker.Check(script);

            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.MissingTerminator), Is.False);
        }

        #endregion

        #region Clean Script Tests

        [Test]
        public void Check_CleanKosScript_ReturnsNoIssues()
        {
            var script = @"// Simple launch script
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
PRINT ""Done"".";

            var result = _checker.Check(script);

            Assert.That(result.HasIssues, Is.False,
                "Clean kOS script should have no issues. Issues found: " +
                string.Join(", ", result.Issues.Select(i => i.ToString())));
        }

        [Test]
        public void Check_ComplexScript_ReturnsNoIssues()
        {
            var script = @"// Complex ascent script
PARAMETER targetAlt IS 80000.

FUNCTION calculateTWR {
    IF SHIP:MASS > 0 {
        RETURN MAXTHRUST / (SHIP:MASS * 9.81).
    }
    RETURN 0.
}

LOCAL twr IS calculateTWR().
PRINT ""TWR: "" + twr.

IF twr > 1 {
    LOCK STEERING TO HEADING(90, 90 - (SHIP:ALTITUDE / 1000)).
    LOCK THROTTLE TO 1.

    UNTIL SHIP:APOAPSIS > targetAlt {
        WAIT 0.1.
    }
}

PRINT ""Target apoapsis reached"".";

            var result = _checker.Check(script);

            Assert.That(result.HasIssues, Is.False,
                "Complex clean kOS script should have no issues. Issues found: " +
                string.Join(", ", result.Issues.Select(i => i.ToString())));
        }

        #endregion

        #region Line Number Tests

        [Test]
        public void Check_ReportsCorrectLineNumber()
        {
            var script = @"SET x TO 5.
SET y TO 10.
SET z TO {"; // Unbalanced on line 3

            var result = _checker.Check(script);

            Assert.That(result.HasIssues, Is.True);
            var braceIssue = result.Issues.FirstOrDefault(i => i.Type == SyntaxIssueType.UnbalancedBrace);
            Assert.That(braceIssue, Is.Not.Null);
            Assert.That(braceIssue.Line, Is.EqualTo(3));
        }

        [Test]
        public void Check_MarkdownBullet_ReportsCorrectLineNumber()
        {
            var script = @"SET x TO 5.
- PRINT x.
SET y TO 10.";

            var result = _checker.Check(script);

            var bulletIssue = result.Issues.FirstOrDefault(i => i.Type == SyntaxIssueType.MarkdownBullet);
            Assert.That(bulletIssue, Is.Not.Null);
            Assert.That(bulletIssue.Line, Is.EqualTo(2));
        }

        #endregion

        #region Edge Cases

        [Test]
        public void Check_EscapedQuoteInString_HandledCorrectly()
        {
            var script = @"PRINT ""He said \""Hello\"""".";

            var result = _checker.Check(script);

            // Should not report unbalanced issues due to escaped quotes
            Assert.That(result.Issues.Any(i =>
                i.Type == SyntaxIssueType.UnbalancedBrace ||
                i.Type == SyntaxIssueType.UnbalancedParenthesis), Is.False);
        }

        [Test]
        public void Check_MultiLineBlockComment_HandledCorrectly()
        {
            var script = @"SET x TO 5.
/* This is a
   multi-line comment
   with { braces } and (parens) */
PRINT x.";

            var result = _checker.Check(script);

            Assert.That(result.Issues.Any(i =>
                i.Type == SyntaxIssueType.UnbalancedBrace ||
                i.Type == SyntaxIssueType.UnbalancedParenthesis), Is.False);
        }

        [Test]
        public void Check_EmptyBlockComment_HandledCorrectly()
        {
            var script = @"/**/SET x TO 5.";

            var result = _checker.Check(script);

            // Should be valid - empty block comment followed by code
            Assert.That(result.Issues.Any(i => i.Type == SyntaxIssueType.UnbalancedBrace), Is.False);
        }

        #endregion
    }
}
