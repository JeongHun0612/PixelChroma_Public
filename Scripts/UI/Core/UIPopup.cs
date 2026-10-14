using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public abstract class UIPopup : UIPanel
    {
        [Header("Dimmed Background")]
        [SerializeField] private Button dimmed;
        [SerializeField] private bool closeOnDimmedClick = true;

        [Header("Escape")]
        [SerializeField] private bool closeOnEscape = true;

        public virtual bool CloseOnEscape => closeOnEscape;

        public override void Initialize()
        {
            base.Initialize();

            if (dimmed != null && closeOnDimmedClick)
                dimmed.onClick.AddListener(OnClickBack);
        }

        public virtual void OnClickBack()
        {
            UIManager.Instance.HidePopup().Forget();
        }
    }
}
