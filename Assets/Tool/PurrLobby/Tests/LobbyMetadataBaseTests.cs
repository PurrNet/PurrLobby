using System.Collections.Generic;
using NUnit.Framework;

namespace PurrNet.Lobby.Tests
{
    public class LobbyMetadataBaseTests
    {
        private TestMetadata _metadata;
        private List<(string key, string value)> _events;

        [SetUp]
        public void SetUp()
        {
            _metadata = new TestMetadata();
            _events = new List<(string, string)>();
            _metadata.onDataChanged += (key, value) => _events.Add((key, value));
        }

        [Test]
        public void SetData_EchoesLocallyAndPushesOnce()
        {
            _metadata.SetData("key", "value");

            Assert.AreEqual("value", _metadata.GetData("key"));
            Assert.AreEqual(new[] { ("key", "value") }, _events);
            Assert.AreEqual(new[] { ("key", "value") }, _metadata.pushedChanges);
        }

        [Test]
        public void SetData_SameValue_IsNoOp()
        {
            _metadata.SetData("key", "value");
            _events.Clear();
            _metadata.pushedChanges.Clear();

            _metadata.SetData("key", "value");

            Assert.IsEmpty(_events);
            Assert.IsEmpty(_metadata.pushedChanges);
        }

        [Test]
        public void SetData_EmptyKey_IsIgnored()
        {
            _metadata.SetData("", "value");
            _metadata.SetData(null, "value");

            Assert.IsEmpty(_events);
            Assert.IsEmpty(_metadata.pushedChanges);
        }

        [Test]
        public void SetData_WhenReadOnly_IsRejected()
        {
            _metadata.canWrite = false;

            _metadata.SetData("key", "value");

            Assert.IsFalse(_metadata.ContainsData("key"));
            Assert.IsEmpty(_events);
            Assert.IsEmpty(_metadata.pushedChanges);
        }

        [Test]
        public void RemoveData_EchoesNullAndPushes()
        {
            _metadata.SetData("key", "value");
            _events.Clear();
            _metadata.pushedChanges.Clear();

            _metadata.RemoveData("key");

            Assert.IsFalse(_metadata.ContainsData("key"));
            Assert.AreEqual(new[] { ("key", (string)null) }, _events);
            Assert.AreEqual(new[] { ("key", (string)null) }, _metadata.pushedChanges);
        }

        [Test]
        public void RemoveData_MissingKey_IsNoOp()
        {
            _metadata.RemoveData("missing");

            Assert.IsEmpty(_events);
            Assert.IsEmpty(_metadata.pushedChanges);
        }

        [Test]
        public void GetData_MissingKey_ReturnsNull()
        {
            Assert.IsNull(_metadata.GetData("missing"));
            Assert.IsFalse(_metadata.TryGetData("missing", out _));
        }

        [Test]
        public void ReplaceFrom_FiresDiffOnly()
        {
            _metadata.SetData("keep", "same");
            _metadata.SetData("change", "old");
            _metadata.SetData("remove", "gone");
            _events.Clear();

            _metadata.ReplaceFrom(new Dictionary<string, string>
            {
                { "keep", "same" },
                { "change", "new" },
                { "added", "fresh" },
            });

            CollectionAssert.AreEquivalent(new[]
            {
                ("remove", (string)null),
                ("change", "new"),
                ("added", "fresh"),
            }, _events);

            Assert.AreEqual("same", _metadata.GetData("keep"));
            Assert.AreEqual("new", _metadata.GetData("change"));
            Assert.AreEqual("fresh", _metadata.GetData("added"));
            Assert.IsFalse(_metadata.ContainsData("remove"));
        }

        [Test]
        public void ReplaceFrom_IsInboundOnly_NeverPushes()
        {
            _metadata.ReplaceFrom(new Dictionary<string, string> { { "key", "value" } });

            Assert.IsEmpty(_metadata.pushedChanges);
        }

        [Test]
        public void ReplaceFrom_Null_ClearsEverything()
        {
            _metadata.SetData("a", "1");
            _metadata.SetData("b", "2");
            _events.Clear();

            _metadata.ReplaceFrom(null);

            CollectionAssert.AreEquivalent(new[]
            {
                ("a", (string)null),
                ("b", (string)null),
            }, _events);
            Assert.IsFalse(_metadata.ContainsData("a"));
            Assert.IsFalse(_metadata.ContainsData("b"));
        }

        [Test]
        public void ApplyPatch_NullValueDeletes_SameValueSkips()
        {
            _metadata.SetData("delete", "x");
            _metadata.SetData("same", "y");
            _events.Clear();

            _metadata.ApplyPatch(new Dictionary<string, string>
            {
                { "delete", null },
                { "same", "y" },
                { "added", "z" },
            });

            CollectionAssert.AreEquivalent(new[]
            {
                ("delete", (string)null),
                ("added", "z"),
            }, _events);
            Assert.IsFalse(_metadata.ContainsData("delete"));
            Assert.AreEqual("z", _metadata.GetData("added"));
        }
    }
}
