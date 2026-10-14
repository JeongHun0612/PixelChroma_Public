using UnityEngine;

namespace PixelChroma
{
    // 로비 스폰 기준점. 로비 씬(LobbyRoom)에 1개만 배치합니다.
    public class LobbySpawnPoint : MonoBehaviour
    {
        private static LobbySpawnPoint _instance;

        [Tooltip("이 지점을 중심으로 ± 이만큼 랜덤하게 흩어집니다.")]
        [SerializeField] private Vector2 spread = new Vector2(3f, 1.5f);

        private void Awake() => _instance = this;
        private void OnDestroy() { if (_instance == this) _instance = null; }

        // 배치돼 있지 않으면 fallback을 그대로 돌려줍니다(= 지금 동작 유지).
        public static Vector3 GetSpawnPosition(Vector3 fallback)
        {
            if (_instance == null) return fallback;

            return _instance.transform.position + new Vector3(
                Random.Range(-_instance.spread.x, _instance.spread.x),
                Random.Range(-_instance.spread.y, _instance.spread.y),
                0f);
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.7f);
            Gizmos.DrawWireCube(transform.position, new Vector3(spread.x * 2f, spread.y * 2f, 0.1f));
        }
#endif
    }
}