using UnityEngine.Events;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    // 버튼 클릭 효과음을 한 곳에서 관리하기 위한 헬퍼.
    // onClick.AddListener(핸들러) 대신 AddClickListener(핸들러)를 쓰면 클릭 사운드가 함께 재생된다.
    public static class ButtonExtensions
    {
        public static void AddClickListener(this Button button, UnityAction action)
        {
            if (button == null) return;

            button.onClick.AddListener(() =>
            {
                SoundManager.Instance?.PlaySfx(SfxType.ButtonClick);
                action?.Invoke();
            });
        }
    }
}
