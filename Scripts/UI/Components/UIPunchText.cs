using UnityEngine;
using OneText.UGUI;
using DG.Tweening;

namespace PixelChroma.UI
{
    [RequireComponent(typeof(OneTextLabel))]
    public class UIPunchText : MonoBehaviour
    {
        [SerializeField] private float punchScale = 0.2f;
        [SerializeField] private float duration = 0.2f;

        private OneTextLabel _text;
        private Vector3 _baseScale;

        private void Awake()
        {
            _text = GetComponent<OneTextLabel>();
            _baseScale = transform.localScale;
        }

        public void SetText(string value)
        {
            if (_text.text == value) return;
            _text.text = value;
            Punch();
        }

        private void Punch()
        {
            transform.DOKill();
            transform.localScale = _baseScale;
            transform.DOPunchScale(_baseScale * punchScale, duration);
        }
    }
}
