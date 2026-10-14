using Mirror;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace PixelChroma.UI
{
    public class TeammateIndicatorPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform indicatorRoot;
        [SerializeField] private float edgePadding = 60f;

        [Header("Indicator Prefabs")]
        [Tooltip("Seeker 위치를 표시할 아이콘 프리팹")]
        [SerializeField] private GameObject seekerIndicatorPrefab;
        [Tooltip("Runner 위치를 표시할 아이콘 프리팹")]
        [SerializeField] private GameObject runnerIndicatorPrefab;

        [Header("Viewer Settings")]
        [Tooltip("이 패널이 붙은 HUD의 주인 역할. 실제로 추적할 역할은 GameModeRule이 TimeState를 보고 결정합니다.")]
        [FormerlySerializedAs("targetRole")]
        [SerializeField] private PlayerRole viewerRole = PlayerRole.Seeker;

        [Header("Distance Filter")]
        [Tooltip("이 거리 이내의 팀원만 인디케이터를 표시합니다 (0 = 제한 없음). CheckTime에는 무시됩니다.")]
        [SerializeField] private float maxTrackingDistance = 30f;

        private Camera targetCamera;

        // 아이콘은 프리팹에 종속되므로 역할별로 풀을 따로 유지한다.
        private readonly List<IndicatorIconView> _seekerPool = new();
        private readonly List<IndicatorIconView> _runnerPool = new();

        // 이번 Refresh가 사용할 풀/프리팹. Refresh가 배치 전에 반드시 채운다.
        private List<IndicatorIconView> _activePool;
        private GameObject _activePrefab;

        private void OnEnable()
        {
            targetCamera = Camera.main;
        }

        public void Deactivate()
        {
            HideAll();
        }

        private void Update()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (targetCamera == null) { HideAll(); return; }

            // 현재 모드 + 타임스테이트에서 "내가 어떤 역할의 위치를 볼지" 결정.
            // CheckTime에는 Seeker가 Runner를 보게 되므로 매 프레임 다시 물어본다.
            var round = GameRoundManager.Instance;
            var rule = round?.CurrentRule;

            PlayerRole trackRole = rule != null
                ? rule.GetIndicatorTargetRole(viewerRole, round.CurrentTimeState)
                : viewerRole;

            if (trackRole == PlayerRole.None) { HideAll(); return; }

            // CheckTime에는 거리와 무관하게 전원을 표시한다.
            bool limitDistance = rule == null || rule.ShouldLimitIndicatorDistance(round.CurrentTimeState);

            // 이번 프레임에 쓸 아이콘 종류를 확정하고, 반대 역할 풀은 전부 숨긴다(역할 전환 시 잔상 방지).
            bool trackingRunner = trackRole == PlayerRole.Runner;
            _activePool = trackingRunner ? _runnerPool : _seekerPool;
            _activePrefab = trackingRunner ? runnerIndicatorPrefab : seekerIndicatorPrefab;
            HidePool(trackingRunner ? _seekerPool : _runnerPool, 0);

            if (_activePrefab == null) { HideAll(); return; }

            //거리체크
            Transform localTransform = NetworkClient.localPlayer != null
                ? NetworkClient.localPlayer.transform
                : null;

            // 거리 제한을 쓸 때만 기준점이 필요하다.
            if (limitDistance && localTransform == null) { HideAll(); return; }

            // trackRole에 따라 추적할 플레이어 리스트 결정
            int used = 0;

            if (trackRole == PlayerRole.Seeker)
            {
                var seekers = InGameManager.Instance != null ? InGameManager.Instance.Seekers : null;
                if (seekers == null) { HideAll(); return; }

                foreach (var seeker in seekers)
                {
                    if (seeker == null || seeker.isLocalPlayer) continue;
                    if (!IsWithinRange(localTransform, seeker.transform, limitDistance)) continue;
                    TryPlaceIndicator(seeker.transform, seeker.playerName,
                                      seeker.pixelNicknamePng, ref used);
                }
            }
            else if (trackRole == PlayerRole.Runner)
            {
                var runners = InGameManager.Instance != null ? InGameManager.Instance.Runners : null;
                if (runners == null) { HideAll(); return; }

                foreach (var runner in runners)
                {
                    if (runner == null || runner.isLocalPlayer) continue;
                    if (runner.IsVisuallyDead) continue;
                    if (!IsWithinRange(localTransform, runner.transform, limitDistance)) continue;
                    TryPlaceIndicator(runner.transform, runner.playerName,
                                      runner.pixelNicknamePng, ref used);
                }
            }

            HidePool(_activePool, used);
        }

        // 화면 밖에 있으면 가장자리에 배치, 화면 안이면 스킵. 배치했으면 used 증가.
        private bool TryPlaceIndicator(Transform target, string playerName,
                               byte[] pixelPng, ref int used)
        {
            Vector3 viewport = targetCamera.WorldToViewportPoint(target.position);
            bool onScreen = viewport.z > 0f
                && viewport.x >= 0f && viewport.x <= 1f
                && viewport.y >= 0f && viewport.y <= 1f;

            if (onScreen) return true;

            var icon = GetIcon(used++);
            PlaceOnEdge(icon, viewport);
            icon.SetNicknameData(playerName, pixelPng);
            return true;
        }


        private IndicatorIconView GetIcon(int index)
        {
            if (index < _activePool.Count)
            {
                _activePool[index].gameObject.SetActive(true);
                return _activePool[index];
            }

            var view = Instantiate(_activePrefab, indicatorRoot).GetComponent<IndicatorIconView>();
            _activePool.Add(view);
            return view;
        }

        private void PlaceOnEdge(IndicatorIconView icon, Vector3 viewport)
        {
            if (viewport.z < 0f)
            {
                viewport.x = 1f - viewport.x;
                viewport.y = 1f - viewport.y;
            }

            Rect screenRect = indicatorRoot.rect;

            Vector2 pixelDir = new Vector2(
                (viewport.x - 0.5f) * screenRect.width,
                (viewport.y - 0.5f) * screenRect.height);

            if (pixelDir.sqrMagnitude < 0.0001f) pixelDir = Vector2.up;
            Vector2 dir = pixelDir.normalized;

            float halfW = screenRect.width / 2f - edgePadding;
            float halfH = screenRect.height / 2f - edgePadding;

            float scale = Mathf.Min(
                dir.x != 0f ? halfW / Mathf.Abs(dir.x) : float.MaxValue,
                dir.y != 0f ? halfH / Mathf.Abs(dir.y) : float.MaxValue);

            icon.Slot.anchoredPosition = dir * scale;

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            icon.RingArrow.localRotation = Quaternion.Euler(0, 0, angle);
        }

        private void HideAll()
        {
            HidePool(_seekerPool, 0);
            HidePool(_runnerPool, 0);
        }

        private static void HidePool(List<IndicatorIconView> pool, int fromIndex)
        {
            for (int i = fromIndex; i < pool.Count; i++)
                pool[i].gameObject.SetActive(false);
        }


        private bool IsWithinRange(Transform local, Transform target, bool limitDistance)
        {
            if (!limitDistance) return true;              // CheckTime 등 제한 해제 구간
            if (maxTrackingDistance <= 0f) return true;   // 0이면 무제한
            float sqrDist = (target.position - local.position).sqrMagnitude;
            return sqrDist <= maxTrackingDistance * maxTrackingDistance;
        }
    }
}