using DG.Tweening;
using OneText.UGUI;
using System.Collections.Generic;
using UnityEngine;

namespace PixelChroma.UI
{
    public class UIScoreBoard : MonoBehaviour
    {
        [Header("Row")]
        [SerializeField] private UIScoreRow rowPrefab;
        [SerializeField] private Transform rowContainer;

        [Header("Header")]
        [SerializeField] private OneTextLabel refreshTimerText;

        [Header("Visibility")]
        [SerializeField] private GameObject visualRoot;

        [Header("Animation")]
        [SerializeField] private float sweepDuration = 1f;      // 스윕 1회 시간
        [SerializeField] private float sweepStagger = 0.3f;     // Row 간 시간차
        [SerializeField] private float counterDuration = 0.6f;


        [Header("Sound")]
        [Tooltip("SeekTime/CheckTime 전환음과 겹치지 않도록, 전환 직후 이 시간 동안 갱신음을 생략")]
        [SerializeField] private float stateChangeMuteWindow = 1.5f;

        private float _lastStateChangeTime = -100f;

        // 애니메이션 기준값 (connectionId 키)
        private readonly Dictionary<int, int> _prevScoreMap = new();
        private readonly Dictionary<int, int> _prevRankMap = new();

        private readonly List<UIScoreRow> _pool = new();

        // ── 초기화 ──

        public void Initialize()
        {
            _pool.Clear();

            // Container 안에 에디터에서 미리 배치해둔 UIScoreRow들을 풀에 등록
            foreach (Transform child in rowContainer)
            {
                var row = child.GetComponent<UIScoreRow>();
                if (row != null)
                {
                    row.ResetRow();
                    _pool.Add(row);
                }
            }
        }

        private void OnEnable()
        {
            ScoreBoardClient.OnSnapshot += HandleSnapshot;
            ScoreBoardClient.OnPlayerDead += HandlePlayerDead;
            ScoreBoardClient.OnPixelRevealed += HandlePixelRevealed;
            ScoreBoardClient.OnCountdownChanged += HandleCountdownChanged;
            GameRoundManager.OnTimeStateChanged += HandleTimeStateChanged;

            RenderCurrentState();   // 인스턴스 교체 직후에도 대기 없이 즉시 반영
        }

        private void OnDisable()
        {
            ScoreBoardClient.OnSnapshot -= HandleSnapshot;
            ScoreBoardClient.OnPlayerDead -= HandlePlayerDead;
            ScoreBoardClient.OnPixelRevealed -= HandlePixelRevealed;
            ScoreBoardClient.OnCountdownChanged -= HandleCountdownChanged;
            GameRoundManager.OnTimeStateChanged -= HandleTimeStateChanged;

            DOTween.Kill(this);
        }

        public void SetVisible(bool visible)
        {
            if (visualRoot != null) visualRoot.SetActive(visible);
        }

        // ── 이벤트 핸들러 ──

        // 스냅샷 복원은 델타 갱신이 아니므로 기준값을 비워 '즉시 표시' 분기를 타게 한다.
        private void RenderCurrentState()
        {
            _prevScoreMap.Clear();
            _prevRankMap.Clear();

            HandleCountdownChanged(ScoreBoardClient.RefreshCountdown);
            HandleSnapshot(ScoreBoardClient.Entries);
        }

        private void HandleSnapshot(ScoreEntry[] entries)
        {
            if (entries == null || entries.Length == 0) ClearRows();
            else RebuildRows(entries);
        }

        private void HandleCountdownChanged(int remaining)
        {
            if (refreshTimerText != null)
                refreshTimerText.text = remaining > 0 ? remaining.ToString() : "";
        }

        private void HandlePlayerDead(int connectionId)
        {
            foreach (var row in _pool)
            {
                if (row.gameObject.activeSelf && row.CurrentConnectionId == connectionId)
                {
                    row.ApplyDeadColor();
                    break;
                }
            }
        }

        private void HandlePixelRevealed(int connectionId, Color32 color)
        {
            foreach (var row in _pool)
            {
                if (row.gameObject.activeSelf && row.CurrentConnectionId == connectionId)
                {
                    row.PlayExposeEffect(color);
                    break;
                }
            }
        }

        // ── UI 구축 (오브젝트 풀링) ──

        private void RebuildRows(ScoreEntry[] entries)
        {
            DOTween.Kill(this);

            // ── 풀 확장 ──
            while (_pool.Count < entries.Length)
            {
                var row = Instantiate(rowPrefab, rowContainer);
                row.ResetRow();
                _pool.Add(row);
            }

            // ── 남는 행 비활성화 ──
            for (int i = entries.Length; i < _pool.Count; i++)
                _pool[i].ResetRow();

            // ── 최초 수신: 애니메이션 없이 즉시 표시 ──
            if (_prevScoreMap.Count == 0)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    _pool[i].gameObject.SetActive(true);
                    ApplyRow(_pool[i], entries[i]);
                }

                SaveCurrentState(entries);
                return;
            }

            bool isForcedSnapshot = ScoreBoardClient.RefreshCountdown <= 0;
            bool nearStateChange = Time.unscaledTime - _lastStateChangeTime < stateChangeMuteWindow;

            if (!isForcedSnapshot && !nearStateChange)
                SoundManager.Instance?.PlaySfx(SfxType.ScoreRefresh);

            // ── 스윕 시퀀스: Row별 시간차로 스윕 → 중간에 데이터 갱신 ──
            for (int i = 0; i < entries.Length; i++)
            {
                int idx = i;   // 클로저용 캡처
                var entry = entries[idx];

                _pool[idx].gameObject.SetActive(true);

                _prevScoreMap.TryGetValue(entry.connectionId, out int oldScore);
                _prevRankMap.TryGetValue(entry.connectionId, out int oldRank);

                DOVirtual.DelayedCall(idx * sweepStagger, () =>
                {
                    _pool[idx].PlaySweep(sweepDuration, onMidPoint: () =>
                    {
                        ApplyRow(_pool[idx], entry);

                        if (oldScore != entry.score)
                            _pool[idx].AnimateScoreCounter(oldScore, entry.score, counterDuration);

                        if (oldRank != entry.rank)
                            _pool[idx].AnimateRankBadge(oldRank, entry.rank);
                    });
                }).SetTarget(this);
            }

            SaveCurrentState(entries);
        }

        // 사망 여부는 서버 스냅샷과 클라 누적 집합의 합집합으로 판정한다.
        // 스냅샷이 낡은 경우(인스턴스 교체 직후)에도 즉시 올바른 색이 나온다.
        private void ApplyRow(UIScoreRow row, ScoreEntry entry)
        {
            bool isSelf = ScoreBoardClient.MyConnectionId >= 0
                       && entry.connectionId == ScoreBoardClient.MyConnectionId;
            bool isDead = entry.isDead || ScoreBoardClient.IsDead(entry.connectionId);

            row.SetData(entry, isSelf, isDead);
        }

        private void SaveCurrentState(ScoreEntry[] entries)
        {
            _prevScoreMap.Clear();
            _prevRankMap.Clear();

            for (int i = 0; i < entries.Length; i++)
            {
                _prevScoreMap[entries[i].connectionId] = entries[i].score;
                _prevRankMap[entries[i].connectionId] = entries[i].rank;
            }
        }

        private void ClearRows()
        {
            foreach (var row in _pool)
            {
                if (row != null) row.ResetRow();
            }

            _prevScoreMap.Clear();
            _prevRankMap.Clear();

            if (refreshTimerText != null) refreshTimerText.text = "";
        }

        private void HandleTimeStateChanged(TimeState state, int timer)
        {
            if (state == TimeState.SeekTime || state == TimeState.CheckTime)
                _lastStateChangeTime = Time.unscaledTime;
        }
    }
}