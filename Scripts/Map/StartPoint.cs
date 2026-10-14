using UnityEngine;

namespace PixelChroma
{
    public class StartPoint : MonoBehaviour
    {
        [Header("ExitPoint 지정")]
        public Transform destination;

        private void OnTriggerEnter2D(Collider2D other)
        {
            // 콜라이더가 자식에 있어도 Rigidbody2D가 붙은 진짜 Player를 가져옴
            Rigidbody2D rb = other.attachedRigidbody;
            if (rb == null) return;

            Debug.Log($"트리거 감지됨: {rb.name}, 태그: {rb.tag}");

            if (rb.CompareTag("Player"))
            {
                TPManager.Instance.Teleport(rb.transform, destination);
            }
        }
    }
}