using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

namespace PixelChroma.UI
{
    public class UICatchLog : MonoBehaviour
    {
        [SerializeField] private RectTransform logParent;
        [SerializeField] private GameObject logEntryPrefab;

        [Header("Timing")]
        [SerializeField] private float displayDuration = 3f;
        [SerializeField] private float fadeDuration = 0.5f;

        [Header("Animation")]
        [Tooltip("등장 / 밀림 / 축소 애니메이션 시간")]
        [SerializeField] private float slideInDuration = 0.5f;
        [Tooltip("로그 한 줄의 높이")]
        [SerializeField] private float entryHeight = 50f;
        [Tooltip("로그 사이 간격")]
        [SerializeField] private float spacing = 2f;

        [Header("Scale")]
        [Tooltip("가장 위 로그의 최소 크기")]
        [SerializeField] private float minScale = 0.7f;
        [Tooltip("한 칸 위로 밀려날 때마다 줄어드는 양")]
        [SerializeField] private float shrinkPerStep = 0.1f;

        [Header("Limits")]
        [SerializeField] private int maxVisible = 3;

        private readonly List<LogEntry> _activeEntries = new();
        private readonly Queue<LogEntry> _pool = new();

        private struct LogEntry
        {
            public UICatchLogEntry view;
            public Tween fadeTween;
        }

        private void OnEnable()
        {
            RunnerPlayer.OnPlayerCaught += HandlePlayerCaught;
        }

        private void OnDisable()
        {
            RunnerPlayer.OnPlayerCaught -= HandlePlayerCaught;
        }

        private void HandlePlayerCaught(string catcherName, string caughtName, uint catcherNetId, uint caughtNetId)
        {
            if (_activeEntries.Count >= maxVisible)
                ReturnToPool(0);

            LogEntry entry = GetEntry();
            entry.view.Setup(catcherName, caughtName, catcherNetId, caughtNetId);

            var cg = entry.view.CanvasGroup;
            var rt = (RectTransform)entry.view.transform;

            // 초기 상태: 최하단(Y=0) + 투명
            cg.alpha = 0f;
            rt.anchoredPosition = Vector2.zero;
            rt.localScale = Vector3.one;

            entry.view.gameObject.SetActive(true);

            var go = entry.view.gameObject;

            // 페이드인 → 대기 → 페이드아웃 → 풀 반환
            entry.fadeTween = DOTween.Sequence()
                .Append(DOTween.To(() => cg.alpha, v => cg.alpha = v,
                    1f, slideInDuration).SetEase(Ease.OutCubic))
                .AppendInterval(displayDuration)
                .Append(DOTween.To(() => cg.alpha, v => cg.alpha = v,
                    0f, fadeDuration))
                .OnComplete(() => ReturnToPoolByGo(go));

            _activeEntries.Add(entry);

            // 모든 로그의 위치 + 크기 재계산 (기존 로그가 위로 밀림)
            RefreshPositionsAndScales();
        }

        /// <summary>
        /// 각 로그의 Y 위치와 크기를 DOTween으로 부드럽게 조절합니다.
        /// 가장 아래(최신)가 Y=0, scale=1.0
        /// 위로 갈수록 Y 증가, scale 감소
        /// </summary>
        private void RefreshPositionsAndScales()
        {
            int count = _activeEntries.Count;
            for (int i = 0; i < count; i++)
            {
                // i=0: 가장 오래된(위), i=count-1: 최신(아래)
                int distanceFromBottom = count - 1 - i;

                float targetScale = Mathf.Max(minScale, 1f - distanceFromBottom * shrinkPerStep);
                float targetY = distanceFromBottom * (entryHeight + spacing);

                var rt = (RectTransform)_activeEntries[i].view.transform;

                // 이전 위치/크기 트윈만 제거 (fadeTween은 CanvasGroup 대상이라 영향 없음)
                rt.DOKill();
                rt.DOAnchorPosY(targetY, slideInDuration).SetEase(Ease.OutCubic);
                rt.DOScale(new Vector3(targetScale, targetScale, 1f), slideInDuration)
                    .SetEase(Ease.OutCubic);
            }
        }

        private LogEntry GetEntry()
        {
            if (_pool.Count > 0)
            {
                var entry = _pool.Dequeue();
                entry.fadeTween?.Kill();
                return entry;
            }

            GameObject go = Instantiate(logEntryPrefab, logParent);
            return new LogEntry
            {
                view = go.GetComponent<UICatchLogEntry>(),
                fadeTween = null,
            };
        }

        private void ReturnToPool(int index)
        {
            if (index < 0 || index >= _activeEntries.Count) return;

            var entry = _activeEntries[index];
            entry.fadeTween?.Kill();
            ((RectTransform)entry.view.transform).DOKill();

            entry.view.CanvasGroup.alpha = 1f;
            entry.view.transform.localScale = Vector3.one;
            entry.view.gameObject.SetActive(false);

            _activeEntries.RemoveAt(index);
            _pool.Enqueue(entry);

            // 하나 빠졌으니 남은 로그들이 아래로 내려오면서 커짐
            RefreshPositionsAndScales();
        }

        private void ReturnToPoolByGo(GameObject go)
        {
            for (int i = 0; i < _activeEntries.Count; i++)
            {
                if (_activeEntries[i].view.gameObject == go)
                {
                    ReturnToPool(i);
                    return;
                }
            }
        }

        public void ClearAll()
        {
            while (_activeEntries.Count > 0)
                ReturnToPool(0);
        }
    }
}