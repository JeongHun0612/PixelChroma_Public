using System;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    // 버튼과 선택 표시용 Focus 오브젝트를 한 컴포넌트로 묶는다.
    // Focus 참조를 버튼 자신이 들고 있으므로, 쓰는 쪽 인스펙터에 슬롯이 버튼마다 두 개씩 늘지 않는다.
    //
    // 주의: 여기서 다루는 "선택"은 EventSystem의 Selected 상태가 아니라 지속되는 선택 상태다.
    // (Button의 Transition은 호버/누름 같은 순간적 피드백 용도로 그대로 두고 쓰면 된다.)
    [RequireComponent(typeof(Button))]
    public class UIFocusButton : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private GameObject focus;

        // 키 입력으로도 같은 동작을 하는 버튼(예: 관전 모드)은 클릭에만 소리가 나면
        // 조작 방법에 따라 피드백이 달라지므로, 그런 곳은 이걸 꺼서 양쪽 다 무음으로 맞춘다.
        [SerializeField] private bool playClickSound = true;

        public event Action OnClicked;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            // 클릭 사운드는 AddClickListener(ButtonExtensions)에서 일괄 처리한다.
            if (playClickSound)
                button.AddClickListener(() => OnClicked?.Invoke());
            else
                button.onClick.AddListener(() => OnClicked?.Invoke());
        }

        public void SetSelected(bool selected)
        {
            if (focus != null)
                focus.SetActive(selected);
        }
    }
}
