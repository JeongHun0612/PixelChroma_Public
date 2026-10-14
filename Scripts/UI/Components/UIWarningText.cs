using DG.Tweening;
using OneText.UGUI;
using UnityEngine;

namespace PixelChroma.UI
{
    /// <summary>
    /// 경고/에러 메시지를 표시하고 흔들어 주는 위젯.
    /// </summary>
    [RequireComponent(typeof(OneTextLabel))]
    public class UIWarningText : MonoBehaviour
    {
        [Header("Text")]
        [SerializeField] private OneTextLabel text;

        [Header("Shake Settings")]
        [SerializeField] private float duration = 0.2f;
        [SerializeField] private float strength = 5f;
        [SerializeField] private int vibrato = 10;

        [Tooltip("Time.timeScale(일시정지 등)의 영향을 받지 않고 흔들지 여부")]
        [SerializeField] private bool ignoreTimeScale = true;

        [Header("Auto Hide")]
        [Tooltip("메시지를 일정 시간 후 자동으로 페이드아웃할지 여부")]
        [SerializeField] private bool autoHide = true;

        [Tooltip("메시지 표시 후 사라지기 시작할 때까지의 대기 시간(초)")]
        [SerializeField] private float autoHideDelay = 2f;

        [Tooltip("페이드아웃에 걸리는 시간(초)")]
        [SerializeField] private float fadeDuration = 0.5f;

        private Tween _hideTween;

        private void Awake()
        {
            if (text == null)
                text = GetComponent<OneTextLabel>();

            if (text == null)
                Debug.LogError($"[{nameof(UIWarningText)}] TMP_Text를 찾을 수 없습니다.", this);
        }

        private void OnDisable()
        {
            // 페이드 도중 팝업이 닫히면 트윈을 정리하고 알파를 원복해 재사용에 대비한다.
            KillHideTween();
            if (text != null) text.alpha = 1f;
        }

        public void Show(string message)
        {
            KillHideTween();
            gameObject.SetActive(true);
            SetText(message);

            if (string.IsNullOrEmpty(message))
                return;

            Shake();

            if (autoHide)
                ScheduleAutoHide();
        }

        public void Clear()
        {
            KillHideTween();
            SetText(string.Empty);
            gameObject.SetActive(false);
        }

        public void SetAutoHide(bool enabled)
        {
            autoHide = enabled;

            if (!autoHide)
                KillHideTween();
        }

        public void Shake()
        {
            // 대상 텍스트가 없으면 자기 자신을 흔든다.
            var target = text != null ? text.transform : transform;

            // 이전 흔들림이 진행 중이면 즉시 마무리해 원위치를 복원한 뒤 다시 시작한다.
            target.DOComplete();
            target.DOShakePosition(duration, strength, vibrato)
                  .SetUpdate(ignoreTimeScale);
        }

        private void SetText(string message)
        {
            if (text == null) return;

            text.text = message;

            if (!string.IsNullOrEmpty(message))
                text.alpha = 1f; // 이전 페이드로 투명해진 상태를 복구
        }

        private void ScheduleAutoHide()
        {
            // autoHideDelay 만큼 유지한 뒤 fadeDuration 동안 서서히 사라진다.
            _hideTween = DOTween.Sequence()
                .AppendInterval(autoHideDelay)
                .Append(DOTween.To(() => text.alpha, a => text.alpha = a, 0f, fadeDuration))
                .SetUpdate(ignoreTimeScale)
                .OnComplete(() =>
                {
                    text.text = string.Empty;
                    text.alpha = 1f; // 다음 표시를 위해 원복
                });
        }

        private void KillHideTween()
        {
            if (_hideTween != null && _hideTween.IsActive())
                _hideTween.Kill();

            _hideTween = null;
        }
    }
}
