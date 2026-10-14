using Cysharp.Threading.Tasks;
using OneText.UGUI;
using UnityEngine;

namespace PixelChroma.UI
{
    public abstract class UIPanel : MonoBehaviour
    {
        public virtual void Initialize() { }

        public virtual void RegisterLocalizedTexts()
        {
            foreach (var label in GetComponentsInChildren<OneTextLabel>(true))
            {
                if (label.GetComponentInParent<LocalizedFontIgnore>(true) != null)
                    continue;

                if (label.GetComponent<LocalizedFontText>() != null)
                    continue;

                LocalizationManager.Register(label);
            }
        }

        public async UniTask Show()
        {
            gameObject.SetActive(true);
            await OnShowAnimation();
            await OnShow();
        }

        public async UniTask Hide(bool instant = false)
        {
            if (!instant) await OnHideAnimation();
            await OnHide();
            gameObject.SetActive(false);
        }

        protected virtual UniTask OnShowAnimation() => UniTask.CompletedTask;
        protected virtual UniTask OnShow() => UniTask.CompletedTask;
        protected virtual UniTask OnHideAnimation() => UniTask.CompletedTask;
        protected virtual UniTask OnHide() => UniTask.CompletedTask;

        public virtual void OnBeforeShow() { }
        public virtual void OnAfterHide() { }
    }
}
