using UnityEngine;

namespace PixelChroma
{
    public class MapSpawnPoint : MonoBehaviour
    {
        private bool _registered;

        private void Start()
        {
            if (_registered) return;
            Register();
        }

        public void Register()
        {
            if (_registered) return;

            if (InGameManager.Instance != null)
            {
                InGameManager.Instance.RegisterSpawnPoint(this.transform);
                _registered = true;
            }
            else
            {
                Debug.LogError("[MapSpawnPoint] InGameManager를 찾을 수 없습니다!");
            }
        }
    }
}