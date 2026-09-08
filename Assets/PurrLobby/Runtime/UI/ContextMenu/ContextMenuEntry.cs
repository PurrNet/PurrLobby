using System;
using TMPro;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class ContextMenuEntry : MonoBehaviour
    {
        [SerializeField] private TMP_Text _icon;
        [SerializeField] private TMP_Text _name;
        [SerializeField] private TMP_Text _description;
        [SerializeField] private ColorInfo _normalColor = new() { enabled = true, color = ColorType.Surface, contrast = true };
        [SerializeField] private ColorInfo _dangerousColor = new() { enabled = true, color = ColorType.Danger };

        private Action<int> _onOptionSelected;
        private int _index;

        public void Setup(int index, ContextOption option, Action<int> onClicked)
        {
            _onOptionSelected = onClicked;
            _index = index;

            _icon.text = string.IsNullOrWhiteSpace(option.icon) ? "" : $"<icon={option.icon}>";
            _name.text = option.name;
            _description.text = option.description;

            var color = option.isDangerous ? _dangerousColor : _normalColor;
            ThemeColors.Set(_icon, color);
            ThemeColors.Set(_name, color);
        }

        public void Select()
        {
            _onOptionSelected?.Invoke(_index);
        }
    }
}
