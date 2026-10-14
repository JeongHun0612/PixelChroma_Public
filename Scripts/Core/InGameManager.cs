using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace PixelChroma
{
    public class InGameManager : MonoSingleton<InGameManager>
    {
        private Transform _spawnPoint;


        private readonly List<RunnerPlayer> _runners = new();
        private readonly List<SeekerPlayer> _seekers = new();

        // 명부(스폰/퇴장/사망)가 바뀔 때마다 (생존 러너 수, 생존 술래 수)로 발화됩니다.
        public static event Action<int, int> OnPlayerCountChanged;

        public IReadOnlyList<RunnerPlayer> Runners => _runners;
        public IReadOnlyList<SeekerPlayer> Seekers => _seekers;

        public void RegisterPlayer(PlayerBase player)
        {
            _runners.RemoveAll(r => r == null);
            _seekers.RemoveAll(s => s == null);

            switch (player)
            {
                case RunnerPlayer runner when !_runners.Contains(runner):
                    _runners.Add(runner);
                    break;
                case SeekerPlayer seeker when !_seekers.Contains(seeker):
                    _seekers.Add(seeker);
                    break;
                case LobbyPlayer:
                    break; // 로비 플레이어는 인게임 명부 대상이 아닙니다.
            }

            NotifyRosterChanged();
        }

        public void UnregisterPlayer(PlayerBase player)
        {
            switch (player)
            {
                case RunnerPlayer runner:
                    _runners.Remove(runner);
                    break;
                case SeekerPlayer seeker:
                    _seekers.Remove(seeker);
                    break;
            }

            NotifyRosterChanged();
        }

        public int GetAliveRunnerCount()
        {
            return _runners.Count(r => r != null && !r.IsDead);
        }

        public int GetAliveSeekerCount()
        {
            return _seekers.Count(s => s != null);
        }

        // 사망(SyncVar hook) 등 명부 수치가 변하는 지점에서 호출해 HUD 갱신을 트리거합니다.
        public void NotifyRosterChanged()
        {
            OnPlayerCountChanged?.Invoke(GetAliveRunnerCount(), GetAliveSeekerCount());
        }

        public bool HasAnyRunner()
        {
            return _runners.Count > 0;
        }

        public void RegisterSpawnPoint(Transform spawnTransform)
        {
            _spawnPoint = spawnTransform;
            Debug.Log($"[InGameManager] 스폰 포인트 등록 완료: {spawnTransform.name}");
        }

        public void ResetState()
        {
            _spawnPoint = null;

            _runners.Clear();
            _seekers.Clear();

            if (MapLoader.Instance != null)
                MapLoader.Instance.UnloadCurrentMap();

            Debug.Log("[InGameManager] 이전 판의 스폰 포인트 데이터가 초기화되었습니다.");
        }

        public Vector3 GetSpawnPosition()
        {
            if (_spawnPoint != null)
            {
                Vector3 offset = new Vector3(UnityEngine.Random.Range(-0.2f, 0.2f), UnityEngine.Random.Range(-0.2f, 0.2f), 0);
                return _spawnPoint.position + offset;
            }

            Debug.LogWarning("[InGameManager] 등록된 스폰 포인트가 없습니다! Vector3.zero를 반환합니다.");
            return Vector3.zero;
        }

        public Vector3 GetWaitingPosition()
        {
            Vector3 offset = new Vector3(UnityEngine.Random.Range(-0.5f, 0.5f), UnityEngine.Random.Range(-0.5f, 0.5f), 0);
            return Vector3.zero + offset;
        }
    }
}
