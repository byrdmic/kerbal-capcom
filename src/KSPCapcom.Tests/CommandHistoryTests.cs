using NUnit.Framework;
using System.Collections.Generic;

namespace KSPCapcom.Tests
{
    /// <summary>
    /// Tests for command history ring buffer logic.
    /// Uses a test helper class that mirrors ChatPanel's history implementation.
    /// </summary>
    [TestFixture]
    public class CommandHistoryTests
    {
        private CommandHistoryHelper _history;

        [SetUp]
        public void SetUp()
        {
            _history = new CommandHistoryHelper(maxSize: 50);
        }

        #region Add and Retrieve Tests

        [Test]
        public void Add_SingleCommand_CanRetrieveViaNavigateUp()
        {
            _history.Add("test command");
            _history.SetCurrentInput("");

            _history.NavigateUp();

            Assert.That(_history.CurrentInput, Is.EqualTo("test command"));
        }

        [Test]
        public void Add_MultipleCommands_NavigateUpRetrievesNewestFirst()
        {
            _history.Add("first");
            _history.Add("second");
            _history.Add("third");
            _history.SetCurrentInput("");

            _history.NavigateUp();
            Assert.That(_history.CurrentInput, Is.EqualTo("third"));

            _history.NavigateUp();
            Assert.That(_history.CurrentInput, Is.EqualTo("second"));

            _history.NavigateUp();
            Assert.That(_history.CurrentInput, Is.EqualTo("first"));
        }

        #endregion

        #region Capacity Limit Tests

        [Test]
        public void Add_ExceedsCapacity_DropsOldest()
        {
            var smallHistory = new CommandHistoryHelper(maxSize: 3);
            smallHistory.Add("one");
            smallHistory.Add("two");
            smallHistory.Add("three");
            smallHistory.Add("four"); // Should drop "one"
            smallHistory.SetCurrentInput("");

            // Navigate through all entries
            smallHistory.NavigateUp();
            Assert.That(smallHistory.CurrentInput, Is.EqualTo("four"));

            smallHistory.NavigateUp();
            Assert.That(smallHistory.CurrentInput, Is.EqualTo("three"));

            smallHistory.NavigateUp();
            Assert.That(smallHistory.CurrentInput, Is.EqualTo("two"));

            // Should stay at "two" - "one" was dropped
            smallHistory.NavigateUp();
            Assert.That(smallHistory.CurrentInput, Is.EqualTo("two"));
        }

        #endregion

        #region Consecutive Duplicate Tests

        [Test]
        public void Add_ConsecutiveDuplicate_NotStored()
        {
            _history.Add("test");
            _history.Add("test"); // Should be skipped
            _history.SetCurrentInput("");

            _history.NavigateUp();
            Assert.That(_history.CurrentInput, Is.EqualTo("test"));

            // Should stay at "test" - only one entry
            _history.NavigateUp();
            Assert.That(_history.CurrentInput, Is.EqualTo("test"));
        }

        [Test]
        public void Add_NonConsecutiveDuplicate_BothStored()
        {
            _history.Add("test");
            _history.Add("other");
            _history.Add("test"); // Different from last, should be stored
            _history.SetCurrentInput("");

            _history.NavigateUp();
            Assert.That(_history.CurrentInput, Is.EqualTo("test"));

            _history.NavigateUp();
            Assert.That(_history.CurrentInput, Is.EqualTo("other"));

            _history.NavigateUp();
            Assert.That(_history.CurrentInput, Is.EqualTo("test"));
        }

        #endregion

        #region Whitespace Rejection Tests

        [Test]
        public void Add_EmptyString_NotStored()
        {
            _history.Add("");
            _history.SetCurrentInput("draft");

            _history.NavigateUp();

            // Should still be at draft - nothing in history
            Assert.That(_history.CurrentInput, Is.EqualTo("draft"));
            Assert.That(_history.IsNavigating, Is.False);
        }

        [Test]
        public void Add_WhitespaceOnly_NotStored()
        {
            _history.Add("   ");
            _history.Add("\t\n");
            _history.SetCurrentInput("draft");

            _history.NavigateUp();

            // Should still be at draft - nothing in history
            Assert.That(_history.CurrentInput, Is.EqualTo("draft"));
            Assert.That(_history.IsNavigating, Is.False);
        }

        [Test]
        public void Add_Null_NotStored()
        {
            _history.Add(null);
            _history.SetCurrentInput("draft");

            _history.NavigateUp();

            Assert.That(_history.CurrentInput, Is.EqualTo("draft"));
            Assert.That(_history.IsNavigating, Is.False);
        }

        #endregion

        #region Navigation Bounds Tests

        [Test]
        public void NavigateUp_EmptyHistory_DoesNothing()
        {
            _history.SetCurrentInput("current");

            _history.NavigateUp();

            Assert.That(_history.CurrentInput, Is.EqualTo("current"));
            Assert.That(_history.IsNavigating, Is.False);
        }

        [Test]
        public void NavigateUp_AtOldest_StaysAtOldest()
        {
            _history.Add("oldest");
            _history.Add("newest");
            _history.SetCurrentInput("");

            _history.NavigateUp(); // newest
            _history.NavigateUp(); // oldest
            _history.NavigateUp(); // should stay at oldest
            _history.NavigateUp(); // should stay at oldest

            Assert.That(_history.CurrentInput, Is.EqualTo("oldest"));
        }

        [Test]
        public void NavigateDown_NotNavigating_DoesNothing()
        {
            _history.Add("test");
            _history.SetCurrentInput("current");

            _history.NavigateDown();

            Assert.That(_history.CurrentInput, Is.EqualTo("current"));
            Assert.That(_history.IsNavigating, Is.False);
        }

        [Test]
        public void NavigateDown_AtNewest_ReturnsToWorkingCopy()
        {
            _history.Add("first");
            _history.Add("second");
            _history.SetCurrentInput("draft");

            _history.NavigateUp(); // second
            _history.NavigateDown(); // back to draft

            Assert.That(_history.CurrentInput, Is.EqualTo("draft"));
            Assert.That(_history.IsNavigating, Is.False);
        }

        #endregion

        #region Working Copy Preservation Tests

        [Test]
        public void NavigateUp_PreservesWorkingCopy()
        {
            _history.Add("history entry");
            _history.SetCurrentInput("my draft text");

            _history.NavigateUp();
            Assert.That(_history.CurrentInput, Is.EqualTo("history entry"));

            _history.NavigateDown();
            Assert.That(_history.CurrentInput, Is.EqualTo("my draft text"));
        }

        [Test]
        public void NavigateUp_ThenDown_MultipleTimes_PreservesWorkingCopy()
        {
            _history.Add("one");
            _history.Add("two");
            _history.SetCurrentInput("draft");

            _history.NavigateUp(); // two
            _history.NavigateUp(); // one
            _history.NavigateDown(); // two
            _history.NavigateDown(); // draft

            Assert.That(_history.CurrentInput, Is.EqualTo("draft"));
        }

        [Test]
        public void ManualEdit_ExitsNavigationMode()
        {
            _history.Add("history");
            _history.SetCurrentInput("");

            _history.NavigateUp();
            Assert.That(_history.IsNavigating, Is.True);

            // Simulate user typing
            _history.OnManualEdit("typed text");

            Assert.That(_history.IsNavigating, Is.False);
            Assert.That(_history.CurrentInput, Is.EqualTo("typed text"));
        }

        #endregion
    }

    /// <summary>
    /// Test helper that mirrors ChatPanel's command history implementation.
    /// This allows testing the logic without Unity dependencies.
    /// </summary>
    public class CommandHistoryHelper
    {
        private readonly List<string> _history = new List<string>();
        private readonly int _maxSize;
        private int _historyIndex = -1;
        private string _workingCopy = "";

        public string CurrentInput { get; private set; } = "";
        public bool IsNavigating => _historyIndex != -1;

        public CommandHistoryHelper(int maxSize = 50)
        {
            _maxSize = maxSize;
        }

        public void SetCurrentInput(string input)
        {
            CurrentInput = input;
        }

        public void Add(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
            {
                return;
            }

            // Skip consecutive duplicates
            if (_history.Count > 0 && _history[_history.Count - 1] == command)
            {
                return;
            }

            _history.Add(command);

            // Trim if exceeds capacity
            while (_history.Count > _maxSize)
            {
                _history.RemoveAt(0);
            }

            // Reset navigation state
            _historyIndex = -1;
            _workingCopy = "";
        }

        public void NavigateUp()
        {
            if (_history.Count == 0)
            {
                return;
            }

            if (_historyIndex == -1)
            {
                _workingCopy = CurrentInput;
                _historyIndex = _history.Count - 1;
            }
            else if (_historyIndex > 0)
            {
                _historyIndex--;
            }

            CurrentInput = _history[_historyIndex];
        }

        public void NavigateDown()
        {
            if (_historyIndex == -1)
            {
                return;
            }

            if (_historyIndex < _history.Count - 1)
            {
                _historyIndex++;
                CurrentInput = _history[_historyIndex];
            }
            else
            {
                _historyIndex = -1;
                CurrentInput = _workingCopy;
            }
        }

        public void OnManualEdit(string newText)
        {
            if (_historyIndex != -1)
            {
                _historyIndex = -1;
                _workingCopy = "";
            }
            CurrentInput = newText;
        }
    }
}
