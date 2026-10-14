using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Mirror;
using UnityEngine;

namespace PixelChroma
{
    public class ScoreManager : MonoSingleton<ScoreManager>
    {
        [Header("Settings")]
        [SerializeField] private float broadcastInterval = 5f;

        // 기존 필드에 추가
        [Header("Vision Score")]
        [SerializeField] private float visionCheckInterval = 1f;  // 시야 체크 주기
        [SerializeField] private int scorePerSeekerVisible = 10;   // Seeker 1명 보일 때마다 주는 점수
        [SerializeField] private float visionRange = 12f;          // 시야 범위 (카메라 대리)

        [Header("Survival Bonus")]
        [SerializeField] private float survivalBonusPercent = 0.3f;  // 생존 보너스 30%

        [Header("Distance Multiplier")]
        [Tooltip("x축: 0 = 시야 경계, 1 = 시커와 밀착 / y축: 그 거리에서의 점수 배율")]
        [SerializeField]
        private AnimationCurve distanceMultiplierCurve
    = AnimationCurve.Linear(0f, 1f, 1f, 3f);

        private readonly Dictionary<int, int> _scores = new();
        private readonly HashSet<int> _frozenPlayers = new();
        private CancellationTokenSource _cts;
        private CancellationTokenSource _visionCts;

        // 1,2,3등 및 꼴등 플레이어 connectionId
        private int _firstConnId = -1;
        private int _secondConnId = -1;
        private int _thirdConnId = -1;
        private int _lastConnId = -1;



        public float SurvivalBonusPercent => survivalBonusPercent;
        public float VisionRange => visionRange;
        public int FirstConnectionId => _firstConnId;
        public int SecondConnectionId => _secondConnId;
        public int ThirdConnectionId => _thirdConnId;
        public int LastConnectionId => _lastConnId;

        // ── 점수 조작 API (서버에서 호출) ──

        public void AddScore(int connectionId, int amount)
        {
            if (_frozenPlayers.Contains(connectionId)) return;   // ← 동결 체크

            if (!_scores.ContainsKey(connectionId))
                _scores[connectionId] = 0;

            _scores[connectionId] += amount;
        }
        public void SetScore(int connectionId, int value)
        {
            _scores[connectionId] = value;
        }

        public int GetScore(int connectionId)
        {
            return _scores.TryGetValue(connectionId, out var s) ? s : 0;
        }

        // ── 브로드캐스트 시작/종료 ──

        public void StartBroadcast()
        {
            StopBroadcast();
            _cts = new CancellationTokenSource();
            BroadcastLoop(_cts.Token).Forget();
        }

        public void StopBroadcast()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        public void ResetAll()
        {
            StopBroadcast();
            StopVisionScoring();
            _scores.Clear();
            _frozenPlayers.Clear();
        }

        private void OnDestroy()
        {
            StopBroadcast();
            StopVisionScoring();
        }

        // ── 주기적 전송 루프 ──

        private async UniTaskVoid BroadcastLoop(CancellationToken token)
        {
            float timeout = 5f;
            float elapsed = 0f;

            while (elapsed < timeout)
            {
                if (NetworkServer.connections.Values.All(conn => conn.isReady))
                    break;

                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(0.5f),
                    cancellationToken: token
                );
                elapsed += 0.5f;
            }

            SendScoreBoard(0f);

            // 주기 갱신은 SeekTime 진입 시점부터 시작
            await UniTask.WaitUntil(
                () => GameRoundManager.Instance != null
                   && GameRoundManager.Instance.CurrentTimeState == TimeState.SeekTime,
                cancellationToken: token
            );

            // 이후 정상 주기
            while (!token.IsCancellationRequested)
            {
                SendScoreBoard();
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(broadcastInterval),
                    cancellationToken: token
                );
            }
        }

        private void SendScoreBoard(float interval = -1f)
        {
            if (!NetworkServer.active) return;
            if (interval < 0f) interval = broadcastInterval;

            var sorted = _scores
                .OrderByDescending(kv => kv.Value)
                .Select((kv, index) => new ScoreEntry
                {
                    rank = index + 1,
                    connectionId = kv.Key,
                    netId = GetNetIdByConnectionId(kv.Key),
                    playerName = NicknameRegistry.TryGet(kv.Key, out var n) ? n : $"Player {kv.Key}",
                    score = kv.Value,
                    pixelNicknamePng = NicknameRegistry.TryGetPixel(kv.Key, out var png) ? png : System.Array.Empty<byte>(),
                    isDead = _frozenPlayers.Contains(kv.Key)
                })
                .ToArray();

            NetworkServer.SendToAll(new ScoreBoardMessage
            {
                entries = sorted,
                refreshInterval = interval
            });
        }

        public void ForceBroadcast()
        {
            SendScoreBoard(0f);
        }

        // ── 시야 점수 시작/종료 ──

        public void StartVisionScoring()
        {
            StopVisionScoring();
            _visionCts = new CancellationTokenSource();
            VisionScoringLoop(_visionCts.Token).Forget();
        }

        public void StopVisionScoring()
        {
            _visionCts?.Cancel();
            _visionCts?.Dispose();
            _visionCts = null;
        }

        private async UniTaskVoid VisionScoringLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(visionCheckInterval),
                    cancellationToken: token
                );
                CheckDistanceAndAddScore();
            }
        }

        private void CheckDistanceAndAddScore()
        {
            if (!NetworkServer.active) return;
            if (InGameManager.Instance == null) return;
            if (GameRoundManager.Instance == null) return;
            if (GameRoundManager.Instance.CurrentTimeState != TimeState.SeekTime) return;

            var seekers = InGameManager.Instance.Seekers;
            var runners = InGameManager.Instance.Runners;

            foreach (var runner in runners)                // Runner 기준 루프
            {
                if (runner == null || runner.IsDead) continue;
                int connId = runner.connectionToClient != null
                    ? runner.connectionToClient.connectionId : -1;
                if (connId < 0) continue;

                foreach (var seeker in seekers)
                {
                    if (seeker == null) continue;

                    float dist = Vector2.Distance(
                        runner.transform.position,
                        seeker.transform.position
                    );
                    if (dist > visionRange) continue;

                    // 시커마다 거리에 따른 개별 배율을 적용해 합산 가산한다.
                    int gain = Mathf.RoundToInt(
                        scorePerSeekerVisible * EvaluateDistanceMultiplier(dist)
                    );
                    if (gain > 0)
                        AddScore(connId, gain);   // Runner에게 점수
                }
            }
        }


        //-----------------------헬퍼함수---------------------------------------

        private uint GetNetIdByConnectionId(int connectionId)
        {
            if (NetworkServer.connections.TryGetValue(connectionId, out var conn))
                return conn.identity != null ? conn.identity.netId : 0;
            return 0;
        }

        public void FreezeScore(int connectionId)
        {
            _frozenPlayers.Add(connectionId);

            if (!NetworkServer.active) return;
            NetworkServer.SendToAll(new PlayerDeadMessage { connectionId = connectionId });
        }

        public void SetRevealedColor(int connectionId, Color32 color)
        {
            if (!NetworkServer.active) return;
            NetworkServer.SendToAll(new RevealedPixelMessage
            {
                connectionId = connectionId,
                color = color
            });
        }

        public void SaveRanks()
        {
            _firstConnId = _secondConnId = _thirdConnId = _lastConnId = -1;

            if (_scores.Count < 2) return;  // 2명 미만이면 아무것도 안 함

            var sorted = _scores
                .OrderByDescending(kv => kv.Value)
                .ToList();

            // 메달 부여 (꼴지 제외한 나머지에서)
            _firstConnId = sorted[0].Key;
            if (sorted.Count >= 3) _secondConnId = sorted[1].Key;
            if (sorted.Count >= 4) _thirdConnId = sorted[2].Key;

            // 꼴지는 항상 부여 (2명 이상이면 무조건)
            _lastConnId = sorted[sorted.Count - 1].Key;
        }

        public void ClearRanks()
        {
            _firstConnId = _secondConnId = _thirdConnId = _lastConnId = -1;
        }

        // 시야 경계에 걸칠수록 낮고, 시커에 밀착할수록 높은 배율을 돌려줍니다.
        // 커브를 상수 1로 두면 거리 배율 도입 이전과 완전히 동일하게 동작합니다.
        private float EvaluateDistanceMultiplier(float dist)
        {
            if (visionRange <= 0f) return 0f;

            float t = 1f - Mathf.Clamp01(dist / visionRange);   // 0 = 경계, 1 = 밀착
            return Mathf.Max(0f, distanceMultiplierCurve.Evaluate(t));
        }
    }
}