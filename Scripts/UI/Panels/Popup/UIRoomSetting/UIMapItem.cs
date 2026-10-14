using System;
using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    [RequireComponent(typeof(Button))]
    public class UIMapItem : MonoBehaviour
    {
        [SerializeField] private OneTextLabel label;
        [SerializeField] private Button button;
        [SerializeField] private GameObject focus;

        public event Action<int> OnClicked;

        private int _index;

        private void Awake()
        {
            if (button == null)
                button = GetComponent<Button>();

            if (label == null)
                label = GetComponentInChildren<OneTextLabel>();

            button.AddClickListener(() => OnClicked?.Invoke(_index));
        }

        public void Setup(int index, string displayName)
        {
            _index = index;
            if (label != null) label.text = displayName;
        }

        public void SetSelected(bool selected)
        {
            if (focus != null)
                focus.SetActive(selected);
        }

        /// <summary>
        /// 아직 내려받는 중인 창작마당 맵처럼, 목록에는 보이되 고를 수 없는 항목에 사용한다.
        /// </summary>
        public void SetInteractable(bool interactable)
        {
            if (button != null)
                button.interactable = interactable;
        }
    }
}