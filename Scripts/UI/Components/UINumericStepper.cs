using System;
using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    // [-] [InputField] [+] 형태의 숫자 입력 UI를 공통으로 관리한다.
    public class UINumericStepper : MonoBehaviour
    {
        [Header("Controls")]
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;
        [SerializeField] private OneTextInputField input;

        [Header("Value")]
        [SerializeField] private float minValue;
        [SerializeField] private float maxValue = 100f;
        [SerializeField] private float step = 1f;
        [SerializeField] private string format = "0";

        public event Action<float> OnValueChanged;

        private float _value;
        private bool _initialized;
        private bool _interactable = true;

        public float Value => _value;

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            if (minusButton != null) minusButton.onClick.AddListener(OnClickMinus);
            if (plusButton != null) plusButton.onClick.AddListener(OnClickPlus);
            if (input != null) input.onEndEdit.AddListener(OnInputEndEdit);

            RefreshInputCharacterLimit();
            SetValue(_value, false);
        }

        public void Configure(float min, float max, float valueStep = 1f, string valueFormat = "0")
        {
            minValue = min;
            maxValue = max;
            step = valueStep > 0f ? valueStep : 1f;
            format = string.IsNullOrEmpty(valueFormat) ? "0" : valueFormat;

            RefreshInputCharacterLimit();
            SetValue(_value, false);
        }

        public void SetRange(float min, float max)
        {
            minValue = min;
            maxValue = max;
            RefreshInputCharacterLimit();
            SetValue(_value, false);
        }

        public void SetValue(float value, bool notify = false)
        {
            float clamped = Clamp(value);
            if (Mathf.Approximately(_value, clamped))
            {
                RefreshUI();
                return;
            }

            _value = clamped;
            RefreshUI();

            if (notify)
                OnValueChanged?.Invoke(_value);
        }

        public void SetInteractable(bool interactable)
        {
            _interactable = interactable;
            RefreshUI();
        }

        private void OnClickMinus()
        {
            SetValue(_value - step, true);
        }

        private void OnClickPlus()
        {
            SetValue(_value + step, true);
        }

        private void OnInputEndEdit(string text)
        {
            if (float.TryParse(text, out float value))
            {
                SetValue(value, true);
                return;
            }

            RefreshUI();
        }

        private float Clamp(float value)
        {
            float min = Mathf.Min(minValue, maxValue);
            float max = Mathf.Max(minValue, maxValue);

            return Mathf.Clamp(Mathf.Round(value), min, max);
        }

        private void RefreshInputCharacterLimit()
        {
            if (input == null) return;

            float maxMagnitude = Mathf.Max(Mathf.Abs(minValue), Mathf.Abs(maxValue));
            int digitCount = Mathf.Max(1, Mathf.FloorToInt(maxMagnitude).ToString().Length);
            bool allowsNegative = minValue < 0f || maxValue < 0f;

            input.characterLimit = digitCount + (allowsNegative ? 1 : 0);
        }

        private void RefreshUI()
        {
            if (minusButton != null) minusButton.interactable = _interactable && _value > Mathf.Min(minValue, maxValue);
            if (plusButton != null) plusButton.interactable = _interactable && _value < Mathf.Max(minValue, maxValue);
            if (input != null) input.interactable = _interactable;

            if (input == null) return;

            input.text = _value.ToString(format);

            if (input.textComponent != null)
            {
                var rt = input.textComponent.rectTransform;
                rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
            }
        }

        private void OnDestroy()
        {
            if (minusButton != null) minusButton.onClick.RemoveListener(OnClickMinus);
            if (plusButton != null) plusButton.onClick.RemoveListener(OnClickPlus);
            if (input != null) input.onEndEdit.RemoveListener(OnInputEndEdit);
        }
    }
}
