using OneText.UGUI;
using UnityEngine;

namespace PixelChroma.UI
{
    /// <summary>
    /// 이 <see cref="OneTextLabel"/>이 로컬라이즈 폰트를 따라가도록 폰트 풀에 직접 등록한다.
    ///
    /// <see cref="role"/>로 UI/디스플레이용과 채팅/본문용 폰트를 구분해 등록한다.
    /// 채팅 라벨(입력창·메시지 프리팹)은 Chat으로 지정해 가독성 폰트를 따라가게 한다.
    /// </summary>
    [RequireComponent(typeof(OneTextLabel))]
    public class LocalizedFontText : MonoBehaviour
    {
        [Tooltip("이 라벨이 따라갈 폰트 역할. 채팅 입력창·메시지 라벨은 Chat으로 지정한다.")]
        [SerializeField] private FontRole role = FontRole.Ui;

        private OneTextLabel _label;

        public OneTextLabel Label => _label != null ? _label : (_label = GetComponent<OneTextLabel>());

        private void Awake()
        {
            _label = GetComponent<OneTextLabel>();
            LocalizationManager.Register(_label, role);
        }

        private void OnDestroy()
        {
            LocalizationManager.Unregister(_label);
        }
    }
}
