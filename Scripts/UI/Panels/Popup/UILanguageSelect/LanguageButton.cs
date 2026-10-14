using UnityEngine;
using OneText.UGUI;

namespace PixelChroma.UI
{
    public class LanguageButton : MonoBehaviour
    {
        [SerializeField] private UIFocusButton focusButton;
        [SerializeField] private OneTextLabel label;

        public UIFocusButton FocusButton => focusButton;

        public void Setup(string text, bool isSelected)
        {
            label.text = text;
            focusButton.SetSelected(isSelected);
        }

        public void SetSelected(bool isSelected)
        {
            focusButton.SetSelected(isSelected);
        }
    }
}
