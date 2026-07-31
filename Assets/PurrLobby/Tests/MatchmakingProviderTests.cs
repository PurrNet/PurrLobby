using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace PurrNet.Lobby.Tests
{
    public class MatchmakingProviderTests
    {
        private TestMatchmakingProvider _provider;
        private List<string> _sequence;

        [SetUp]
        public void SetUp()
        {
            _provider = ScriptableObject.CreateInstance<TestMatchmakingProvider>();
            _sequence = new List<string>();
            _provider.onStatusChanged += (_, status) => _sequence.Add($"status:{status}");
            _provider.onMatchFound += (_, _) => _sequence.Add("found");
            _provider.onMatchmakingError += (_, error) => _sequence.Add($"error:{error}");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_provider);
        }

        [Test]
        public void BeginTicket_SetsActive_AndRaisesSearching()
        {
            var ticket = _provider.BeginTicketPublic();

            Assert.IsFalse(string.IsNullOrEmpty(ticket.ticketId));
            Assert.AreEqual(ticket.ticketId, _provider.activeTicketPublic?.ticketId);
            Assert.AreEqual(new[] { "status:Searching" }, _sequence);
        }

        [Test]
        public void BeginTicket_UsesExternalId()
        {
            var ticket = _provider.BeginTicketPublic("external-42");

            Assert.AreEqual("external-42", ticket.ticketId);
        }

        [Test]
        public void IsStale_TracksActiveTicket()
        {
            var ticket = _provider.BeginTicketPublic();

            Assert.IsFalse(_provider.IsStalePublic(ticket));
            Assert.IsTrue(_provider.IsStalePublic(new MatchmakingTicket { ticketId = "other" }));

            var replacement = _provider.BeginTicketPublic();
            Assert.IsTrue(_provider.IsStalePublic(ticket));
            Assert.IsFalse(_provider.IsStalePublic(replacement));
        }

        [Test]
        public void IsStale_WithNoActiveTicket_IsTrue()
        {
            Assert.IsTrue(_provider.IsStalePublic(new MatchmakingTicket { ticketId = "any" }));
        }

        [Test]
        public void TryConsumeActiveTicket_ConsumesExactlyOnce()
        {
            var ticket = _provider.BeginTicketPublic();

            Assert.IsFalse(_provider.TryConsumeActiveTicketPublic(new MatchmakingTicket { ticketId = "other" }));
            Assert.IsNotNull(_provider.activeTicketPublic);

            Assert.IsTrue(_provider.TryConsumeActiveTicketPublic(ticket));
            Assert.IsNull(_provider.activeTicketPublic);

            Assert.IsFalse(_provider.TryConsumeActiveTicketPublic(ticket));
        }

        [Test]
        public void CompleteMatch_ClearsTicket_AndRaisesFoundThenMatchFound()
        {
            var ticket = _provider.BeginTicketPublic();
            _sequence.Clear();

            _provider.CompleteMatchPublic(ticket, new MatchResult());

            Assert.IsNull(_provider.activeTicketPublic);
            Assert.AreEqual(new[] { "status:Found", "found" }, _sequence);
        }

        [Test]
        public void FailMatch_ClearsTicket_AndRaisesErrorThenFailed()
        {
            var ticket = _provider.BeginTicketPublic();
            _sequence.Clear();

            _provider.FailMatchPublic(ticket, "boom");

            Assert.IsNull(_provider.activeTicketPublic);
            Assert.AreEqual(new[] { "error:boom", "status:Failed" }, _sequence);
        }

        [Test]
        public void CancelLocally_ClearsTicket_AndRaisesCancelled()
        {
            var ticket = _provider.BeginTicketPublic();
            _sequence.Clear();

            _provider.CancelLocallyPublic(ticket);

            Assert.IsNull(_provider.activeTicketPublic);
            Assert.AreEqual(new[] { "status:Cancelled" }, _sequence);
        }
    }
}
