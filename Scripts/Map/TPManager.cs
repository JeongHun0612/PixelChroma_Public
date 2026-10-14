using System.Net.NetworkInformation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PixelChroma
{
    public class TPManager : MonoBehaviour
    {
        public static TPManager Instance { get; private set; }

        private GameObject player;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
          
        }


        public void Teleport(Transform player, Transform destination)
        {
            if (player == null || destination == null)
                return;

            Rigidbody2D rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
                rb.linearVelocity = Vector2.zero;

            player.position = destination.position;

            Debug.Log($"[TPManager] 플레이어가 {destination.name} 위치로 텔레포트했습니다.");
        }
    }
}
