using Cysharp.Threading.Tasks;
using DG.Tweening;
using OneText.UGUI;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    [Serializable]
    public class PresetSlot
    {
        public Button button;
        public RawImage preview;
    }

    public class UIPixelNicknameSetting : UIPopup
    {
        [Header("Canvas")]
        [SerializeField] private RawImage canvasImage;
        [SerializeField] private RawImage checkerboardBackground;
        [SerializeField] private RawImage hoverOverlay;
        [SerializeField] private int canvasWidth = 48;
        [SerializeField] private int canvasHeight = 12;

        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Color Picker")]
        [SerializeField] private UIColorPicker colorPicker;

        [Header("Tools")]
        [SerializeField] private Button clearButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button closeButton;

        [Header("Preview")]
        [SerializeField] private RawImage previewImage;

        [Header("Presets")]
        [SerializeField] private PresetSlot[] presetSlots;    // 유저 프리셋 3개 (Button + RawImage)
        [SerializeField] private Color presetActiveColor = new Color(1f, 0.85f, 0.3f, 1f);
        [SerializeField] private Color presetInactiveColor = new Color(0.3f, 0.3f, 0.3f, 1f);

        [Header("Default Preset")]
        [SerializeField] private PresetSlot[] defaultPresetSlots;  // 기본 프리셋 3개 (Button + RawImage)
        [SerializeField] private Texture2D[] defaultImages;        // Inspector에서 기본 이미지들 할당

        [Header("Animation")]
        [SerializeField] private RectTransform panel;
        [SerializeField] private float animationDuration = 0.25f;

        [Header("Save Feedback")]
        [SerializeField] private OneTextLabel saveFeedbackLabel;

        private Texture2D _editTexture;
        private Texture2D _checkerTexture;
        private Color _currentColor = Color.white;
        private BrushMode _brushMode = BrushMode.Brush;
        private int _brushSize = 1;
        private bool _isPainting;
        private ScreenEyedropperSampler _eyedropperSampler;
        private BrushMode _eyedropperReturnMode = BrushMode.Brush;
        private bool _isEyedropperActive;
        private bool _waitForMouseRelease;

        // ── Input System ──
        private PlayerInputActions _input;

        // ── 호버 인디케이터 (인게임 EnsureBrushPreview와 동일 사양) ──
        private Texture2D _hoverTexture;
        private Color32[] _hoverClearBuffer;
        private int _lastHoverPixelX = -1;
        private int _lastHoverPixelY = -1;

        // ── Flood Fill 스크래치 버퍼 ──
        private bool[] _floodVisited;
        private readonly Stack<int> _floodStack = new Stack<int>(256);
        private readonly List<int> _floodResult = new List<int>(256);

        // ── Undo / Redo ──
        private const int MaxHistory = 50;
        private readonly List<Color32[]> _undoStack = new();
        private readonly List<Color32[]> _redoStack = new();
        private Color32[] _strokeSnapshot;
        private bool _strokeActive;

        // ── Presets ──
        private int _activePresetIndex;
        private byte[][] _presetCache;
        private bool _isDefaultPreset;
        private int _defaultImageIndex;
        private Texture2D[] _presetPreviewTextures;   // ← 추가: 유저 프리셋 프리뷰 텍스처

        // --- Tween ---
        private Tween _saveFeedbackTween;

        // ────────────────────── 라이프사이클 ──────────────────────

        public override void Initialize()
        {
            base.Initialize();

            // Input System
            if (inputActions != null)
                _input = new PlayerInputActions(inputActions);

            _eyedropperSampler = new ScreenEyedropperSampler(this);
            _eyedropperSampler.OnColorSampled += OnEyedropperColorSampled;

            // ColorPicker
            if (colorPicker != null)
            {
                colorPicker.Initialize();
                colorPicker.OnColorChanged      += OnColorChanged;
                colorPicker.OnToolButtonClicked  += OnToolChanged;
                colorPicker.OnBrushSizeChanged   += OnBrushSizeChanged;
                colorPicker.OnUndoButtonClicked  += Undo;
                colorPicker.OnRedoButtonClicked  += Redo;
            }

            if (clearButton != null) clearButton.AddClickListener(OnClickClear);
            if (saveButton != null) saveButton.AddClickListener(OnClickSave);
            if (closeButton != null) closeButton.AddClickListener(OnClickBack);

            if (presetSlots != null)
            {
                for (int i = 0; i < presetSlots.Length; i++)
                {
                    int index = i;
                    if (presetSlots[i].button != null)
                        presetSlots[i].button.AddClickListener(() => OnPresetSelected(index));
                }
            }
            if (defaultPresetSlots != null)
            {
                for (int i = 0; i < defaultPresetSlots.Length; i++)
                {
                    int index = i;
                    if (defaultPresetSlots[i].button != null)
                        defaultPresetSlots[i].button.AddClickListener(() => OnDefaultPresetSelected(index));
                }
            }
            SetupCanvasDrag();
        }

        public override void OnBeforeShow()
        {
            base.OnBeforeShow();
            _input?.EnableAll();
            CancelEyedropper();

            LoadPresetsAndSelect();

            _brushMode = BrushMode.Brush;
            _brushSize = 1;
            _isEyedropperActive = false;
            _waitForMouseRelease = false;
            _eyedropperSampler?.RestoreEventSystem();
            RefreshCursor();

            if (colorPicker != null)
            {
                colorPicker.SetColor(Color.white);
                colorPicker.SetActiveTool(BrushMode.Brush);
                colorPicker.SetBrushSize(1);
            }

            if (panel != null) panel.localScale = Vector3.zero;

            _saveFeedbackTween?.Kill();
            if (saveFeedbackLabel != null)
                saveFeedbackLabel.gameObject.SetActive(false);
        }

        public override void OnAfterHide()
        {
            base.OnAfterHide();
            _input?.DisableAll();
            ClearHoverOverlay();
            CancelEyedropper();
            ResetCursor();

            // ── 유저 프리셋이 선택되었는데 빈 캔버스면 기본 프리셋 0번으로 전환 및 저장 ──
            if (!_isDefaultPreset && IsCanvasEmpty())
            {
                PixelNicknameService.SaveIsDefaultPreset(true);
                PixelNicknameService.SaveDefaultImageIndex(0);

                if (defaultImages != null && defaultImages.Length > 0 && defaultImages[0] != null)
                {
                    var readable = CopyToReadable(defaultImages[0]);
                    PixelNicknameService.Save(readable.EncodeToPNG());
                    Destroy(readable);
                }
            }
        }

        protected override async UniTask OnShowAnimation()
        {
            if (panel != null)
                await panel.DOScale(Vector3.one, animationDuration)
                    .SetEase(Ease.OutBack).ToUniTask();
        }

        protected override async UniTask OnHideAnimation()
        {
            if (panel != null)
                await panel.DOScale(Vector3.zero, animationDuration)
                    .SetEase(Ease.InBack).ToUniTask();
        }

        private void OnDestroy()
        {
            CancelEyedropper();
            ResetCursor();
            if (_eyedropperSampler != null)
            {
                _eyedropperSampler.OnColorSampled -= OnEyedropperColorSampled;
                _eyedropperSampler.Dispose();
                _eyedropperSampler = null;
            }

            if (_editTexture != null)   Destroy(_editTexture);
            if (_checkerTexture != null) Destroy(_checkerTexture);
            if (_hoverTexture != null)   Destroy(_hoverTexture);

            CleanupPresetPreviews();

            if (colorPicker != null)
            {
                colorPicker.OnColorChanged      -= OnColorChanged;
                colorPicker.OnToolButtonClicked  -= OnToolChanged;
                colorPicker.OnBrushSizeChanged   -= OnBrushSizeChanged;
                colorPicker.OnUndoButtonClicked  -= Undo;
                colorPicker.OnRedoButtonClicked  -= Redo;
            }
            _saveFeedbackTween?.Kill();
        }

        // ────────────────────── Update ──────────────────────

        private void Update()
        {
            HandleEyedropperInput();
            UpdateHoverIndicator();
            HandleKeyboardShortcuts();
            HandleBrushSizeScroll();
        }

        // ────────────────────── 캔버스 초기화 ──────────────────────

        private void GenerateCheckerboard()
        {
            if (checkerboardBackground == null) return;

            if (_checkerTexture != null)
                Destroy(_checkerTexture);

            _checkerTexture = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color light = new Color(0.9f, 0.9f, 0.9f, 1f);
            Color dark  = new Color(0.7f, 0.7f, 0.7f, 1f);

            for (int y = 0; y < canvasHeight; y++)
                for (int x = 0; x < canvasWidth; x++)
                    _checkerTexture.SetPixel(x, y, (x + y) % 2 == 0 ? light : dark);

            _checkerTexture.Apply();
            checkerboardBackground.texture = _checkerTexture;
        }

        private void InitializeHoverOverlay()
        {
            if (hoverOverlay == null) return;

            if (_hoverTexture != null)
                Destroy(_hoverTexture);

            // 인게임과 동일: 캔버스 픽셀당 SubRes 서브픽셀, Bilinear 필터
            int hoverW = canvasWidth * PaintBrushStyle.SubRes;
            int hoverH = canvasHeight * PaintBrushStyle.SubRes;
            _hoverTexture = new Texture2D(hoverW, hoverH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            _hoverClearBuffer = new Color32[hoverW * hoverH];
            _hoverTexture.SetPixels32(_hoverClearBuffer);
            _hoverTexture.Apply();

            hoverOverlay.texture = _hoverTexture;
            hoverOverlay.raycastTarget = false;
            hoverOverlay.gameObject.SetActive(false);

            _lastHoverPixelX = -1;
            _lastHoverPixelY = -1;
        }

        // ────────────────────── 입력 처리 ──────────────────────

        private void SetupCanvasDrag()
        {
            if (canvasImage == null) return;

            var trigger = canvasImage.gameObject.GetComponent<EventTrigger>();
            if (trigger == null)
                trigger = canvasImage.gameObject.AddComponent<EventTrigger>();

            var pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
            pointerDown.callback.AddListener(data =>
            {
                _isPainting = true;
                BeginStroke();
                HandlePaintInput((PointerEventData)data);
            });
            trigger.triggers.Add(pointerDown);

            var drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
            drag.callback.AddListener(data => HandlePaintInput((PointerEventData)data));
            trigger.triggers.Add(drag);

            var pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
            pointerUp.callback.AddListener(_ =>
            {
                _isPainting = false;
                EndStroke();
            });
            trigger.triggers.Add(pointerUp);
        }

        private void HandlePaintInput(PointerEventData eventData)
        {
            if (_editTexture == null || canvasImage == null) return;
            if (!_isPainting) return;

            RectTransform rt = canvasImage.rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rt, eventData.position, eventData.pressEventCamera, out Vector2 localPoint))
                return;

            Rect rect = rt.rect;
            float u = (localPoint.x - rect.x) / rect.width;
            float v = (localPoint.y - rect.y) / rect.height;

            if (u < 0f || u >= 1f || v < 0f || v >= 1f) return;

            int px = Mathf.FloorToInt(u * canvasWidth);
            int py = Mathf.FloorToInt(v * canvasHeight);

            switch (_brushMode)
            {
                case BrushMode.Brush:
                    PaintPixels(px, py, _currentColor);
                    break;
                case BrushMode.Eraser:
                    PaintPixels(px, py, Color.clear);
                    break;
                case BrushMode.Eyedropper:
                    break;
                case BrushMode.Fill:
                    FloodFill(px, py, _currentColor);
                    break;
            }
        }

        // ────────────────────── 키보드 단축키 (InputSystem) ──────────────────────

        private void HandleKeyboardShortcuts()
        {
            if (_input == null) return;
            if (_isEyedropperActive) return;

            // 드래그 중에는 Undo/Redo/도구전환 막음
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) return;

            int history = PaintShortcuts.ReadHistory(_input);
            if (history > 0) Redo();
            else if (history < 0) Undo();

            if (PaintShortcuts.TryReadTool(_input, out BrushMode mode))
                OnToolChanged(mode);
        }

        private void HandleBrushSizeScroll()
        {
            if (_input?.BrushSizeModifier == null) return;
            if (!_input.BrushSizeModifier.IsPressed()) return;
            if (Mouse.current == null) return;

            float scroll = Mouse.current.scroll.y.ReadValue();
            if (Mathf.Abs(scroll) < 0.01f) return;
            int delta = PaintShortcuts.ReadSizeDelta(scroll);
            if (delta == 0) return;

            int max = colorPicker != null ? colorPicker.MaxBrushSize : 5;
            int next = _brushSize + delta;
            next = Mathf.Clamp(next, 1, max);

            if (next != _brushSize)
            {
                _brushSize = next;
                colorPicker?.SetBrushSize(next);
                _lastHoverPixelX = -1; // 호버 크기 갱신
            }
        }

        // ────────────────────── 그리기 ──────────────────────

        private void PaintPixels(int cx, int cy, Color color)
        {
            if (PaintOps.GetBrushBounds(cx, cy, _brushSize, canvasWidth, canvasHeight,
                out int x0, out int y0, out int x1, out int y1))
            {
                for (int y = y0; y < y1; y++)
                {
                    for (int x = x0; x < x1; x++)
                        _editTexture.SetPixel(x, y, color);
                }
            }

            _editTexture.Apply();
        }

        private void FloodFill(int startX, int startY, Color fillColor)
        {
            if (startX < 0 || startX >= canvasWidth || startY < 0 || startY >= canvasHeight)
                return;

            Color32[] pixels = _editTexture.GetPixels32();
            int startIdx = startY * canvasWidth + startX;
            Color32 fill = fillColor;
            if (PaintColorUtil.Equals(pixels[startIdx], fill)) return;

            int cellCount = canvasWidth * canvasHeight;
            if (_floodVisited == null || _floodVisited.Length < cellCount)
                _floodVisited = new bool[cellCount];

            int changedCount = PaintOps.CollectFloodRegion(
                pixels,
                canvasWidth,
                canvasHeight,
                startIdx,
                null,
                // 픽셀 아트의 경계를 보존하도록 인게임과 같은 Color32 정확 비교를 사용한다.
                0,
                _floodVisited,
                _floodStack,
                _floodResult);

            for (int i = 0; i < changedCount; i++)
                pixels[_floodResult[i]] = fill;

            _editTexture.SetPixels32(pixels);
            _editTexture.Apply();
        }

        // ────────────────────── Undo / Redo ──────────────────────

        private Color32[] CapturePixels()
        {
            return _editTexture.GetPixels32();
        }

        private void ApplyPixels(Color32[] pixels)
        {
            _editTexture.SetPixels32(pixels);
            _editTexture.Apply();
        }

        private void PushUndo(Color32[] snapshot)
        {
            _undoStack.Add(snapshot);
            if (_undoStack.Count > MaxHistory)
                _undoStack.RemoveAt(0);
        }

        private void ClearHistory()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            _strokeSnapshot = null;
            _strokeActive = false;
        }

        // 스트로크 경계 (PointerDown → PointerUp = 1 스트로크 = 1 Undo 단위)

        private void BeginStroke()
        {
            if (_strokeActive) return;
            _strokeSnapshot = CapturePixels();
            _strokeActive = true;
        }

        private void EndStroke()
        {
            if (!_strokeActive) return;
            _strokeActive = false;

            if (_strokeSnapshot == null) return;

            Color32[] current = CapturePixels();
            if (!PixelsEqual(_strokeSnapshot, current))
            {
                PushUndo(_strokeSnapshot);
                _redoStack.Clear();
            }
            _strokeSnapshot = null;
        }

        private void Undo()
        {
            if (_undoStack.Count == 0) return;
            EndStroke();

            _redoStack.Add(CapturePixels());
            Color32[] prev = _undoStack[_undoStack.Count - 1];
            _undoStack.RemoveAt(_undoStack.Count - 1);
            ApplyPixels(prev);
        }

        private void Redo()
        {
            if (_redoStack.Count == 0) return;
            EndStroke();

            PushUndo(CapturePixels());
            Color32[] next = _redoStack[_redoStack.Count - 1];
            _redoStack.RemoveAt(_redoStack.Count - 1);
            ApplyPixels(next);
        }

        private static bool PixelsEqual(Color32[] a, Color32[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (!PaintColorUtil.Equals(a[i], b[i]))
                    return false;
            }
            return true;
        }

        // ────────────────────── 호버 인디케이터 ──────────────────────

        private void UpdateHoverIndicator()
        {
            if (_hoverTexture == null || canvasImage == null || hoverOverlay == null) return;

            // 스포이드 모드에서는 호버 표시 안 함
            if (_brushMode == BrushMode.Eyedropper)
            {
                ClearHoverOverlay();
                return;
            }

            if (Mouse.current == null)
            {
                ClearHoverOverlay();
                return;
            }

            Vector2 mousePos = Mouse.current.position.ReadValue();
            RectTransform rt = canvasImage.rectTransform;
            Camera cam = canvasImage.canvas != null ? canvasImage.canvas.worldCamera : null;

            if (!RectTransformUtility.RectangleContainsScreenPoint(rt, mousePos, cam))
            {
                ClearHoverOverlay();
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rt, mousePos, cam, out Vector2 localPoint);

            Rect rect = rt.rect;
            float u = (localPoint.x - rect.x) / rect.width;
            float v = (localPoint.y - rect.y) / rect.height;

            int px = Mathf.Clamp(Mathf.FloorToInt(u * canvasWidth), 0, canvasWidth - 1);
            int py = Mathf.Clamp(Mathf.FloorToInt(v * canvasHeight), 0, canvasHeight - 1);

            // 같은 픽셀이면 다시 그리지 않음
            if (px == _lastHoverPixelX && py == _lastHoverPixelY) return;
            _lastHoverPixelX = px;
            _lastHoverPixelY = py;

            // 채우기 모드는 단일 픽셀 표시
            int displaySize = _brushMode == BrushMode.Fill ? 1 : _brushSize;
            DrawBrushOutline(px, py, displaySize);
        }

        private void DrawBrushOutline(int cx, int cy, int size)
        {
            PaintBrushStyle.StampBrush(
                _hoverTexture,
                _hoverClearBuffer,
                cx,
                cy,
                size,
                canvasWidth,
                canvasHeight);

            if (!hoverOverlay.gameObject.activeSelf)
                hoverOverlay.gameObject.SetActive(true);
        }

        private void ClearHoverOverlay()
        {
            _lastHoverPixelX = -1;
            _lastHoverPixelY = -1;

            if (hoverOverlay != null && hoverOverlay.gameObject.activeSelf)
                hoverOverlay.gameObject.SetActive(false);
        }

        // ────────────────────── UIColorPicker 이벤트 ──────────────────────

        private void OnColorChanged(Color color)
        {
            _currentColor = color;

            // 통 커서를 쓰는 중이면 물감 색이 바로 따라오도록 한다.
            if (CursorManager.Instance != null)
                CursorManager.Instance.SetPaintColor(color);
        }

        private void OnToolChanged(BrushMode mode)
        {
            // 스포이드는 Space 홀드로만 진입하는 임시 도구다.
            if (mode == BrushMode.Eyedropper) return;

            if (_isEyedropperActive)
                EndEyedropper(restoreEventSystem: true);

            _brushMode = mode;
            colorPicker?.SetActiveTool(mode);
            _lastHoverPixelX = -1; // 도구 변경 시 호버 갱신

            RefreshCursor();
        }

        private void OnBrushSizeChanged(int size)
        {
            _brushSize = size;
            _lastHoverPixelX = -1; // 크기 변경 시 호버 갱신
            RefreshCursor();
        }

        private void RefreshCursor()
        {
            PaintToolCursor.Apply(_brushMode, _brushSize, _currentColor);
        }

        private static void ResetCursor()
        {
            if (CursorManager.Instance != null)
                CursorManager.Instance.SetCursorType(CursorType.Default);
        }

        private void HandleEyedropperInput()
        {
            InputAction action = _input?.HoldEyedropper;
            if (action == null) return;

            if (_waitForMouseRelease)
            {
                if (Mouse.current == null || !Mouse.current.leftButton.isPressed)
                {
                    _waitForMouseRelease = false;
                    _eyedropperSampler?.RestoreEventSystem();
                }
                return;
            }

            if (!_isEyedropperActive)
            {
                if (action.WasPressedThisFrame())
                    BeginEyedropper();
                return;
            }

            if (Mouse.current != null
                && Mouse.current.leftButton.wasPressedThisFrame
                && _eyedropperSampler.HasValidSample)
            {
                ConfirmEyedropper(waitForMouseRelease: true);
                return;
            }

            if (!action.WasReleasedThisFrame()) return;

            if (_eyedropperSampler.HasValidSample)
                ConfirmEyedropper(waitForMouseRelease: false);
            else
                EndEyedropper(restoreEventSystem: true);
        }

        private void BeginEyedropper()
        {
            if (_isEyedropperActive) return;

            _isPainting = false;
            EndStroke();
            _eyedropperReturnMode = _brushMode == BrushMode.Eyedropper
                ? BrushMode.Brush
                : _brushMode;
            _brushMode = BrushMode.Eyedropper;
            _isEyedropperActive = true;
            _eyedropperSampler?.Start(_currentColor);
            ClearHoverOverlay();
            RefreshCursor();
        }

        private void ConfirmEyedropper(bool waitForMouseRelease)
        {
            _currentColor = _eyedropperSampler.CurrentColor;
            colorPicker?.SetColor(_currentColor);
            _waitForMouseRelease = waitForMouseRelease;
            EndEyedropper(restoreEventSystem: !waitForMouseRelease);
        }

        private void EndEyedropper(bool restoreEventSystem)
        {
            if (!_isEyedropperActive) return;

            _eyedropperSampler?.Stop(restoreEventSystem);
            _isEyedropperActive = false;
            _brushMode = _eyedropperReturnMode;
            colorPicker?.SetActiveTool(_brushMode);
            _lastHoverPixelX = -1;
            RefreshCursor();
        }

        private void CancelEyedropper()
        {
            if (_isEyedropperActive)
                EndEyedropper(restoreEventSystem: true);

            if (_waitForMouseRelease)
            {
                _waitForMouseRelease = false;
                _eyedropperSampler?.RestoreEventSystem();
            }
        }

        private void OnEyedropperColorSampled(Color color)
        {
            _currentColor = color;
            colorPicker?.SetEyedropperPreview(color);

            if (CursorManager.Instance != null)
                CursorManager.Instance.UpdateEyedropperPreview(color);
        }

        // ────────────────────── 버튼 ──────────────────────

        private void OnClickClear()
        {
            if (_editTexture == null) return;
            if (_isDefaultPreset) return;

            Color32[] before = CapturePixels();
            _editTexture.SetPixels32(new Color32[canvasWidth * canvasHeight]);
            _editTexture.Apply();

            Color32[] after = CapturePixels();
            if (!PixelsEqual(before, after))
            {
                PushUndo(before);
                _redoStack.Clear();
            }
        }

        private void OnClickSave()
        {
            if (_isDefaultPreset)
            {
                PixelNicknameService.SaveIsDefaultPreset(true);
                PixelNicknameService.SaveDefaultImageIndex(_defaultImageIndex);

                // 기본 프리셋 원본을 메인 키에 저장 (네트워크 전송용)
                if (defaultImages != null && _defaultImageIndex < defaultImages.Length
                    && defaultImages[_defaultImageIndex] != null)
                {
                    var readable = CopyToReadable(defaultImages[_defaultImageIndex]);
                    PixelNicknameService.Save(readable.EncodeToPNG());
                    Destroy(readable);
                }
            }
            else if (_editTexture != null)
            {
                byte[] png = _editTexture.EncodeToPNG();

                PixelNicknameService.SaveIsDefaultPreset(false);
                PixelNicknameService.SavePreset(_activePresetIndex, png);
                PixelNicknameService.SaveActivePresetIndex(_activePresetIndex);

                // 메인 키 업데이트 (네트워크 호환)
                PixelNicknameService.Save(png);

                // ── 프리셋 프리뷰 즉시 갱신 ──
                _presetCache[_activePresetIndex] = png;
                RefreshUserPresetPreview(_activePresetIndex);
            }

            ShowSaveFeedback();
        }

        private void ShowSaveFeedback()
        {
            if (saveFeedbackLabel == null) return;

            _saveFeedbackTween?.Kill();

            var rt = saveFeedbackLabel.rectTransform;

            saveFeedbackLabel.alpha = 0f;
            rt.localScale = Vector3.one * 0.8f;
            saveFeedbackLabel.gameObject.SetActive(true);

            _saveFeedbackTween = DOTween.Sequence()
                .Append(DOTween.To(() => saveFeedbackLabel.alpha, a => saveFeedbackLabel.alpha = a, 1f, 0.25f)
                    .SetEase(Ease.OutQuad))
                .Join(rt.DOScale(1f, 0.3f).SetEase(Ease.OutBack))
                .AppendInterval(1.5f)
                .Append(DOTween.To(() => saveFeedbackLabel.alpha, a => saveFeedbackLabel.alpha = a, 0f, 0.4f)
                    .SetEase(Ease.InQuad))
                .OnComplete(() => saveFeedbackLabel.gameObject.SetActive(false));
        }


        // ────────────────────── 프리셋 ──────────────────────

        private void LoadPresetsAndSelect()
        {
            _presetCache = new byte[PixelNicknameService.PresetCount][];
            for (int i = 0; i < PixelNicknameService.PresetCount; i++)
                _presetCache[i] = PixelNicknameService.LoadPreset(i);

            _isDefaultPreset = PixelNicknameService.LoadIsDefaultPreset();
            _defaultImageIndex = PixelNicknameService.LoadDefaultImageIndex();
            _activePresetIndex = PixelNicknameService.LoadActivePresetIndex();

            if (_isDefaultPreset)
                ApplyDefaultToCanvas(_defaultImageIndex);
            else
                ApplyPresetToCanvas(_activePresetIndex);

            GenerateCheckerboard();
            InitializeHoverOverlay();

            RefreshDefaultPresetPreviews(); 
            RefreshAllUserPresetPreviews(); 
            UpdatePresetToggleVisual();
            UpdateEditingLock();
        }

        private void OnDefaultPresetSelected(int index)
        {
            // 이미 같은 기본 프리셋이 선택된 상태면 무시
            if (_isDefaultPreset && _defaultImageIndex == index) return;

            // 유저 프리셋에서 전환하는 경우: 현재 캔버스를 캐시에 보관
            if (!_isDefaultPreset && _editTexture != null)
            {
                // 빈 캔버스는 유저 프리셋에 저장하지 않음
                if (!IsCanvasEmpty())
                {
                    _presetCache[_activePresetIndex] = _editTexture.EncodeToPNG();
                    RefreshUserPresetPreview(_activePresetIndex);
                }
            }

            _isDefaultPreset = true;
            _defaultImageIndex = index;

            ApplyDefaultToCanvas(index);
            UpdatePresetToggleVisual();
            UpdateEditingLock();
        }

        private void OnPresetSelected(int index)
        {
            if (_isDefaultPreset)
            {
                _isDefaultPreset = false;
                _activePresetIndex = index;
                ApplyPresetToCanvas(index);
                UpdatePresetToggleVisual();
                UpdateEditingLock();
                return;
            }

            if (index == _activePresetIndex) return;

            // 현재 캔버스 작업물을 기존 프리셋 캐시에 임시 보관
            int prevIndex = _activePresetIndex;
            if (_editTexture != null)
                _presetCache[prevIndex] = _editTexture.EncodeToPNG();

            RefreshUserPresetPreview(prevIndex);   // ← 추가: 떠나는 프리셋 프리뷰 갱신

            _activePresetIndex = index;
            ApplyPresetToCanvas(index);
            UpdatePresetToggleVisual();
            UpdateEditingLock();
        }

        private void ApplyDefaultToCanvas(int imageIndex)
        {
            if (defaultImages == null || defaultImages.Length == 0) return;

            imageIndex = Mathf.Clamp(imageIndex, 0, defaultImages.Length - 1);
            Texture2D source = defaultImages[imageIndex];
            if (source == null) return;

            InitializeCanvasTexture();

            int copyW = Mathf.Min(source.width, canvasWidth);
            int copyH = Mathf.Min(source.height, canvasHeight);
            for (int y = 0; y < copyH; y++)
                for (int x = 0; x < copyW; x++)
                    _editTexture.SetPixel(x, y, source.GetPixel(x, y));

            _editTexture.Apply();
            canvasImage.texture = _editTexture;
            if (previewImage != null)
                previewImage.texture = _editTexture;

            ClearHistory();
        }

        private void ApplyPresetToCanvas(int index)
        {
            InitializeCanvasTexture();

            byte[] data = _presetCache[index];
            if (data != null && data.Length > 0)
            {
                var temp = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (temp.LoadImage(data))
                {
                    int copyW = Mathf.Min(temp.width, canvasWidth);
                    int copyH = Mathf.Min(temp.height, canvasHeight);
                    for (int y = 0; y < copyH; y++)
                        for (int x = 0; x < copyW; x++)
                            _editTexture.SetPixel(x, y, temp.GetPixel(x, y));
                }
                Destroy(temp);
            }

            _editTexture.Apply();
            canvasImage.texture = _editTexture;
            if (previewImage != null)
                previewImage.texture = _editTexture;

            ClearHistory();
        }

        private void UpdateEditingLock()
        {
            if (canvasImage != null)
                canvasImage.raycastTarget = !_isDefaultPreset;
        }

        private void UpdatePresetToggleVisual()
        {
            // 유저 프리셋 버튼
            if (presetSlots != null)
            {
                for (int i = 0; i < presetSlots.Length; i++)
                {
                    if (presetSlots[i].button == null) continue;
                    var img = presetSlots[i].button.GetComponent<Image>();
                    if (img != null)
                    {
                        bool active = !_isDefaultPreset && i == _activePresetIndex;
                        img.color = active ? presetActiveColor : presetInactiveColor;
                    }
                }
            }

            // 기본 프리셋 버튼
            if (defaultPresetSlots != null)
            {
                for (int i = 0; i < defaultPresetSlots.Length; i++)
                {
                    if (defaultPresetSlots[i].button == null) continue;
                    var img = defaultPresetSlots[i].button.GetComponent<Image>();
                    if (img != null)
                    {
                        bool active = _isDefaultPreset && i == _defaultImageIndex;
                        img.color = active ? presetActiveColor : presetInactiveColor;
                    }
                }
            }
        }

        private bool IsCanvasEmpty()
        {
            if (_editTexture == null) return true;

            Color32[] pixels = _editTexture.GetPixels32();
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i].a > 0)
                    return false;
            }
            return true;
        }

        private void InitializeCanvasTexture()
        {
            if (_editTexture != null)
                Destroy(_editTexture);

            _editTexture = new Texture2D(canvasWidth, canvasHeight, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            _editTexture.SetPixels32(new Color32[canvasWidth * canvasHeight]);
            _editTexture.Apply();
        }

        // ────────────────────── 프리셋 프리뷰 ──────────────────────

        /// <summary>
        /// 기본 프리셋 프리뷰: defaultImages[i]를 직접 할당 (텍스처 생성 불필요)
        /// </summary>
        private void RefreshDefaultPresetPreviews()
        {
            if (defaultPresetSlots == null || defaultImages == null) return;

            for (int i = 0; i < defaultPresetSlots.Length; i++)
            {
                if (defaultPresetSlots[i].preview == null) continue;

                if (i < defaultImages.Length && defaultImages[i] != null)
                {
                    defaultPresetSlots[i].preview.texture = defaultImages[i];
                    defaultPresetSlots[i].preview.uvRect = new Rect(0, 0, 1, 1);
                    defaultPresetSlots[i].preview.enabled = true;
                }
                else
                {
                    defaultPresetSlots[i].preview.texture = null;
                    defaultPresetSlots[i].preview.enabled = false;
                }
            }
        }

        /// <summary>
        /// 유저 프리셋 프리뷰 전체 갱신: _presetCache[i]의 PNG → Texture2D
        /// </summary>
        private void RefreshAllUserPresetPreviews()
        {
            if (presetSlots == null) return;

            if (_presetPreviewTextures == null)
                _presetPreviewTextures = new Texture2D[presetSlots.Length];

            for (int i = 0; i < presetSlots.Length; i++)
                RefreshUserPresetPreview(i);
        }

        /// <summary>
        /// 특정 유저 프리셋 프리뷰만 갱신
        /// </summary>
        private void RefreshUserPresetPreview(int index)
        {
            if (presetSlots == null || _presetPreviewTextures == null) return;
            if (index < 0 || index >= presetSlots.Length) return;
            if (presetSlots[index].preview == null) return;

            // 기존 텍스처 정리
            if (_presetPreviewTextures[index] != null)
                Destroy(_presetPreviewTextures[index]);

            byte[] data = (index < _presetCache.Length) ? _presetCache[index] : null;

            if (data != null && data.Length > 0)
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                tex.LoadImage(data);
                _presetPreviewTextures[index] = tex;
                presetSlots[index].preview.texture = tex;
                presetSlots[index].preview.uvRect = new Rect(0, 0, 1, 1);
                presetSlots[index].preview.enabled = true;
            }
            else
            {
                _presetPreviewTextures[index] = null;
                presetSlots[index].preview.texture = null;
                presetSlots[index].preview.enabled = false;
            }
        }

        /// <summary>
        /// 프리뷰 텍스처 메모리 정리
        /// </summary>
        private void CleanupPresetPreviews()
        {
            if (_presetPreviewTextures == null) return;
            for (int i = 0; i < _presetPreviewTextures.Length; i++)
            {
                if (_presetPreviewTextures[i] != null)
                    Destroy(_presetPreviewTextures[i]);
            }
            _presetPreviewTextures = null;
        }

        /// <summary>
        /// 압축 텍스처를 RGBA32로 복사하여 EncodeToPNG 호출이 가능하게 만든다.
        /// </summary>
        private static Texture2D CopyToReadable(Texture2D source)
        {
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(source, rt);

            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            var readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readable.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readable.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            return readable;
        }
    }
}
