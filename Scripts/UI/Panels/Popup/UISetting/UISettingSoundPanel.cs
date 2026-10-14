using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    // 사운드 탭. 마스터/BGM/효과음 3채널 볼륨을 SoundManager에 반영한다.
    public class UISettingSoundPanel : MonoBehaviour
    {
        // 슬라이더/입력은 0~100 정수로 다루고, SoundManager에는 0~1로 변환해 넘긴다.
        private const float MinVolume = 0f;
        private const float MaxVolume = 100f;

        [Header("Master")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private OneTextInputField masterInput;

        [Header("BGM")]
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private OneTextInputField bgmInput;

        [Header("SFX")]
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private OneTextInputField sfxInput;

        public void Initialize()
        {
            SetupChannel(masterSlider, masterInput, AudioChannel.Master);
            SetupChannel(bgmSlider, bgmInput, AudioChannel.Bgm);
            SetupChannel(sfxSlider, sfxInput, AudioChannel.Sfx);
        }

        // 팝업이 열릴 때 현재 볼륨으로 슬라이더/입력을 되돌린다.
        public void Refresh()
        {
            RefreshChannel(masterSlider, masterInput, AudioChannel.Master);
            RefreshChannel(bgmSlider, bgmInput, AudioChannel.Bgm);
            RefreshChannel(sfxSlider, sfxInput, AudioChannel.Sfx);
        }

        private void SetupChannel(Slider slider, OneTextInputField input, AudioChannel channel)
        {
            if (slider != null)
            {
                slider.minValue = MinVolume;
                slider.maxValue = MaxVolume;
                slider.wholeNumbers = true;
                slider.onValueChanged.AddListener(value => OnSliderChanged(input, channel, value));
            }

            if (input != null)
            {
                //input.contentType = OneTextInputField.ContentType.IntegerNumber;
                input.onEndEdit.AddListener(text => OnInputEndEdit(slider, input, channel, text));
            }
        }

        // 슬라이더 조작: SoundManager에 반영하고 입력창 텍스트를 맞춘다.
        private void OnSliderChanged(OneTextInputField input, AudioChannel channel, float value)
        {
            ApplyVolume(channel, value);
            SetInputText(input, value);
        }

        // 직접 입력: 숫자면 clamp 후 반영, 아니면 이전 값으로 되돌린다(재표시).
        private void OnInputEndEdit(Slider slider, OneTextInputField input, AudioChannel channel, string text)
        {
            if (int.TryParse(text, out int parsed))
                SetVolume(slider, input, channel, parsed);
            else
                RefreshChannel(slider, input, channel);
        }

        // 슬라이더와 InputField가 같은 clamp 규칙을 공유하도록 값 설정을 한곳에 모은다.
        private void SetVolume(Slider slider, OneTextInputField input, AudioChannel channel, float value)
        {
            value = Mathf.Clamp(Mathf.Round(value), MinVolume, MaxVolume);
            if (slider != null) slider.SetValueWithoutNotify(value);
            ApplyVolume(channel, value);
            SetInputText(input, value);
        }

        private void RefreshChannel(Slider slider, OneTextInputField input, AudioChannel channel)
        {
            float value = Mathf.Round(GetVolume01(channel) * 100f);
            if (slider != null) slider.SetValueWithoutNotify(value);
            SetInputText(input, value);
        }

        private static void ApplyVolume(AudioChannel channel, float value0to100)
        {
            SoundManager.Instance?.SetVolume(channel, value0to100 / 100f);
        }

        private static float GetVolume01(AudioChannel channel)
        {
            return SoundManager.Instance != null ? SoundManager.Instance.GetVolume(channel) : 1f;
        }

        // InputField 텍스트를 값으로 맞춘다(onEndEdit만 구독하므로 재입력 루프는 생기지 않는다).
        private static void SetInputText(OneTextInputField input, float value0to100)
        {
            if (input == null) return;

            input.text = Mathf.RoundToInt(value0to100).ToString();

            // 텍스트 오버플로우로 밀린 textComponent 스크롤 오프셋을 원위치시킨다.
            if (input.textComponent != null)
            {
                var rt = input.textComponent.rectTransform;
                rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
            }
        }
    }
}
