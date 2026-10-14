using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    // TMP_Dropdown은 펼칠 때 현재 선택된 항목으로 스크롤을 맞춰주지 않는다.
    // 이 컴포넌트는 리스트가 열리는 즉시, 선택된 항목이 보이도록 스크롤한다.
    // (TMP_Dropdown은 Show() 안에서 항목 배치를 동기적으로 끝내므로 프레임 대기가 필요 없다.)
    [AddComponentMenu("UI/Scroll To Selected Dropdown (TMP)")]
    public class ScrollToSelectedDropdown : TMP_Dropdown
    {
        public override void OnPointerClick(PointerEventData eventData)
        {
            base.OnPointerClick(eventData);   // 여기서 리스트를 생성·배치한다.
            ScrollToSelected();
        }

        public override void OnSubmit(BaseEventData eventData)
        {
            base.OnSubmit(eventData);         // 키보드로 열 때도 동일하게 처리.
            ScrollToSelected();
        }

        private void ScrollToSelected()
        {
            // Show()는 비활성/상호작용 불가/이미 열림 등에서 조기 반환하므로 리스트가 없을 수 있다.
            var listTr = transform.Find("Dropdown List");
            if (listTr == null) return;

            var scrollRect = listTr.GetComponentInChildren<ScrollRect>();
            if (scrollRect == null || scrollRect.content == null) return;

            int count = options.Count;
            if (count <= 1) return;

            Canvas.ForceUpdateCanvases();

            // 선택 항목의 위치 비율만큼 스크롤한다(맨 위=1, 맨 아래=0).
            float normalized = 1f - (float)value / (count - 1);
            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(normalized);
        }
    }
}
