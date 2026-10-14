using System;
using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    // 외곽선 엠보스(하이라이트/쉐도우) 설정을 담당한다. GameSettingPanel 하위에 배치된다.
    // RoomOption의 소유권은 UIRoomSetting에 있고, 여기서는 Bind로 받은 참조에 값만 반영한다.
    // 실제 라운드 적용은 PlayerPaintCanvas가 CurrentRoomOption에서 값을 읽어 처리한다.
    //
    // 내부 저장값(RoomOption.edgeHighlight/Shadow)은 0~1(풀 강도)이지만, 슬라이더/입력창은
    // 정수 표기 0~50 스케일로 다룬다. 표기만 0~50이고 효과 세기는 0~1 풀 강도를 유지한다.
    public class UIEmbossSetting : MonoBehaviour
    {
        // 0~1 저장값 ↔ 0~50 UI 표기 변환 계수. (슬라이더 50 = 내부 1.0 = 풀 강도)
        private const float PercentScale = 50f;

        [Header("Highlight")]
        [SerializeField] private Slider highlightSlider;
        [SerializeField] private OneTextInputField highlightInput;

        [Header("Shadow")]
        [SerializeField] private Slider shadowSlider;
        [SerializeField] private OneTextInputField shadowInput;

        [Header("Preview")]
        [SerializeField] private UIEmbossPreview preview;   // 선택: 실시간 엠보스 프리뷰
        [SerializeField] private UIEmbossColorSelector embossColorSelector;

        // 미리보기 UI가 구독해 실시간으로 PlayerPaintCanvas.ApplyEmboss를 호출할 수 있다.
        // (highlight, shadow) 두 값을 0~1로 전달한다.
        public event Action<float, float> OnEmbossChanged;

        private RoomOption _option;
        private bool _initialized;

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            if (highlightInput != null) highlightInput.onEndEdit.AddListener(OnHighlightInput);
            if (shadowInput != null) shadowInput.onEndEdit.AddListener(OnShadowInput);

            // 슬라이더는 0~50 정수 스케일. 저장 시 PercentToValue로 0~1(풀 강도)로 되돌린다.
            SetupSlider(highlightSlider, ToPercent(RoomOption.MinEmboss), ToPercent(RoomOption.MaxEmboss), OnHighlightChanged);
            SetupSlider(shadowSlider, ToPercent(RoomOption.MinEmboss), ToPercent(RoomOption.MaxEmboss), OnShadowChanged);

            // 프리뷰가 있으면 초기화 후 값 변경을 구독시킨다. (Bind 시 RaiseChanged로 첫 반영)
            if (preview != null)
            {
                preview.Initialize();
                OnEmbossChanged += preview.SetEmboss;
            }

            if (embossColorSelector != null)
            {
                embossColorSelector.Bind(preview);
                embossColorSelector.Initialize();
            }
        }

        public void Bind(RoomOption option)
        {
            _option = option;
            RefreshAll();
        }

        // 슬라이더와 InputField가 같은 clamp 규칙을 공유하도록 값 설정을 한곳에 모은다.
        // percent 입력(0~50)을 받아 0~1로 저장한다.
        private void SetHighlight(float percent)
        {
            _option.edgeHighlight = PercentToValue(percent);
            if (highlightSlider != null) highlightSlider.SetValueWithoutNotify(ToPercent(_option.edgeHighlight));
            RefreshValueUI();
            RaiseChanged();
        }

        private void SetShadow(float percent)
        {
            _option.edgeShadow = PercentToValue(percent);
            if (shadowSlider != null) shadowSlider.SetValueWithoutNotify(ToPercent(_option.edgeShadow));
            RefreshValueUI();
            RaiseChanged();
        }

        // 슬라이더는 이미 0~50 범위로 clamp되어 있으므로 PercentToValue로 0~1 저장한다.
        private void OnHighlightChanged(float percent)
        {
            if (_option == null) return;
            _option.edgeHighlight = PercentToValue(percent);
            RefreshValueUI();
            RaiseChanged();
        }

        private void OnShadowChanged(float percent)
        {
            if (_option == null) return;
            _option.edgeShadow = PercentToValue(percent);
            RefreshValueUI();
            RaiseChanged();
        }

        // InputField 직접 입력 처리: 숫자면 clamp 후 반영, 아니면 이전 값으로 되돌린다(재표시).
        private void OnHighlightInput(string text)
        {
            if (_option == null) return;
            if (float.TryParse(text, out float percent)) SetHighlight(percent);
            else RefreshValueUI();
        }

        private void OnShadowInput(string text)
        {
            if (_option == null) return;
            if (float.TryParse(text, out float percent)) SetShadow(percent);
            else RefreshValueUI();
        }

        private void RefreshAll()
        {
            if (_option == null) return;

            RefreshValueUI();

            if (highlightSlider != null) highlightSlider.SetValueWithoutNotify(ToPercent(_option.edgeHighlight));
            if (shadowSlider != null) shadowSlider.SetValueWithoutNotify(ToPercent(_option.edgeShadow));

            RaiseChanged();
        }

        private void RefreshValueUI()
        {
            if (highlightInput != null) ForceSetText(highlightInput, ToPercent(_option.edgeHighlight).ToString("0"));
            if (shadowInput != null) ForceSetText(shadowInput, ToPercent(_option.edgeShadow).ToString("0"));
        }

        private void RaiseChanged()
        {
            if (_option == null) return;
            OnEmbossChanged?.Invoke(_option.edgeHighlight, _option.edgeShadow);
        }

        // 0~1 저장값을 0~50 표기로.
        private static float ToPercent(float value01)
        {
            return value01 * PercentScale;
        }

        // 0~50 입력을 0~1로 변환 후 clamp.
        private static float PercentToValue(float percent)
        {
            float value01 = percent / PercentScale;
            return Mathf.Clamp(value01, RoomOption.MinEmboss, RoomOption.MaxEmboss);
        }

        private static void ForceSetText(OneTextInputField input, string value)
        {
            input.text = value;

            // 텍스트 오버플로우로 밀린 textComponent의 스크롤 오프셋을 원위치시킨다.
            if (input.textComponent != null)
            {
                var rt = input.textComponent.rectTransform;
                rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
            }
        }

        private static void SetupSlider(Slider slider, float min, float max, UnityEngine.Events.UnityAction<float> onChanged)
        {
            if (slider == null) return;

            slider.minValue = min;
            slider.maxValue = max;
            slider.wholeNumbers = true;   // 표기는 정수 %(다른 GameSetting 슬라이더와 동일)
            slider.onValueChanged.AddListener(onChanged);
        }
    }
}
