using UnityEngine;

namespace PixelChroma
{
    // 앱 전체 생명주기 싱글톤 (DontDestroyOnLoad)
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoBehaviour
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
            DontDestroyOnLoad(gameObject);
        }
    }
}
