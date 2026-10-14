using DG.Tweening;
using UnityEngine;

namespace PixelChroma.UI
{
    /// <summary>
    /// Runner 머리 위의 좋아요 입력 안내를 표시하고 숨기는 World Space UI 뷰입니다.
    /// Canvas 루트에 붙이면 자신의 RectTransform/CanvasGroup을 기본 애니메이션 대상으로 사용합니다.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class WorldLikeGuideView : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform animatedRoot;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Position")]
        [Tooltip("현재 캐릭터 폼의 하단에서 좋아요 캔버스를 더 아래로 내릴 거리(월드 유닛)")]
        [SerializeField, Min(0f)] private float bottomOffset = 0.5f;

        [Header("Show")]
        [SerializeField, Min(0f)] private float showDuration = 0.2f;
        [SerializeField] private Ease showEase = Ease.OutBack;

        [Header("Hide")]
        [SerializeField, Min(0f)] private float hideDuration = 0.15f;
        [SerializeField] private Ease hideEase = Ease.InBack;

        private Sequence _sequence;
        private Vector2 _visiblePosition;
        private Vector3 _visibleScale;
        private bool _initialized;
        private bool _isVisible;

        private void Awake()
        {
            Initialize();
            ApplyHiddenImmediately();
        }

        private void OnEnable()
        {
            if (!_initialized) return;

            if (_isVisible)
                ApplyVisibleImmediately();
            else
                ApplyHiddenImmediately();
        }

        private void OnDisable()
        {
            KillSequence();
        }

        private void OnDestroy()
        {
            KillSequence();
        }

        public void Show()
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);

            Initialize();
            if (animatedRoot == null || canvasGroup == null)
                return;

            if (_isVisible)
                return;

            _isVisible = true;
            KillSequence();

            canvasGroup.alpha = 0f;
            animatedRoot.anchoredPosition = _visiblePosition;
            animatedRoot.localScale = Vector3.zero;

            _sequence = DOTween.Sequence().SetLink(gameObject);
            _sequence.Join(canvasGroup.DOFade(1f, showDuration).SetEase(Ease.OutQuad));
            _sequence.Join(animatedRoot.DOScale(_visibleScale, showDuration).SetEase(showEase));
            _sequence.OnComplete(() => _sequence = null);
        }

        public void Hide()
        {
            Initialize();
            if (!_isVisible || animatedRoot == null || canvasGroup == null)
                return;

            _isVisible = false;
            KillSequence();

            _sequence = DOTween.Sequence().SetLink(gameObject);
            _sequence.Join(canvasGroup.DOFade(0f, hideDuration).SetEase(Ease.InQuad));
            _sequence.Join(animatedRoot.DOScale(Vector3.zero, hideDuration).SetEase(hideEase));
            _sequence.OnComplete(() =>
            {
                _sequence = null;
                ApplyHiddenImmediately();
                gameObject.SetActive(false);
            });
        }

        /// <summary>
        /// 현재 폼의 피벗 기준 하단 Y를 받아 캔버스를 하단보다 offset만큼 아래에 배치합니다.
        /// </summary>
        public void SetFormBottomY(float formBottomY)
        {
            Initialize();
            if (animatedRoot == null || canvasGroup == null)
                return;

            KillSequence();
            _visiblePosition.y = formBottomY - bottomOffset;

            if (_isVisible)
                ApplyVisibleImmediately();
            else
                ApplyHiddenImmediately();
        }

        private void Initialize()
        {
            if (_initialized) return;

            if (animatedRoot == null)
                animatedRoot = transform as RectTransform;
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
            if (animatedRoot != null)
            {
                _visiblePosition = animatedRoot.anchoredPosition;
                _visibleScale = animatedRoot.localScale;
            }

            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            _initialized = true;
        }

        private void ApplyVisibleImmediately()
        {
            if (animatedRoot == null || canvasGroup == null) return;
            canvasGroup.alpha = 1f;
            animatedRoot.anchoredPosition = _visiblePosition;
            animatedRoot.localScale = _visibleScale;
        }

        private void ApplyHiddenImmediately()
        {
            if (animatedRoot == null || canvasGroup == null) return;
            canvasGroup.alpha = 0f;
            animatedRoot.anchoredPosition = _visiblePosition;
            animatedRoot.localScale = _visibleScale;
        }

        private void KillSequence()
        {
            _sequence?.Kill();
            _sequence = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (animatedRoot == null)
                animatedRoot = transform as RectTransform;
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }
#endif
    }
}
