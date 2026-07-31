using System;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class LobbyInfoEntry : MonoBehaviour
    {
        [SerializeField] private TMPro.TMP_Text _name;
        [SerializeField] private TMPro.TMP_Text _users;
        [SerializeField] private TMPro.TMP_Text _maxUsers;

        private LobbyInfo _lobbyInfo;
        private Action<LobbyInfo> _onClicked;

        public void Setup(LobbyInfo info, Action<LobbyInfo> onClicked)
        {
            _lobbyInfo = info;
            _onClicked = onClicked;
            _name.text = info.name;
            _users.text = info.playerCount.ToString();
            _maxUsers.text = info.maxPlayers.ToString();
        }

        public void Enter()
        {
            _onClicked?.Invoke(_lobbyInfo);
        }
    }
}
