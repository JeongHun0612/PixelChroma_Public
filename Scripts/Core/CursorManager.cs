using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace PixelChroma.UI
{
    public enum CursorType
    {
        Default,
        Brush,
        Eraser,
        Eyedropper,
        Crosshair,
        Fill
    }

    public class CursorManager : MonoSingleton<CursorManager>
    {
        [Header("UI References")]
        public RectTransform cursorRect;
        public Image cursorImage;

        [Header("Cursor Settings")]
        public Sprite defaultCursor;
        public Vector2 defaultPivot = new Vector2(0f, 1f);

        public Sprite brushCursor;
        public Vector2 brushPivot = new Vector2(0f, 1f);

        public Sprite eraserCursor;
        public Vector2 eraserPivot = new Vector2(0f, 1f);

        public Sprite eyedropperCursor;
        public Vector2 eyedropperPivot = new Vector2(0f, 1f);

        public Sprite fillCursor;
        public Vector2 fillPivot = new Vector2(0f, 1f);

        public Sprite crosshairCursor;
        public Vector2 crosshairPivot = new Vector2(0.5f, 0.5f);

        [Header("Fill Paint")]
        [Tooltip("통 커서의 물감 레이어. 통 커서일 때만 보이며 선택한 색으로 칠해진다.")]
        public Image fillPaintOverlay;

        [Header("Eyedropper Preview")]
        public RectTransform eyedropperPreview;
        public Image eyedropperColorSwatch;
        public OneTextLabel eyedropperHexLabel;

        private Canvas _parentCanvas;

        private void InitCanvas()
        {
            if (cursorRect != null && _parentCanvas == null)
            {
                var canvas = cursorRect.GetComponentInParent<Canvas>();
                if (canvas != null) _parentCanvas = canvas.rootCanvas;

                // 커서의 Nested Canvas를 TMP_Dropdown(sortingOrder=30000)보다 위에 배치
                var nestedCanvas = cursorRect.GetComponent<Canvas>();
                if (nestedCanvas != null)
                {
                    nestedCanvas.overrideSorting = true;
                    nestedCanvas.sortingOrder = 32767;
                }
            }

            DisableCursorRaycasts();
        }

        private void Start()
        {
            Cursor.visible = false;
            DisableCursorRaycasts();
            SetCursorType(CursorType.Default);
        }

        private void Update()
        {
            if (cursorRect == null || cursorImage == null) return;
            if (_parentCanvas == null) InitCanvas();
            if (_parentCanvas == null || Mouse.current == null) return;

            Vector2 mousePos = Mouse.current.position.ReadValue();
            // 캔버스의 크기, 해상도, 렌더 모드(Overlay/Camera)에 상관없이 
            // 스크린 마우스 좌표를 캔버스 내부의 정확한 로컬 좌표로 100% 완벽하게 변환합니다.
            RectTransform canvasRect = _parentCanvas.GetComponent<RectTransform>();
            Camera cam = _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _parentCanvas.worldCamera;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, mousePos, cam, out Vector2 localPos))
            {
                cursorRect.localPosition = localPos;
            }
        }

        public void UpdateEyedropperPreview(Color color)
        {
            if (eyedropperColorSwatch != null)
                eyedropperColorSwatch.color = color;

            if (eyedropperHexLabel != null)
                eyedropperHexLabel.text = $"#{ColorUtility.ToHtmlStringRGB(color)}";
        }

        /// <summary>
        /// 페인트 통 커서의 물감 색을 지정한다.
        /// 물감 레이어는 통 커서일 때만 보이므로 어느 타이밍에 불러도 안전하다.
        /// </summary>
        public void SetPaintColor(Color color)
        {
            if (fillPaintOverlay != null)
                fillPaintOverlay.color = color;
        }

        public void SetCursorType(CursorType type, float brushScale = 1f)
        {
            if (cursorImage == null || cursorRect == null) return;

            cursorRect.localScale = Vector3.one;

            switch (type)
            {
                case CursorType.Default:
                    cursorImage.sprite = defaultCursor;
                    cursorRect.pivot = defaultPivot;
                    break;
                case CursorType.Brush:
                    cursorImage.sprite = brushCursor;
                    cursorRect.pivot = brushPivot;
                    cursorRect.localScale = new Vector3(brushScale, brushScale, 1f);
                    break;
                case CursorType.Eraser:
                    cursorImage.sprite = eraserCursor;
                    cursorRect.pivot = eraserPivot;
                    cursorRect.localScale = new Vector3(brushScale, brushScale, 1f);
                    break;
                case CursorType.Eyedropper:
                    cursorImage.sprite = eyedropperCursor;
                    cursorRect.pivot = eyedropperPivot;
                    break;
                case CursorType.Fill:
                    // 물감은 fillPaintOverlay가 따로 그리므로 여기서는 통 몸체만 세팅한다.
                    cursorImage.sprite = fillCursor != null ? fillCursor : brushCursor;
                    cursorRect.pivot = fillPivot;
                    break;
                case CursorType.Crosshair:
                    cursorImage.sprite = crosshairCursor;
                    cursorRect.pivot = crosshairPivot;
                    break;
            }

            // 물감 레이어와 스포이드 미리보기는 각자의 커서일 때만 보이도록 커서 타입에 종속시킨다.
            if (fillPaintOverlay != null)
                fillPaintOverlay.gameObject.SetActive(type == CursorType.Fill);

            if (eyedropperPreview != null)
                eyedropperPreview.gameObject.SetActive(type == CursorType.Eyedropper);
        }

        private void DisableCursorRaycasts()
        {
            if (cursorRect == null) return;

            var graphics = cursorRect.GetComponentsInChildren<Graphic>(true);
            foreach (var graphic in graphics)
                graphic.raycastTarget = false;
        }
    }
}
