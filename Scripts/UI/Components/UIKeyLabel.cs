using OneText.UGUI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PixelChroma.UI
{
    /// <summary>
    /// InputAction에 실제로 바인딩된 키를 키캡 문자로 표시합니다.
    /// 액션 에셋을 고치면 UI가 따라오므로 프리팹에 손으로 적어둘 필요가 없습니다.
    /// 에디트 모드에서도 갱신되어 플레이하지 않고도 프리팹에서 실제 키를 확인할 수 있습니다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(LocalizedFontIgnore))]
    public class UIKeyLabel : MonoBehaviour
    {
        [Tooltip("표시할 액션. Input Actions 에셋에서 액션을 드래그해 연결합니다.")]
        [SerializeField] private InputActionReference action;

        [Tooltip("바인딩이 여러 개인 액션에서 몇 번째를 표시할지. 키 하나짜리 액션은 0입니다.")]
        [SerializeField] private int bindingIndex;

        [Tooltip("비워두면 같은 오브젝트의 TMP_Text를 사용합니다.")]
        [SerializeField] private OneTextLabel label;

        private void OnEnable() => Refresh();

        /// <summary>
        /// 표시를 다시 계산합니다. 나중에 런타임 리바인딩을 붙이면 그 직후에 호출하세요.
        /// </summary>
        public void Refresh()
        {
            if (label == null) label = GetComponent<OneTextLabel>();
            if (label == null) return;

            InputAction target = action != null ? action.action : null;
            if (target == null) return;

            if (bindingIndex < 0 || bindingIndex >= target.bindings.Count) return;

            string display = target.GetBindingDisplayString(
                bindingIndex, InputBinding.DisplayStringOptions.DontIncludeInteractions);

            if (label.text == display) return;

            label.text = display;

#if UNITY_EDITOR
            // 에디트 모드 변경은 더티 표시를 해줘야 프리팹/씬에 실제로 저장됩니다.
            if (!Application.isPlaying)
                UnityEditor.EditorUtility.SetDirty(label);
#endif
        }

#if UNITY_EDITOR
        // 인스펙터에서 값을 바꾸는 즉시 반영합니다.
        // OnValidate 안에서 바로 텍스트를 건드리면 TMP 메시 재생성 때문에 경고가 나므로 한 프레임 미룹니다.
        private void OnValidate()
        {
            if (Application.isPlaying) return;

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                Refresh();
            };
        }
#endif
    }
}
