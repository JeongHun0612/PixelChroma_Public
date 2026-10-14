using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using OneText.UGUI;

namespace PixelChroma.UI
{
    public class UISpectatorModePanel : MonoBehaviour
    {
        private sealed class ModeButtonState
        {
            public readonly UIFocusButton Button;
            public readonly SpectateMode Mode;
            public readonly Vector3 BaseScale;
            public Tween ScaleTween;

            public ModeButtonState(UIFocusButton button, SpectateMode mode)
            {
                Button = button;
                Mode = mode;
                BaseScale = button != null ? button.transform.localScale : Vector3.one;
            }
        }

        [SerializeField] private UIFocusButton seekerButton;
        [SerializeField] private UIFocusButton runnerButton;
        [SerializeField] private UIFocusButton freeButton;
        [SerializeField] private OneTextLabel targetNameText;
        [SerializeField] private RawImage targetNameImage;
        [SerializeField] private Color selfTargetNameColor = Color.yellow;

        [Header("Tab Selection Animation")]
        [SerializeField, Min(1f)] private float focusScale = 1.1f;
        [SerializeField, Min(0f)] private float animationDuration = 0.18f;

        // 관전 모드 버튼을 눌렀을 때 발생합니다. 실제 전환은 로컬 플레이어가 처리합니다.
        public event Action<SpectateMode> OnModeSelected;

        private Color _defaultTargetNameColor = Color.white;
        private Texture2D _cachedTexture;
        private ModeButtonState[] _buttonStates;
        private SpectateMode _activeMode;
        private bool _hasActiveMode;

        private void Awake()
        {
            if (targetNameText != null)
                _defaultTargetNameColor = targetNameText.color;

            _buttonStates = new[]
            {
                new ModeButtonState(seekerButton, SpectateMode.Seeker),
                new ModeButtonState(runnerButton, SpectateMode.Runner),
                new ModeButtonState(freeButton, SpectateMode.Free)
            };

            foreach (ModeButtonState state in _buttonStates)
                BindModeButton(state.Button, state.Mode);
        }

        private void BindModeButton(UIFocusButton button, SpectateMode mode)
        {
            if (button == null) return;

            button.OnClicked += () => OnModeSelected?.Invoke(mode);
        }

        private void OnDisable()
        {
            KillScaleTweens();

            // 비활성화 도중 멈춘 중간 스케일이 다음 표시 때 남지 않도록 최종 상태로 정리합니다.
            if (_buttonStates == null) return;

            foreach (ModeButtonState state in _buttonStates)
            {
                if (state.Button != null)
                    state.Button.transform.localScale = state.BaseScale
                        * (_hasActiveMode && _activeMode == state.Mode ? focusScale : 1f);
            }
        }

        private void OnDestroy()
        {
            KillScaleTweens();

            if (_cachedTexture != null)
                Destroy(_cachedTexture);
        }

        public void Show()
        {
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        public void SetActiveMode(SpectateMode mode)
        {
            foreach (ModeButtonState state in _buttonStates)
                state.Button?.SetSelected(mode == state.Mode);

            // 같은 상태 동기화가 반복되어도 진행 중인 팝 애니메이션은 다시 시작하지 않습니다.
            if (_hasActiveMode && _activeMode == mode)
                return;

            bool hadActiveMode = _hasActiveMode;
            SpectateMode previousMode = _activeMode;

            _activeMode = mode;
            _hasActiveMode = true;

            foreach (ModeButtonState state in _buttonStates)
            {
                UpdateButtonScale(
                    state,
                    mode == state.Mode,
                    hadActiveMode && previousMode == state.Mode);
            }
        }

        private void UpdateButtonScale(ModeButtonState state, bool isSelected, bool wasSelected)
        {
            if (state.Button == null) return;

            state.ScaleTween?.Kill();
            state.ScaleTween = null;

            Transform buttonTransform = state.Button.transform;

            if (isSelected)
            {
                buttonTransform.localScale = state.BaseScale * focusScale;

                // DOPunchScale은 현재 스케일로 돌아오므로 선택 유지 크기를 먼저 적용합니다.
                Vector3 punchAmount = state.BaseScale * (focusScale - 1f);
                state.ScaleTween = buttonTransform
                    .DOPunchScale(punchAmount, animationDuration, vibrato: 1, elasticity: 0.5f)
                    .SetUpdate(true);
            }
            else if (wasSelected)
            {
                state.ScaleTween = buttonTransform
                    .DOScale(state.BaseScale, animationDuration * 0.5f)
                    .SetEase(Ease.OutQuad)
                    .SetUpdate(true);
            }
            else
            {
                buttonTransform.localScale = state.BaseScale;
            }
        }

        private void KillScaleTweens()
        {
            if (_buttonStates == null) return;

            foreach (ModeButtonState state in _buttonStates)
            {
                state.ScaleTween?.Kill();
                state.ScaleTween = null;
            }
        }

        public void SetTargetName(string displayName, bool isSelf, byte[] pixelPng = null)
        {
            bool showPixel = PixelNicknameHelper.ShouldShowPixel();

            if (showPixel && targetNameImage != null)
            {
                // Todo: 본인일 경우에 닉네임에 Outline등 추가? 필요하면 하기
                _cachedTexture = PixelNicknameHelper.ApplyPng(targetNameImage, pixelPng, _cachedTexture);
                targetNameImage.gameObject.SetActive(true);
                if (targetNameText != null) targetNameText.gameObject.SetActive(false);
            }
            else
            {
                if (targetNameImage != null) targetNameImage.gameObject.SetActive(false);
                if (targetNameText != null)
                {
                    targetNameText.gameObject.SetActive(true);
                    targetNameText.text = displayName;
                    targetNameText.color = isSelf ? selfTargetNameColor : _defaultTargetNameColor;
                }
            }
        }

    }
}
