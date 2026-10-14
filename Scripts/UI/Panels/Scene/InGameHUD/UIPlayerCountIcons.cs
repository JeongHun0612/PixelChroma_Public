using UnityEngine;

namespace PixelChroma.UI
{
    // 남은 인원 아이콘을 미리 배치해두고, 전달받은 count만큼만 앞에서부터 활성화합니다.
    public class UIPlayerCountIcons : MonoBehaviour
    {
        [Tooltip("남은 인원을 표시할 아이콘들. 배열 순서대로 앞에서부터 count개가 켜집니다.")]
        [SerializeField] private GameObject[] icons;

        public void SetCount(int count)
        {
            if (icons == null) return;

            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] != null)
                    icons[i].SetActive(i < count);
            }
        }
    }
}
