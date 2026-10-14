using System.Collections.Generic;
using OneText.UGUI;
using UnityEngine;

namespace PixelChroma.UI
{
    public class UILoading : UIPopup
    {
        public override bool CloseOnEscape => false;

        [Header("Tooltip")]
        [SerializeField] private LoadingTooltipTextDataSO textData;
        [SerializeField] private OneTextLabel tooltipLabel;

        private List<int> _indices;
        private int _cursor;

        private void EnsureIndices()
        {
            if (_indices != null) return;

            _indices = new List<int>();
            for (int i = 0; i < textData.tooltips.Length; i++)
                _indices.Add(i);
            Shuffle(_indices);
            _cursor = 0;
        }

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();

            if (textData == null || textData.tooltips == null || textData.tooltips.Length == 0)
                return;
            if (tooltipLabel == null)
                return;

            EnsureIndices();
            tooltipLabel.text = textData.tooltips[_indices[_cursor]].GetLocalizedString();
        }

        public override void OnAfterHide()
        {
            base.OnAfterHide();

            if (_indices == null) return;

            _cursor++;
            if (_cursor >= _indices.Count)
            {
                _cursor = 0;
                Shuffle(_indices);
            }
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}