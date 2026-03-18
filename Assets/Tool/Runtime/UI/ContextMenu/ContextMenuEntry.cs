using System;
using TMPro;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class ContextMenuEntry : MonoBehaviour
    {
        [SerializeField] private TMP_Text _icon;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private Color _normalColor;
        [SerializeField] private Color _dangerousColor;

        private Action<int> _onOptionSelected;
        private int _index;

        public void Setup(int index, ContextOption option, Action<int> onClicked)
        {
            _onOptionSelected  = onClicked;
            _index = index;

            _icon.text = string.IsNullOrWhiteSpace(option.icon) ? "" : $"<icon={option.icon}>";
            _name.text = option.name;
            _description.text = option.description;

            if (option.isDangerous)
            {
                _icon.color = _dangerousColor;
                _name.color = _dangerousColor;
            }
            else
            {
                _icon.color = _normalColor;
                _name.color = _normalColor;
            }
        }

        public void Select()
        {
            _onOptionSelected?.Invoke(_index);
        }
    }
}
