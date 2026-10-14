using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    // 버튼의 "비활성" 상태를 입력 차단(interactable)과 덮개 오브젝트로 함께 처리한다.
    // Color Tint의 Disabled alpha가 낮아 뒷배경이 비치는 문제를 피하려고, 비활성 시각 표시는 덮개가 담당한다.
    [RequireComponent(typeof(Button))]
    public class UIDisableCoverButton : MonoBehaviour
    {
        // 모든 비활성 덮개가 동일한 톤을 쓰도록 색을 코드에서 통일한다. (톤 조정 시 이 값만 바꾸면 됨)
        private static readonly Color CoverColor = new Color(0f, 0f, 0f, 0.6f);

        [SerializeField] private Button button;
        [SerializeField] private Image disabledCover;

        public Button Button => button;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (button != null)
            {
                // 비활성 시각은 덮개가 담당하므로 Color Tint의 Disabled Color는 아무 변화도 주지 않도록
                // 흰색(불투명)으로 고정한다. Disabled 틴트만 무력화되고 Highlighted/Pressed 피드백은 유지된다.
                ColorBlock colors = button.colors;
                colors.disabledColor = Color.white;
                button.colors = colors;
            }

            // 덮개 색은 인스펙터 값과 무관하게 통일된 톤으로 강제한다.
            if (disabledCover != null)
                disabledCover.color = CoverColor;
        }

        public void SetInteractable(bool interactable)
        {
            if (button != null)
                button.interactable = interactable;

            if (disabledCover != null)
                disabledCover.gameObject.SetActive(!interactable);
        }
    }
}
