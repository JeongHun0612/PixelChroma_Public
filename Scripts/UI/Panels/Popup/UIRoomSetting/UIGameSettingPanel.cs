using System;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    // 시간 / 강제 노출 설정을 담당한다. (인원 수는 UIRoomInfoPanel로 이동)
    // RoomOption의 소유권은 UIRoomSetting에 있고, 여기서는 Bind로 받은 참조에 값만 반영한다.
    public class UIGameSettingPanel : MonoBehaviour
    {
        [Header("Scroll")]
        [SerializeField] private ScrollRect scrollRect;

        [Header("Hide Time")]
        [SerializeField] private UINumericStepper hideTimeStepper;

        [Header("Seek Time")]
        [SerializeField] private UINumericStepper seekTimeStepper;

        [Header("Check Time")]
        [SerializeField] private UINumericStepper checkTimeStepper;

        [Header("Forced Expose")]
        [SerializeField] private Toggle forcedExposeToggle;            // on/off 토글
        [SerializeField] private GameObject forcedExposeActivePanel;    // 실제 작동(슬라이더/입력) 패널
        [SerializeField] private GameObject forcedExposeDisabledPanel;  // 회색 비활성 표시용 패널
        [SerializeField] private UINumericStepper forcedExposeIntervalStepper;

        [Header("Emboss")]
        [SerializeField] private UIEmbossSetting embossSetting;         // 하이라이트/쉐도우 설정

        private RoomOption _option;
        private bool _initialized;

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            SetupStepper(hideTimeStepper, RoomOption.MinHideTime, RoomOption.MaxHideTime, OnHideTimeChanged, RoomOption.TimeStep);
            SetupStepper(seekTimeStepper, RoomOption.MinSeekTime, RoomOption.MaxSeekTime, OnSeekTimeChanged, RoomOption.TimeStep);
            SetupStepper(checkTimeStepper, RoomOption.MinCheckTime, RoomOption.MaxCheckTime, OnCheckTimeChanged, RoomOption.TimeStep);
            SetupStepper(forcedExposeIntervalStepper, RoomOption.MinForcedExposeInterval, RoomOption.GetMaxForcedExposeInterval(RoomOption.DefaultSeekTime), OnForcedExposeIntervalChanged, RoomOption.TimeStep);

            if (forcedExposeToggle != null) forcedExposeToggle.onValueChanged.AddListener(OnForcedExposeChanged);

            if (embossSetting != null) embossSetting.Initialize();
        }

        public void Bind(RoomOption option)
        {
            _option = option;
            RefreshAll();

            // 엠보스 설정은 같은 RoomOption 참조를 물려받아 자기 필드만 갱신한다.
            if (embossSetting != null) embossSetting.Bind(option);
        }

        public void ResetScroll()
        {
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 1f;
        }

        // 버튼(+/-)과 InputField 직접 입력이 동일한 clamp 규칙을 공유하도록 값 설정을 한곳에 모은다.
        private void SetHideTime(float value)
        {
            _option.hideTime = Mathf.Clamp(Mathf.Round(value), RoomOption.MinHideTime, RoomOption.MaxHideTime);
            RefreshTimeUI();
        }

        private void SetSeekTime(float value)
        {
            _option.seekTime = Mathf.Clamp(Mathf.Round(value), RoomOption.MinSeekTime, RoomOption.MaxSeekTime);
            ClampForcedExposeInterval();
            RefreshTimeUI();
        }

        private void SetCheckTime(float value)
        {
            _option.checkTime = Mathf.Clamp(Mathf.Round(value), RoomOption.MinCheckTime, RoomOption.MaxCheckTime);
            RefreshTimeUI();
        }

        private void SetForcedExposeInterval(float value)
        {
            _option.forcedExposeInterval = Mathf.Clamp(
                Mathf.Round(value),
                RoomOption.MinForcedExposeInterval,
                RoomOption.GetMaxForcedExposeInterval(_option.seekTime));
            RefreshTimeUI();
        }

        private void OnHideTimeChanged(float value)
        {
            if (_option == null) return;
            SetHideTime(value);
        }

        private void OnSeekTimeChanged(float value)
        {
            if (_option == null) return;
            SetSeekTime(value);
        }

        private void OnCheckTimeChanged(float value)
        {
            if (_option == null) return;
            SetCheckTime(value);
        }

        private void OnForcedExposeIntervalChanged(float value)
        {
            if (_option == null) return;
            SetForcedExposeInterval(value);
        }

        private void OnForcedExposeChanged(bool isOn)
        {
            if (_option == null) return;
            _option.forcedExposeEnabled = isOn;  // 토글 상태 반영
            RefreshForcedExposeUI();             // 패널 스왑
        }

        private void RefreshForcedExposeUI()
        {
            if (_option == null) return;
            bool on = _option.forcedExposeEnabled;

            // 체크 표시는 Toggle 컴포넌트가 처리하므로 값만 알림 없이 반영한다.
            if (forcedExposeToggle != null) forcedExposeToggle.SetIsOnWithoutNotify(on);

            // on: 실제 패널 / off: 회색 비활성 패널
            if (forcedExposeActivePanel != null) forcedExposeActivePanel.SetActive(on);
            if (forcedExposeDisabledPanel != null) forcedExposeDisabledPanel.SetActive(!on);
            if (forcedExposeIntervalStepper != null) forcedExposeIntervalStepper.SetInteractable(on);
        }

        private void RefreshAll()
        {
            if (_option == null) return;

            RefreshTimeUI();
            RefreshForcedExposeUI();
        }

        private void RefreshTimeUI()
        {
            NormalizeTimeOptions();

            if (hideTimeStepper != null) hideTimeStepper.SetValue(_option.hideTime);
            if (seekTimeStepper != null) seekTimeStepper.SetValue(_option.seekTime);
            if (checkTimeStepper != null) checkTimeStepper.SetValue(_option.checkTime);
            if (forcedExposeIntervalStepper != null)
            {
                forcedExposeIntervalStepper.SetRange(RoomOption.MinForcedExposeInterval, RoomOption.GetMaxForcedExposeInterval(_option.seekTime));
                forcedExposeIntervalStepper.SetValue(_option.forcedExposeInterval);
            }
        }

        private void NormalizeTimeOptions()
        {
            if (_option == null) return;

            _option.hideTime = Mathf.Clamp(Mathf.Round(_option.hideTime), RoomOption.MinHideTime, RoomOption.MaxHideTime);
            _option.seekTime = Mathf.Clamp(Mathf.Round(_option.seekTime), RoomOption.MinSeekTime, RoomOption.MaxSeekTime);
            _option.checkTime = Mathf.Clamp(Mathf.Round(_option.checkTime), RoomOption.MinCheckTime, RoomOption.MaxCheckTime);
            ClampForcedExposeInterval();
        }

        private void ClampForcedExposeInterval()
        {
            if (_option == null) return;

            _option.forcedExposeInterval = Mathf.Clamp(
                Mathf.Round(_option.forcedExposeInterval),
                RoomOption.MinForcedExposeInterval,
                RoomOption.GetMaxForcedExposeInterval(_option.seekTime));
        }

        private static void SetupStepper(UINumericStepper stepper, float min, float max, Action<float> onChanged, float step = 1f)
        {
            if (stepper == null) return;

            stepper.Initialize();
            stepper.Configure(min, max, step);
            stepper.OnValueChanged += onChanged;
        }
    }
}
