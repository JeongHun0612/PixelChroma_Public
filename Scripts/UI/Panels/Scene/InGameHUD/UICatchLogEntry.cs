using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UICatchLogEntry : MonoBehaviour
    {
        [SerializeField] private OneTextLabel catcherText;
        [SerializeField] private RawImage catcherImage;    
        [SerializeField] private OneTextLabel caughtText;
        [SerializeField] private RawImage caughtImage;     


        public CanvasGroup CanvasGroup { get; private set; }
        public LayoutElement LayoutElement { get; private set; }

        private Texture2D _catcherTex;                      
        private Texture2D _caughtTex;                       

        private void Awake()
        {
            CanvasGroup = GetComponent<CanvasGroup>();
            if (CanvasGroup == null)
                CanvasGroup = gameObject.AddComponent<CanvasGroup>();

            LayoutElement = GetComponent<LayoutElement>();
            if (LayoutElement == null)
                LayoutElement = gameObject.AddComponent<LayoutElement>();
        }

        private void OnDestroy()                           
        {
            if (_catcherTex != null) Destroy(_catcherTex);
            if (_caughtTex != null) Destroy(_caughtTex);
        }

        public void Setup(string catcherName, string caughtName, uint catcherNetId, uint caughtNetId)
        {
            SetupSlot(catcherText, catcherImage, catcherName, catcherNetId, ref _catcherTex);
            SetupSlot(caughtText, caughtImage, caughtName, caughtNetId, ref _caughtTex);
        }

        private void SetupSlot(OneTextLabel text, RawImage image, string name, uint netId, ref Texture2D tex)
        {
            var player = PixelNicknameHelper.FindPlayer(netId);
            byte[] pngData = PixelNicknameHelper.GetPixelData(player);
            bool showPixel = PixelNicknameHelper.ShouldShowPixel();

            if (showPixel && image != null)
            {
                tex = PixelNicknameHelper.ApplyPng(image, pngData, tex);
                image.gameObject.SetActive(true);
                if (text != null) text.gameObject.SetActive(false);
            }
            else
            {
                if (image != null) image.gameObject.SetActive(false);
                if (text != null)
                {
                    text.gameObject.SetActive(true);
                    text.text = name;
                }
            }
        }
    }
}