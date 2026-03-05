using System;
using UnityEngine;

namespace PurrLobby
{
    [CreateAssetMenu(menuName = "PurrNet/Lobby/Providers/PurrNet/Game Starter")]
    public sealed class PurrGameStarter : GameStarterProvider
    {
        public override void Initialize(MenuOrchestrator menuOrchestrator) { }

        public override void StartGame(ILobby lobby, Action<GameStartResponse> onComplete)
        {
            onComplete?.Invoke(
                GameStartResponse.Failure("PurrNet does not support starting games. Please use a custom game starter implementation."));
        }
    }
}
