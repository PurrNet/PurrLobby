using System.Collections.Generic;
using System.Text;
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

    public class LobbyChatBaseTests
    {
        private TestPlayer _localPlayer;
        private TestPlayer _remotePlayer;
        private TestChat _chat;
        private List<(IPlayer sender, string message)> _received;

        [SetUp]
        public void SetUp()
        {
            _localPlayer = new TestPlayer("local");
            _remotePlayer = new TestPlayer("remote");
            _chat = new TestChat(_localPlayer);
            _received = new List<(IPlayer, string)>();
            _chat.onMessageReceived += (sender, data) =>
                _received.Add((sender, Encoding.UTF8.GetString(data)));
        }

        [Test]
        public void SendMessage_LoopsBackBeforeSendingToProvider()
        {
            var order = new List<string>();
            _chat.onMessageReceived += (_, _) => order.Add("loopback");
            _chat.onSend += _ => order.Add("provider");

            _chat.SendMessage(Encoding.UTF8.GetBytes("hello"));

            Assert.AreEqual(new[] { "loopback", "provider" }, order);
            Assert.AreEqual(1, _chat.sentMessages.Count);
            Assert.AreEqual(new[] { (_localPlayer as IPlayer, "hello") }, _received);
        }

        [Test]
        public void ReceiveFromProvider_MatchingLocalEcho_IsConsumed()
        {
            var data = Encoding.UTF8.GetBytes("hello");
            _chat.SendMessage(data);

            _chat.ReceiveFromProviderPublic(_localPlayer, data);

            Assert.AreEqual(new[] { (_localPlayer as IPlayer, "hello") }, _received);
        }

        [Test]
        public void ReceiveFromProvider_RemoteMessageWithSamePayload_IsDelivered()
        {
            var data = Encoding.UTF8.GetBytes("hello");
            _chat.SendMessage(data);

            _chat.ReceiveFromProviderPublic(_remotePlayer, data);

            Assert.AreEqual(new[]
            {
                (_localPlayer as IPlayer, "hello"),
                (_remotePlayer as IPlayer, "hello"),
            }, _received);
        }

        [Test]
        public void ReceiveFromProvider_UnmatchedLocalMessage_IsDelivered()
        {
            _chat.ReceiveFromProviderPublic(_localPlayer, Encoding.UTF8.GetBytes("server event"));

            Assert.AreEqual(new[] { (_localPlayer as IPlayer, "server event") }, _received);
        }

        [Test]
        public void RepeatedMessages_ConsumeOneProviderEchoEach()
        {
            var data = Encoding.UTF8.GetBytes("same");
            _chat.SendMessage(data);
            _chat.SendMessage(data);

            _chat.ReceiveFromProviderPublic(_localPlayer, data);
            _chat.ReceiveFromProviderPublic(_localPlayer, data);

            Assert.AreEqual(2, _received.Count);
            Assert.AreEqual(2, _chat.sentMessages.Count);
        }

        [Test]
        public void SendMessage_WithoutLocalPlayer_StillSends()
        {
            _chat.localPlayerValue = null;

            _chat.SendMessage(Encoding.UTF8.GetBytes("hello"));

            Assert.IsEmpty(_received);
            Assert.AreEqual(1, _chat.sentMessages.Count);
        }

        [Test]
        public void SendMessage_NullOrEmpty_IsIgnored()
        {
            _chat.SendMessage(null);
            _chat.SendMessage(System.Array.Empty<byte>());

            Assert.IsEmpty(_received);
            Assert.IsEmpty(_chat.sentMessages);
        }
    }
}
