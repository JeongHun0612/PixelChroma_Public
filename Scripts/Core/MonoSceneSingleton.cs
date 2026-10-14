using UnityEngine;

namespace PixelChroma
{
    // 씬 한정 싱글톤 (씬 전환 시 자동 소멸)
    public abstract class MonoSceneSingleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static T Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this as T;
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
