using DG.Tweening;
using UnityEngine;

namespace PixelChroma.UI
{
    /// <summary>
    /// 패널(그룹)을 통째로 slide + fade 시키는 범용 등장 연출.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIPanelSlideIntro : MonoBehaviour
    {
        public enum SlideFrom { Left, Right, Bottom, Top }

        [Header("Slide")]
        [Tooltip("어느 방향에서 들어올지")]
        [SerializeField] private SlideFrom slideFrom = SlideFrom.Left;
        [Tooltip("시작 오프셋 거리(px)")]
        [SerializeField] private float slideDistance = 60f;
        [Tooltip("이동 시간(초)")]
        [SerializeField] private float duration = 0.35f;
        [Tooltip("시작 지연(초)")]
        [SerializeField] private float startDelay = 0f;
        [Tooltip("감속 커브")]
        [SerializeField] private Ease ease = Ease.OutCubic;

        private RectTransform _rect;
        private CanvasGroup _cg;
        private Vector2 _restPos;
        private bool _captured;
        private Sequence _seq;

        private Vector2 StartOffset => slideFrom switch
        {
            SlideFrom.Left => new Vector2(-slideDistance, 0f),
            SlideFrom.Right => new Vector2(slideDistance, 0f),
            SlideFrom.Bottom => new Vector2(0f, -slideDistance),
            SlideFrom.Top => new Vector2(0f, slideDistance),
            _ => new Vector2(-slideDistance, 0f),
        };

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _cg = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnEnable()
        {
            // 정착 위치는 레이아웃 제어를 받지 않아 불변 → 최초 1회만 캡처
            if (!_captured)
            {
                _restPos = _rect.anchoredPosition;
                _captured = true;
            }
            Play();
        }

        private void OnDisable()
        {
            _seq?.Kill();
            _seq = null;

            _rect.anchoredPosition = _restPos;
            _cg.alpha = 1f;
        }

        private void Play()
        {
            _seq?.Kill();

            // 초기 상태: 지정 방향으로 밀고 투명하게
            _rect.anchoredPosition = _restPos + StartOffset;
            _cg.alpha = 0f;

            _seq = DOTween.Sequence().SetLink(gameObject);
            _seq.Insert(startDelay, _rect.DOAnchorPos(_restPos, duration).SetEase(ease));
            _seq.Insert(startDelay, _cg.DOFade(1f, duration).SetEase(Ease.OutQuad));
        }
    }
}
