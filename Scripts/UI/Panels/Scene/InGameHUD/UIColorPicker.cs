using System;
using OneText.UGUI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    [Serializable]
    public class ToolSlot
    {
        public BrushMode mode;
        public UIFocusButton button;
    }

    public class UIColorPicker : MonoBehaviour
    {
        private const int DefaultMaxBrushSize = 5;
        private const int DefaultBrushSize = 1;

        [Header("SV Rectangle")]
        [SerializeField] private RectTransform svArea;
        [SerializeField] private RawImage svImage;
        [SerializeField] private RectTransform svSelector;

        [Header("Hue Bar")]
        [SerializeField] private RectTransform hueBarArea;
        [SerializeField] private RectTransform hueSelector;

        [Header("Preview")]
        [SerializeField] private Image colorPreview;
        [SerializeField] private OneTextLabel hexLabel;

        [Header("Tools")]
        [SerializeField] private ToolSlot[] toolSlots;

        [Header("History")]
        [SerializeField] private Button undoButton;
        [SerializeField] private Button redoButton;

        [Header("Brush Size")]
        [SerializeField] private Slider brushSizeSlider;
        [SerializeField] private OneTextLabel brushSizeLabel;

        private Texture2D _svTexture;
        private float _hue;
        private float _saturation = 1f;
        private float _value = 1f;
        private bool _initialized;

        public Color SelectedColor => Color.HSVToRGB(_hue, _saturation, _value);
        public int BrushSize => brushSizeSlider != null ? (int)brushSizeSlider.value : 1;
        public int MaxBrushSize => DefaultMaxBrushSize;

        public event Action<Color> OnColorChanged;
        public event Action<int> OnBrushSizeChanged;
        public event Action<BrushMode> OnToolButtonClicked;
        public event Action OnUndoButtonClicked;
        public event Action OnRedoButtonClicked;

        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            _svTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            SetupSVDrag();
            SetupHueDrag();
            SetupBrushSizeSliders();
            SetupToolButtons();
            SetColor(Color.white);
            SetActiveTool(BrushMode.Brush);
        }

        private void SetupSVDrag()
        {
            var trigger = svArea.gameObject.AddComponent<EventTrigger>();
            var pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            pointerDown.callback.AddListener(data => OnSVInput((PointerEventData)data));
            trigger.triggers.Add(pointerDown);

            var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener(data => OnSVInput((PointerEventData)data));
            trigger.triggers.Add(drag);
        }

        private void SetupHueDrag()
        {
            var trigger = hueBarArea.gameObject.AddComponent<EventTrigger>();
            var pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            pointerDown.callback.AddListener(data => OnHueInput((PointerEventData)data));
            trigger.triggers.Add(pointerDown);

            var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener(data => OnHueInput((PointerEventData)data));
            trigger.triggers.Add(drag);
        }

        private void OnSVInput(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    svArea, eventData.position, eventData.pressEventCamera, out var localPoint))
                return;


            var rect = svArea.rect;
            _saturation = Mathf.Clamp01((localPoint.x - rect.x) / rect.width);
            _value = Mathf.Clamp01((localPoint.y - rect.y) / rect.height);

            UpdateSVSelectorPosition();
            UpdateDisplay();
        }

        private void OnHueInput(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    hueBarArea, eventData.position, eventData.pressEventCamera, out var localPoint))
                return;


            var rect = hueBarArea.rect;
            _hue = Mathf.Clamp01((localPoint.x - rect.x) / rect.width);

            UpdateHueSelectorPosition();
            RegenerateSVTexture();
            UpdateDisplay();
        }

        private void UpdateSVSelectorPosition()
        {
            var rect = svArea.rect;
            svSelector.anchorMin = Vector2.zero;
            svSelector.anchorMax = Vector2.zero;
            svSelector.anchoredPosition = new Vector2(rect.width * _saturation, rect.height * _value);
        }

        private void UpdateHueSelectorPosition()
        {
            var rect = hueBarArea.rect;
            hueSelector.anchorMin = new Vector2(0, 0);
            hueSelector.anchorMax = new Vector2(0, 1);
            hueSelector.anchoredPosition = new Vector2(rect.width * _hue, 0);
        }

        public void SetColor(Color color)
        {
            Color.RGBToHSV(color, out _hue, out _saturation, out _value);
            RegenerateSVTexture();
            UpdateSVSelectorPosition();
            UpdateHueSelectorPosition();
            UpdateDisplay();
        }

        private void RegenerateSVTexture()
        {
            int w = _svTexture.width;
            int h = _svTexture.height;
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / (h - 1);
                for (int x = 0; x < w; x++)
                {
                    float s = (float)x / (w - 1);
                    _svTexture.SetPixel(x, y, Color.HSVToRGB(_hue, s, v));
                }
            }
            _svTexture.Apply();
            svImage.texture = _svTexture;
        }

        private void UpdateDisplay()
        {
            var color = SelectedColor;
            colorPreview.color = color;
            if (hexLabel != null) hexLabel.text = $"#{ColorUtility.ToHtmlStringRGB(color)}";
            OnColorChanged?.Invoke(color);
        }

        private void SetupBrushSizeSliders()
        {
            if (brushSizeSlider == null) return;
            brushSizeSlider.wholeNumbers = true;
            brushSizeSlider.minValue = 1;
            brushSizeSlider.maxValue = DefaultMaxBrushSize;
            brushSizeSlider.value = DefaultBrushSize;
            brushSizeSlider.onValueChanged.AddListener(OnBrushSizeSliderChanged);
            UpdateBrushSizeLabel();
        }

        private void OnBrushSizeSliderChanged(float value)
        {
            UpdateBrushSizeLabel();
            OnBrushSizeChanged?.Invoke((int)value);
        }

        public void SetBrushSize(int size)
        {
            if (brushSizeSlider == null) return;
            brushSizeSlider.value = Mathf.Clamp(size, 1, DefaultMaxBrushSize);
        }

        private void UpdateBrushSizeLabel()
        {
            if (brushSizeLabel != null)
                brushSizeLabel.text = $"{(int)brushSizeSlider.value}px";
        }

        public void SetActiveTool(BrushMode mode)
        {
            if (toolSlots == null) return;
            foreach (var slot in toolSlots)
            {
                if (slot.mode == BrushMode.Eyedropper)
                    continue;

                if (slot.button != null)
                    slot.button.SetSelected(slot.mode == mode);
            }
        }

        public void SetEyedropperPreview(Color color)
        {
            if (colorPreview != null)
                colorPreview.color = color;
        }

        private void SetupToolButtons()
        {
            if (toolSlots != null)
            {
                foreach (var slot in toolSlots)
                {
                    if (slot.mode == BrushMode.Eyedropper)
                    {
                        if (slot.button != null)
                            slot.button.gameObject.SetActive(false);
                        continue;
                    }

                    if (slot.button != null)
                    {
                        var mode = slot.mode;
                        slot.button.OnClicked += () => OnToolButtonClicked?.Invoke(mode);
                    }
                }
            }

            if (undoButton != null)
                undoButton.onClick.AddListener(() => OnUndoButtonClicked?.Invoke());
            if (redoButton != null)
                redoButton.onClick.AddListener(() => OnRedoButtonClicked?.Invoke());
        }

        private void OnDestroy()
        {
            if (_svTexture != null) Destroy(_svTexture);
        }
    }
}
