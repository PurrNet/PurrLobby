using System;
using System.Collections;
using System.Collections.Generic;
using PurrNet.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PurrNet.Lobby
{
    public class ContextMenuView : MonoView
    {
        [SerializeField] private ContextCategoryEntry _categoryEntry;
        [SerializeField] private ContextMenuEntry _entry;
        [SerializeField] private RectTransform _contentParent;
        [SerializeField] private GetPrefferedHeightFromScrollView _height;
        [Space]
        [SerializeField] private RectTransform _panel;
        [SerializeField] private TMP_Text _title;

        private UIPool<ContextMenuEntry> _optionEntries;
        private UIPool<ContextCategoryEntry> _categoryEntries;
        private Action<int> _onOptionSelected;

        protected override void OnBecomeBackground()
        {
            PopMe();
        }

        public void Setup(Vector2 screenPos, string title, IReadOnlyList<ContextOption> options, Action<int> onOptionSelected)
        {
            _onOptionSelected = onOptionSelected;
            _optionEntries ??= new UIPool<ContextMenuEntry>(_entry, _contentParent);
            _categoryEntries ??= new UIPool<ContextCategoryEntry>(_categoryEntry, _contentParent);

            _title.text = title;

            _optionEntries.ResetCounter();
            _categoryEntries.ResetCounter();

            string _currentCategory = null;

            for (int i = 0; i < options.Count; i++)
            {
                var option = options[i];

                if (_currentCategory != option.category)
                {
                    _currentCategory = option.category;
                    if (!string.IsNullOrWhiteSpace(option.category))
                    {
                        var category = _categoryEntries.GetInstance();
                        category.Setup(option.category);
                        category.transform.SetAsLastSibling();
                    }
                }

                var entry = _optionEntries.GetInstance();
                entry.Setup(i, option, OnClicked);
                entry.transform.SetAsLastSibling();
            }

            _optionEntries.DiscardRest();
            _categoryEntries.DiscardRest();

            PositionPanel(screenPos);
        }

        private void OnClicked(int index)
        {
            _onOptionSelected?.Invoke(index);
            PopMe();
        }

        private void PositionPanel(Vector2 screenPos)
        {
            var canvasRect = canvas.transform as RectTransform;

            if (!canvasRect)
                return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                screenPos,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                out var localPoint
            );

            _panel.localPosition = localPoint;

            LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);
            _height.ForceRebuild();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_panel);

            var panelRect = _panel.rect;
            var canvasSize = canvasRect.rect.size;

            float minX = -canvasSize.x / 2f;
            float maxX =  canvasSize.x / 2f - panelRect.width;
            float minY = -canvasSize.y / 2f + panelRect.height;
            float maxY =  canvasSize.y / 2f;

            _panel.localPosition = new Vector3(
                Mathf.Clamp(localPoint.x, minX, maxX),
                Mathf.Clamp(localPoint.y, minY, maxY),
                0
            );
        }

        protected override IEnumerator OnEnterTransition()
        {
            return ViewTransitions.FadeIn(this);
        }

        protected override IEnumerator OnExitTransition()
        {
            return ViewTransitions.FadeOut(this);
        }
    }
}
