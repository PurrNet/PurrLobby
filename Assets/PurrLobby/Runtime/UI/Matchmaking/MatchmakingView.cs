using System;
using System.Globalization;
using System.Threading.Tasks;
using PurrNet.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace PurrNet.Lobby
{
    public class MatchmakingView : MonoView
    {
        [SerializeField] private TMPro.TMP_Text _message;
        [Tooltip("Optional ready-check buttons. The default view creates matching buttons from its Cancel button.")]
        [SerializeField] private ButtonElement _acceptButton;
        [SerializeField] private ButtonElement _declineButton;

        private GameOrchestrator _orchestrator;
        private MatchmakingProvider _provider;
        private MatchmakingTicket? _currentTicket;
        private MatchmakingStatus _status = MatchmakingStatus.Queued;
        private MatchmakingReadyCheck? _readyCheck;
        private ButtonElement _cancelButton;
        private bool _cancelling, _responding, _failed, _closed, _terminal, _handoff, _unsubscribed, _buttonsBound;
        private int _generation;
        private string _cancelTicketId;
        private Task<APIResponse> _cancelTask;

        public void Setup(GameOrchestrator orchestrator, MatchmakingRequest request)
        {
            if (_provider && !_closed && !_handoff) CloseWork();
            Unsubscribe();
            _generation++;
            _currentTicket = null;
            _readyCheck = null;
            _status = MatchmakingStatus.Queued;
            _cancelling = _responding = _failed = _closed = _terminal = _handoff = false;
            _unsubscribed = false;
            _cancelTask = null;
            _cancelTicketId = null;
            _orchestrator = orchestrator;
            _provider = orchestrator ? orchestrator.matchmakingProvider : null;
            EnsureReadyButtons();
            UpdateReadyButtons();
            SetMessage($"Matchmaking for {request.queueId ?? request.gameMode}: Queued");
            if (!_provider)
            {
                _terminal = true;
                Toaster.Push("No matchmaking provider", "This feature is not available", true);
                CloseView();
                return;
            }
            _provider.onStatusChanged += OnStatusChanged;
            _provider.onMatchFound += OnMatchFound;
            _provider.onMatchmakingError += OnMatchmakingError;
            _provider.onReadyCheckChanged += OnReadyCheckChanged;
            StartMatchmakingAsync(_provider, request, _generation).Forget("[MatchmakingView] StartMatchmaking failed");
        }

        private async Task StartMatchmakingAsync(MatchmakingProvider provider, MatchmakingRequest request, int generation)
        {
            MatchmakingTicketResponse response;
            try { response = await provider.StartMatchmaking(request); }
            catch (Exception e)
            {
                if (this && !_closed && generation == _generation) ShowFailure(e.Message);
                return;
            }
            // Closing cannot orphan a ticket whose HTTP create was still pending.
            // Cleanup must finish even after the view has been destroyed.
            if (!this || _closed || generation != _generation)
            {
                if (response.success && (generation != _generation || !_handoff) && provider)
                    await CleanupTicket(provider, response.ticket);
                return;
            }
            if (!response.success) { ShowFailure(response.error); return; }
            if (_handoff || _terminal) return;
            _currentTicket = response.ticket;
            RefreshMessage();
        }

        public override void OnPopped() => CloseWork();

        private void OnDestroy()
        {
            CloseWork();
            if (_buttonsBound)
            {
                if (_acceptButton) _acceptButton.onClick -= Accept;
                if (_declineButton) _declineButton.onClick -= Decline;
            }
        }

        private void CloseWork()
        {
            if (_closed) return;
            _closed = true;
            UpdateReadyButtons();
            Unsubscribe();
            if (!_handoff && !_terminal && _currentTicket.HasValue && _provider)
                CleanupTicket(_provider, _currentTicket.Value).Forget("[MatchmakingView] Ticket cleanup failed");
        }

        private void Unsubscribe()
        {
            if (_unsubscribed || !_provider) return;
            _unsubscribed = true;
            _provider.onStatusChanged -= OnStatusChanged;
            _provider.onMatchFound -= OnMatchFound;
            _provider.onMatchmakingError -= OnMatchmakingError;
            _provider.onReadyCheckChanged -= OnReadyCheckChanged;
        }

        private bool AcceptsEvent(MatchmakingTicket ticket) => !_closed && !_handoff && !_terminal
            && (!_currentTicket.HasValue || _currentTicket.Value.ticketId == ticket.ticketId);

        private void OnMatchFound(MatchmakingTicket ticket, MatchResult result)
        {
            if (!AcceptsEvent(ticket)) return;
            _currentTicket = ticket;
            _handoff = true; // Before replacing the view: OnPopped must not cancel the match.
            _readyCheck = null;
            UpdateReadyButtons();
            if (result.lobby != null)
            {
                parentStack.ReplaceOrPush<LobbyView>(this).Setup(result.lobby, _orchestrator);
                return;
            }
            StartGameFromMatch(result).Forget("[MatchmakingView] StartGameFromMatch failed");
        }

        private async Task StartGameFromMatch(MatchResult result)
        {
            var orchestrator = _orchestrator;
            var provider = _provider;
            var ticket = _currentTicket;
            var allocator = orchestrator.gameAllocator;
            var launchScene = SceneManager.GetActiveScene();
            orchestrator.activeLobby = null;
            LoadingView loadingView = null;
            GameSession gameSession = null;
            try
            {
                loadingView = parentStack.ReplaceOrPush<LoadingView>(this);
                if (!allocator) throw new InvalidOperationException("No game allocator is configured.");
                if (loadingView) loadingView.Setup("Allocating game...");
                var response = await allocator.AllocateGame(result);
                if (!response.success) throw new Exception(response.error);
                if (loadingView) loadingView.Setup("Loading game...");
                await allocator.LoadGame(result);
                if (GameSession.TryGet(SceneManager.GetActiveScene(), out gameSession) && gameSession.isExiting)
                    throw new OperationCanceledException("The game was closed before admission completed.");
                await allocator.ConnectMatch(result, response.connection);
            }
            catch (Exception e)
            {
                var currentScene = SceneManager.GetActiveScene();
                GameSession.TryGet(currentScene, out var session);
                // The managed session still records an intentional exit even
                // after its Unity object has been destroyed by the scene change.
                // Its exit handler has already fenced this admission attempt.
                bool alreadyExiting = gameSession is { isExiting: true };
                if (!alreadyExiting)
                {
                    Toaster.PushError("Failed to start game", e);
                    // Fence admission and stop networking before awaiting remote
                    // cleanup, which may be slow or unavailable.
                    if (session && currentScene != launchScene)
                    {
                        session.ConfigurePlayerSession();
                        session.LeaveToMenu();
                    }
                    if (allocator) await allocator.Logout();
                    if (!session && currentScene != launchScene)
                    {
                        var menu = string.IsNullOrEmpty(orchestrator.menuScene) ? launchScene.name : orchestrator.menuScene;
                        if (!string.IsNullOrEmpty(menu)) _ = SceneManager.LoadSceneAsync(menu);
                    }
                }
                // Only the elected host's allocating match can still be
                // cancelled; active outcomes belong to the trusted server.
                if (result.requiresHostReady && provider && ticket.HasValue)
                    await CleanupTicket(provider, ticket.Value);
            }
            finally { if (loadingView) loadingView.PopMe(); }
        }

        public void Cancel()
        {
            if (_cancelling || _closed || _handoff || _terminal) return;
            if (!_currentTicket.HasValue) { CloseView(); return; }
            _cancelling = true;
            UpdateReadyButtons();
            CancelAsync(_provider, _currentTicket.Value, _generation).Forget("[MatchmakingView] CancelMatchmaking failed");
        }

        private Task<APIResponse> CancelOnce(MatchmakingProvider provider, MatchmakingTicket ticket)
        {
            if (_cancelTask != null && _cancelTicketId == ticket.ticketId && !_cancelTask.IsCompleted) return _cancelTask;
            if (_cancelTask != null && _cancelTicketId == ticket.ticketId && _cancelTask.Status == TaskStatus.RanToCompletion && _cancelTask.Result.success) return _cancelTask;
            _cancelTicketId = ticket.ticketId;
            // Publish the task before a synchronous provider can raise Cancelled
            // and reenter OnPopped.
            var completion = new TaskCompletionSource<APIResponse>();
            _cancelTask = completion.Task;
            FinishCancellation(provider, ticket, completion);
            return completion.Task;
        }

        private static async void FinishCancellation(MatchmakingProvider provider, MatchmakingTicket ticket, TaskCompletionSource<APIResponse> completion)
        {
            try { completion.TrySetResult(await provider.CancelMatchmaking(ticket)); }
            catch (Exception e) { completion.TrySetResult(APIResponse.Failure(e.Message)); }
        }

        private async Task CleanupTicket(MatchmakingProvider provider, MatchmakingTicket ticket)
        {
            var response = await CancelOnce(provider, ticket);
            if (!response.success && provider)
                Debug.LogWarning("[MatchmakingView] Ticket cleanup was not acknowledged; server expiry still applies.", provider);
        }

        private async Task CancelAsync(MatchmakingProvider provider, MatchmakingTicket ticket, int generation)
        {
            var response = await CancelOnce(provider, ticket);
            if (!this || _closed || _handoff || generation != _generation) return;
            _cancelling = false;
            if (!response.success) { Toaster.Push("Failed to cancel", response.error, true); UpdateReadyButtons(); }
            else { _terminal = true; CloseView(); }
        }

        public void Accept() => Respond(true);
        public void Decline() => Respond(false);

        private void Respond(bool accept)
        {
            if (_closed || _handoff || _terminal || _responding || _cancelling || !_currentTicket.HasValue || !_readyCheck.HasValue) return;
            if (accept && _readyCheck.Value.accepted) return;
            _responding = true;
            UpdateReadyButtons();
            RespondAsync(_provider, _currentTicket.Value, _readyCheck.Value.matchId, accept, _generation)
                .Forget("[MatchmakingView] Ready response failed");
        }

        private async Task RespondAsync(MatchmakingProvider provider, MatchmakingTicket ticket, string matchId, bool accept, int generation)
        {
            APIResponse response;
            try { response = await provider.RespondToReadyCheck(ticket, accept); }
            catch (Exception e) { response = APIResponse.Failure(e.Message); }
            if (!this || _closed || _handoff || _terminal || generation != _generation) return;
            _responding = false;
            if (!response.success) Toaster.Push("Ready check", response.error, true);
            else if (!accept) { _terminal = true; CloseView(); return; }
            else if (_readyCheck.HasValue && _readyCheck.Value.matchId == matchId)
            {
                var ready = _readyCheck.Value;
                ready.accepted = true;
                _readyCheck = ready;
            }
            UpdateReadyButtons();
            RefreshMessage();
        }

        private void OnReadyCheckChanged(MatchmakingTicket ticket, MatchmakingReadyCheck readyCheck)
        {
            if (!AcceptsEvent(ticket)) return;
            _currentTicket = ticket;
            _status = MatchmakingStatus.ReadyCheck;
            _readyCheck = readyCheck;
            UpdateReadyButtons();
            RefreshMessage();
        }

        private void OnStatusChanged(MatchmakingTicket ticket, MatchmakingStatus status)
        {
            if (!AcceptsEvent(ticket)) return;
            _currentTicket = ticket; // Events can arrive before StartMatchmaking returns.
            _status = status;
            if (status != MatchmakingStatus.ReadyCheck) _readyCheck = null;
            UpdateReadyButtons();
            RefreshMessage();
            if (status == MatchmakingStatus.Failed) ShowFailure("The matchmaker reported a failure.");
            else if (status == MatchmakingStatus.Cancelled) { _terminal = true; CloseView(); }
        }

        private void OnMatchmakingError(MatchmakingTicket ticket, string error)
        {
            if (AcceptsEvent(ticket)) ShowFailure(error);
        }

        private void ShowFailure(string error)
        {
            if (_failed || _closed || _handoff) return;
            _failed = _terminal = true;
            Toaster.Push("Matchmaking failed", string.IsNullOrEmpty(error) ? "Unknown error." : error, true);
            CloseView();
        }

        private void CloseView()
        {
            if (parentStack) PopMe();
            else CloseWork();
        }

        private void Update()
        {
            if (_readyCheck.HasValue && !_closed && !_handoff) RefreshMessage();
        }

        private void RefreshMessage()
        {
            if (_readyCheck.HasValue)
            {
                var ready = _readyCheck.Value;
                var remaining = string.Empty;
                if (DateTimeOffset.TryParse(ready.deadline, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var deadline))
                    remaining = $" · {Math.Max(0, (int)Math.Ceiling((deadline - DateTimeOffset.UtcNow).TotalSeconds))}s";
                SetMessage($"Match found · {ready.acceptedPlayers}/{ready.totalPlayers} ready{remaining}\n"
                    + (ready.accepted ? "Accepted — waiting for the other players." : "Accept to join this match, or decline."));
                return;
            }
            var message = $"Matchmaking: {_status}";
            if (_status == MatchmakingStatus.Assembling && _currentTicket.HasValue && !string.IsNullOrEmpty(_currentTicket.Value.inviteCode))
                message += $"\nShare this party invitation:\n{_currentTicket.Value.ticketId}\n{_currentTicket.Value.inviteCode}";
            SetMessage(message);
        }

        private void SetMessage(string value) { if (_message) _message.text = value; }

        private void EnsureReadyButtons()
        {
            if (_buttonsBound) return;
            foreach (var button in GetComponentsInChildren<ButtonElement>(true))
            {
                if (button == _acceptButton || button == _declineButton) continue;
                var clicks = button.onClickUnity;
                if (clicks == null) continue;
                for (int i = 0; i < clicks.GetPersistentEventCount(); i++)
                {
                    if (clicks.GetPersistentTarget(i) == this && clicks.GetPersistentMethodName(i) == nameof(Cancel))
                    { _cancelButton = button; break; }
                }
                if (_cancelButton) break;
            }
            if (!_acceptButton && _cancelButton) _acceptButton = CloneAction(_cancelButton, "Accept match", true);
            if (!_declineButton && _cancelButton) _declineButton = CloneAction(_cancelButton, "Decline match", false);
            if (!_acceptButton || !_declineButton)
            {
                Debug.LogError("[MatchmakingView] Assign ready-check buttons or retain the default Cancel button in this view.", this);
                return;
            }
            _acceptButton.onClick += Accept;
            _declineButton.onClick += Decline;
            _buttonsBound = true;
        }

        private static ButtonElement CloneAction(ButtonElement template, string label, bool accent)
        {
            var clone = Instantiate(template.gameObject, template.transform.parent);
            clone.name = label;
            clone.transform.SetSiblingIndex(template.transform.GetSiblingIndex());
            foreach (var button in clone.GetComponentsInChildren<ButtonElement>(true))
                button.onClickUnity = new UnityEvent(); // Remove cloned persistent Cancel callbacks, too.
            var action = clone.GetComponent<ButtonElement>();
            var text = clone.GetComponentInChildren<TMPro.TMP_Text>(true);
            if (text) text.text = label;
            if (accent) action.SetColors(new ColorInfo { enabled = true, color = ColorType.Accent }, new ColorInfo { enabled = true, color = ColorType.Accent });
            clone.SetActive(false);
            return action;
        }

        private void UpdateReadyButtons()
        {
            bool ready = _readyCheck.HasValue && !_closed && !_handoff && !_terminal;
            if (_acceptButton)
            {
                _acceptButton.gameObject.SetActive(ready);
                _acceptButton.interactable = ready && !_responding && !_cancelling && !_readyCheck.Value.accepted;
            }
            if (_declineButton)
            {
                _declineButton.gameObject.SetActive(ready);
                _declineButton.interactable = ready && !_responding && !_cancelling;
            }
            if (_cancelButton) _cancelButton.gameObject.SetActive(!ready && !_handoff);
        }
    }
}
