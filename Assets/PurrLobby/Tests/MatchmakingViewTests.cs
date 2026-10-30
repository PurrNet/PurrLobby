using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PurrNet.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PurrNet.Lobby.Tests
{
    public class MatchmakingViewTests
    {
        private MatchmakingView _view;
        private ViewMatchmakingProvider _provider;
        private ViewMatchmakingProvider _replacementProvider;
        private GameOrchestrator _orchestrator;

        [SetUp]
        public void SetUp()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/PurrLobby/Prefabs/Views/MatchmakingView.prefab");
            Assert.IsNotNull(prefab, "The shipped matchmaking view must support ready checks without prefab changes.");
            _view = Object.Instantiate(prefab).GetComponent<MatchmakingView>();
            _provider = ScriptableObject.CreateInstance<ViewMatchmakingProvider>();
            _orchestrator = ScriptableObject.CreateInstance<GameOrchestrator>();
            _orchestrator.matchmakingProvider = _provider;
        }

        [TearDown]
        public void TearDown()
        {
            if (_view) Object.DestroyImmediate(_view.gameObject);
            if (_replacementProvider) Object.DestroyImmediate(_replacementProvider);
            Object.DestroyImmediate(_orchestrator);
            Object.DestroyImmediate(_provider);
        }

        private ButtonElement Action(string name) => _view.GetComponentsInChildren<ButtonElement>(true)
            .Single(button => button.name == name);

        [Test]
        public void DefaultPrefab_CreatesExplicitReadyButtons_AndNeverAutoAccepts()
        {
            _view.Setup(_orchestrator, new MatchmakingRequest { queueId = "ranked" });
            var accept = Action("Accept match");
            var decline = Action("Decline match");
            Assert.IsFalse(accept.gameObject.activeSelf);
            Assert.IsFalse(decline.gameObject.activeSelf);

            _provider.Ready("match-one");

            Assert.IsTrue(accept.gameObject.activeSelf);
            Assert.IsTrue(decline.gameObject.activeSelf);
            Assert.IsEmpty(_provider.responses);
            Assert.AreEqual(0, accept.onClickUnity.GetPersistentEventCount(), "A cloned accept button must not retain Cancel.");
            Assert.AreEqual(0, decline.onClickUnity.GetPersistentEventCount());
            _view.Accept();
            _view.Accept();
            Assert.AreEqual(new[] { true }, _provider.responses);
            Assert.IsFalse(accept.interactable);
            Assert.IsTrue(decline.interactable);
            Assert.AreEqual(0, _provider.cancelCount);
        }

        [Test]
        public void Decline_RespondsToReadyCheck_AndClosesWithoutAnExtraCancellation()
        {
            _view.Setup(_orchestrator, new MatchmakingRequest());
            _provider.Ready("match-one");

            _view.Decline();
            _view.OnPopped();

            Assert.AreEqual(new[] { false }, _provider.responses);
            Assert.AreEqual(0, _provider.cancelCount);
            Assert.IsFalse(Action("Accept match").gameObject.activeSelf);
        }

        [Test]
        public void RequeuedAfterReadyTimeout_ClearsActionsUntilTheNextReadyCheck()
        {
            _view.Setup(_orchestrator, new MatchmakingRequest());
            _provider.Ready("timed-out");
            _provider.Searching();

            _view.Accept();
            _view.Decline();
            Assert.IsEmpty(_provider.responses);
            Assert.IsFalse(Action("Accept match").gameObject.activeSelf);
            Assert.IsFalse(Action("Decline match").gameObject.activeSelf);

            _provider.Ready("next-match");
            Assert.IsTrue(Action("Accept match").gameObject.activeSelf);
            _view.Accept();
            Assert.AreEqual(new[] { true }, _provider.responses);
        }

        [Test]
        public void PoppingDuringReadyCheck_CancelsOnce_AndUnsubscribes()
        {
            _view.Setup(_orchestrator, new MatchmakingRequest());
            _provider.Ready("match-one");

            _view.OnPopped();
            _provider.Ready("late-event");
            _view.OnPopped();

            Assert.AreEqual(1, _provider.cancelCount);
            Assert.IsFalse(Action("Accept match").gameObject.activeSelf);
            Assert.IsEmpty(_provider.responses);
        }

        [UnityTest]
        public IEnumerator PoppingBeforeStartAcknowledgement_CleansUpTheLateTicket()
        {
            _provider.pendingStart = true;
            _provider.announceBeforeAcknowledgement = false;
            _view.Setup(_orchestrator, new MatchmakingRequest());
            _view.OnPopped();
            Assert.AreEqual(0, _provider.cancelCount);

            _provider.AcknowledgeStart();
            yield return null;
            yield return null;

            Assert.AreEqual(1, _provider.cancelCount);
        }

        [UnityTest]
        public IEnumerator PendingStartWithEarlyStatus_CancelsOnlyOnceAcrossPopAndAcknowledgement()
        {
            _provider.pendingStart = true;
            _provider.announceBeforeAcknowledgement = true;
            _view.Setup(_orchestrator, new MatchmakingRequest());
            _view.OnPopped();
            Assert.AreEqual(1, _provider.cancelCount);

            _provider.AcknowledgeStart();
            yield return null;
            yield return null;

            Assert.AreEqual(1, _provider.cancelCount);
        }

        [UnityTest]
        public IEnumerator DestroyingBeforeStartAcknowledgement_StillCleansUpTheLateTicket()
        {
            _provider.pendingStart = true;
            _provider.announceBeforeAcknowledgement = false;
            _view.Setup(_orchestrator, new MatchmakingRequest());
            Object.DestroyImmediate(_view.gameObject);

            _provider.AcknowledgeStart();
            yield return null;
            yield return null;

            Assert.AreEqual(1, _provider.cancelCount);
        }

        [UnityTest]
        public IEnumerator OldReadyResponse_CannotCloseAReplacementSearch()
        {
            _provider.pendingReadyResponse = true;
            _view.Setup(_orchestrator, new MatchmakingRequest());
            _provider.Ready("old-match");
            _view.Decline();

            _replacementProvider = ScriptableObject.CreateInstance<ViewMatchmakingProvider>();
            _orchestrator.matchmakingProvider = _replacementProvider;
            _view.Setup(_orchestrator, new MatchmakingRequest());
            _replacementProvider.Ready("new-match");
            _provider.AcknowledgeReadyResponse();
            yield return null;
            yield return null;

            Assert.IsTrue(Action("Accept match").gameObject.activeSelf);
            Assert.AreEqual(0, _replacementProvider.cancelCount);
            _view.Accept();
            Assert.AreEqual(new[] { true }, _replacementProvider.responses);
        }
    }

    public class ViewMatchmakingProvider : MatchmakingProvider
    {
        public bool pendingStart, announceBeforeAcknowledgement, pendingReadyResponse;
        public int cancelCount;
        public readonly List<bool> responses = new();
        private TaskCompletionSource<MatchmakingTicketResponse> _start;
        private TaskCompletionSource<APIResponse> _readyResponse;
        private MatchmakingTicket _ticket;

        public override Task<MatchmakingTicketResponse> StartMatchmaking(MatchmakingRequest request)
        {
            if (!pendingStart || announceBeforeAcknowledgement) _ticket = BeginTicket("view-ticket");
            if (!pendingStart) return Task.FromResult(MatchmakingTicketResponse.Success(_ticket));
            _start = new TaskCompletionSource<MatchmakingTicketResponse>();
            return _start.Task;
        }

        public void AcknowledgeStart()
        {
            if (string.IsNullOrEmpty(_ticket.ticketId)) _ticket = BeginTicket("view-ticket");
            _start.SetResult(MatchmakingTicketResponse.Success(_ticket));
        }

        public override Task<APIResponse> CancelMatchmaking(MatchmakingTicket ticket)
        {
            cancelCount++;
            CancelLocally(ticket);
            return Task.FromResult(APIResponse.Success());
        }

        public override Task<APIResponse> RespondToReadyCheck(MatchmakingTicket ticket, bool accept)
        {
            responses.Add(accept);
            if (pendingReadyResponse)
            {
                _readyResponse = new TaskCompletionSource<APIResponse>();
                return _readyResponse.Task;
            }
            return Task.FromResult(APIResponse.Success());
        }

        public void AcknowledgeReadyResponse() => _readyResponse.SetResult(APIResponse.Success());

        public void Ready(string matchId) => RaiseReadyCheck(_ticket, new MatchmakingReadyCheck
        {
            matchId = matchId,
            deadline = DateTimeOffset.UtcNow.AddSeconds(30).ToString("O"),
            totalPlayers = 2,
            acceptedPlayers = 0,
            accepted = false
        });

        public void Searching() => RaiseStatusChanged(_ticket, MatchmakingStatus.Searching);
    }
}
