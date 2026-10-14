using System;
using PixelChroma.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PixelChroma
{
    public enum BrushMode { Brush, Eraser, Eyedropper, Fill }

    public class PlayerPaintController
    {
        private readonly PlayerBase _owner;
        private readonly PlayerPaintCanvas _paintCanvas;
        private readonly PlayerInputActions _input;

        private UIColorPicker _boundPicker;
        private IPaintHUD _boundHUD;
        private BrushMode _brushMode = BrushMode.Brush;
        private bool _isPaintMode;

        private Color _eyedropperColor;
        private readonly ScreenEyedropperSampler _eyedropperSampler;
        private BrushMode _eyedropperReturnMode = BrushMode.Brush;
        private bool _isEyedropperActive;
        private bool _waitForMouseRelease;

        private Camera _cachedCamera;
        private GameObject _brushPreview;
        private SpriteRenderer _brushPreviewRenderer;
        private int _previewLastSize = -1;

        public event Action<bool> OnPaintModeChanged;
        public event Action<int> OnBrushSizeScrolled;

        public bool IsPaintMode => _isPaintMode;
        public BrushMode BrushMode => _brushMode;

        public PlayerPaintController(
            PlayerBase owner,
            PlayerPaintCanvas paintCanvas,
            PlayerInputActions input)
        {
            _owner = owner;
            _paintCanvas = paintCanvas;
            _input = input;
            _cachedCamera = Camera.main;
            _eyedropperSampler = new ScreenEyedropperSampler(owner);
            _eyedropperSampler.OnColorSampled += OnEyedropperColorSampled;
        }

        public void Tick()
        {
            HandleEyedropperInput();
            HandleModeInput();

            if (_isEyedropperActive)
            {
                HideBrushPreview();
                return;
            }

            HandleHistoryInput();

            UpdateBrushPreview();
            HandlePainting();
        }

        public void Dispose()
        {
            CancelTransientTool();
            _eyedropperSampler.OnColorSampled -= OnEyedropperColorSampled;
            _eyedropperSampler.Dispose();

            // 플레이어 교체 시에는 새 로컬 플레이어가 이미 커서를 넘겨받았을 수 있다.
            // 이전 플레이어의 정리가 새 역할의 커서를 덮어쓰지 않도록 현재 로컬 플레이어일 때만 초기화한다.
            if (_owner != null
                && _owner.isLocalPlayer
                && Mirror.NetworkClient.localPlayer == _owner.netIdentity
                && CursorManager.Instance != null)
                CursorManager.Instance.SetCursorType(CursorType.Default);

            if (_brushPreview != null)
            {
                if (_brushPreviewRenderer != null && _brushPreviewRenderer.sprite != null)
                    UnityEngine.Object.Destroy(_brushPreviewRenderer.sprite.texture);
                UnityEngine.Object.Destroy(_brushPreview);
                _brushPreview = null;
                _brushPreviewRenderer = null;
                _previewLastSize = -1;
            }
        }

        public void BindHUD(IPaintHUD hud)
        {
            if (hud == null) return;

            _boundHUD = hud;
            _boundPicker = hud.ColorPicker;
            OnPaintModeChanged += hud.SetPaintMode;

            if (_boundPicker != null)
            {
                OnBrushSizeScrolled += _boundPicker.SetBrushSize;
                _boundPicker.OnBrushSizeChanged += OnUIBrushSizeChanged;
                _boundPicker.OnColorChanged += OnUIColorChanged;
                _boundPicker.OnToolButtonClicked += OnUIToolButtonClicked;
                _boundPicker.OnUndoButtonClicked += OnUIUndoClicked;
                _boundPicker.OnRedoButtonClicked += OnUIRedoClicked;
                if (_paintCanvas != null)
                    _paintCanvas.BrushSize = _boundPicker.BrushSize;
            }
        }

        public void UnbindHUD()
        {
            CancelTransientTool();
            _boundHUD?.SetPaintMode(false);

            if (_boundPicker != null)
            {
                OnBrushSizeScrolled -= _boundPicker.SetBrushSize;
                _boundPicker.OnBrushSizeChanged -= OnUIBrushSizeChanged;
                _boundPicker.OnColorChanged -= OnUIColorChanged;
                _boundPicker.OnToolButtonClicked -= OnUIToolButtonClicked;
                _boundPicker.OnUndoButtonClicked -= OnUIUndoClicked;
                _boundPicker.OnRedoButtonClicked -= OnUIRedoClicked;
            }

            OnPaintModeChanged = null;
            OnBrushSizeScrolled = null;
            _boundPicker = null;
            _boundHUD = null;
        }

        public void SetBrushMode(BrushMode mode)
        {
            // 스포이드는 Space 홀드로만 진입하는 임시 도구다.
            if (mode == BrushMode.Eyedropper) return;

            if (_isEyedropperActive)
                EndEyedropper(restoreEventSystem: true);

            if (_brushMode == mode) return;

            _paintCanvas?.EndStroke();   // 도구 전환 시 진행 중 스트로크 확정

            _brushMode = mode;

            _boundPicker?.SetActiveTool(mode);
            RefreshCursor();
        }

        public void TogglePaintMode()
        {
            _isPaintMode = !_isPaintMode;
            OnPaintModeChanged?.Invoke(_isPaintMode);

            if (!_isPaintMode)
            {
                _paintCanvas?.EndStroke();
                CancelTransientTool();

                HideBrushPreview();
            }

            RefreshCursor();
        }

        public void HandleBrushScroll(float scroll)
        {
            if (_boundPicker == null || _paintCanvas == null) return;

            int delta = PaintShortcuts.ReadSizeDelta(scroll);
            if (delta == 0) return;

            int max = _boundPicker.MaxBrushSize;
            int current = _paintCanvas.BrushSize;
            int next = current + delta;
            next = Mathf.Clamp(next, 1, max);
            _paintCanvas.BrushSize = next;
            OnBrushSizeScrolled?.Invoke(next);
        }

        public void RefreshCursor()
        {
            if (!_owner.isLocalPlayer || CursorManager.Instance == null) return;

            if (!_isPaintMode)
            {
                CursorManager.Instance.SetCursorType(_owner.GetDefaultCursorType());
                return;
            }

            int brushSize = _boundPicker != null ? _boundPicker.BrushSize : 1;
            Color paintColor = _boundPicker != null ? _boundPicker.SelectedColor : Color.white;
            PaintToolCursor.Apply(_brushMode, brushSize, paintColor);
        }

        public void HidePreview()
        {
            HideBrushPreview();
        }

        public void CancelTransientTool()
        {
            if (_isEyedropperActive)
                EndEyedropper(restoreEventSystem: true);

            if (_waitForMouseRelease)
            {
                _waitForMouseRelease = false;
                _eyedropperSampler.RestoreEventSystem();
            }
        }

        private void HandleHistoryInput()
        {
            if (_paintCanvas == null) return;
            // 드래그(색칠) 중에는 되돌리기를 막는다 — 진행 중 스트로크와 꼬이지 않도록
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) return;

            int history = PaintShortcuts.ReadHistory(_input);
            if (history > 0)
            {
                _paintCanvas.EndStroke();   // 혹시 남은 스트로크가 있으면 먼저 확정
                _paintCanvas.Redo();
            }
            else if (history < 0)
            {
                _paintCanvas.EndStroke();
                _paintCanvas.Undo();
            }
        }

        private void HandleModeInput()
        {
            if (PaintShortcuts.TryReadTool(_input, out BrushMode mode))
                SetBrushMode(mode);
        }

        private void UpdateBrushPreview()
        {
            if (_owner == null || !_owner.isLocalPlayer || !_isPaintMode || _brushMode == BrushMode.Fill)
            {
                HideBrushPreview();
                return;
            }

            // UI 위에서는 색칠이 막히므로 프리뷰도 숨김
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                HideBrushPreview();
                return;
            }

            if (!TryGetMousePixel(out var pixel))
            {
                HideBrushPreview();
                return;
            }

            int size = _paintCanvas.BrushSize;
            EnsureBrushPreview(size);

            Vector3 center = _paintCanvas.GetBrushWorldCenter(pixel.x, pixel.y, size);
            center.z = 0f;
            float worldSize = _paintCanvas.GetBrushWorldSize(size);

            _brushPreview.transform.position = center;
            _brushPreview.transform.localScale = new Vector3(worldSize, worldSize, 1f);

            _brushPreviewRenderer.color = _brushMode == BrushMode.Eraser
                ? PaintBrushStyle.EraserFill   // 지우개: 빨간 반투명 (외곽선은 텍스처에 내장)
                : Color.white; // 브러시: 텍스처 원색 그대로 (회색 내부 + 검정 외곽선)

            if (!_brushPreview.activeSelf)
                _brushPreview.SetActive(true);
        }

        private void EnsureBrushPreview(int size)
        {
            if (_brushPreview == null)
            {
                _brushPreview = new GameObject("BrushAreaPreview");
                _brushPreviewRenderer = _brushPreview.AddComponent<SpriteRenderer>();
                _brushPreviewRenderer.sortingLayerID = _paintCanvas.SortingLayerID;
                _brushPreviewRenderer.sortingOrder = _paintCanvas.SortingOrder + 1000;
                _brushPreview.SetActive(false);
                _previewLastSize = -1;
            }

            if (_previewLastSize == size) return;
            _previewLastSize = size;

            Texture2D tex = PaintBrushStyle.CreateBrushTexture(size);
            int texSize = tex.width;

            if (_brushPreviewRenderer.sprite != null)
                UnityEngine.Object.Destroy(_brushPreviewRenderer.sprite.texture);

            _brushPreviewRenderer.sprite = Sprite.Create(
                tex, new Rect(0f, 0f, texSize, texSize),
                new Vector2(0.5f, 0.5f), texSize);
        }

        private void HideBrushPreview()
        {
            if (_brushPreview != null && _brushPreview.activeSelf)
                _brushPreview.SetActive(false);
        }

        private void HandleEyedropperInput()
        {
            InputAction action = _input?.HoldEyedropper;
            if (action == null) return;

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
            if (!_isPaintMode || _isEyedropperActive) return;

            _paintCanvas?.EndStroke();
            _eyedropperReturnMode = _brushMode == BrushMode.Eyedropper
                ? BrushMode.Brush
                : _brushMode;
            _brushMode = BrushMode.Eyedropper;
            _isEyedropperActive = true;

            Color initialColor = _boundPicker != null ? _boundPicker.SelectedColor : _eyedropperColor;
            _eyedropperSampler.Start(initialColor);
            HideBrushPreview();
            RefreshCursor();
        }

        private void ConfirmEyedropper(bool waitForMouseRelease)
        {
            _eyedropperColor = _eyedropperSampler.CurrentColor;
            _boundPicker?.SetColor(_eyedropperColor);
            _waitForMouseRelease = waitForMouseRelease;
            EndEyedropper(restoreEventSystem: !waitForMouseRelease);
        }

        private void EndEyedropper(bool restoreEventSystem)
        {
            if (!_isEyedropperActive) return;

            _eyedropperSampler.Stop(restoreEventSystem);
            _isEyedropperActive = false;
            _brushMode = _eyedropperReturnMode;
            _boundPicker?.SetActiveTool(_brushMode);
            RefreshCursor();
        }

        private void HandlePainting()
        {
            if (_brushMode == BrushMode.Eyedropper) return;
            if (Mouse.current == null) return;

            // 마우스를 떼면 이번 드래그를 1 스트로크로 확정(히스토리 1건)
            if (Mouse.current.leftButton.wasReleasedThisFrame)
                _paintCanvas.EndStroke();

            if (_waitForMouseRelease)
            {
                if (!Mouse.current.leftButton.isPressed)
                {
                    _waitForMouseRelease = false;
                    _eyedropperSampler.RestoreEventSystem();
                }
                return;
            }

            if (!Mouse.current.leftButton.isPressed) return;
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
            if (!TryGetMousePixel(out var pixel)) return;

            if (_brushMode == BrushMode.Fill)
            {
                if (!Mouse.current.leftButton.wasPressedThisFrame) return;
                if (!_paintCanvas.IsInBounds(pixel.x, pixel.y)) return;

                Color color = _boundPicker != null ? _boundPicker.SelectedColor : Color.white;
                _paintCanvas.BeginStroke();
                _paintCanvas.FloodFill(pixel.x, pixel.y, color);   // 클릭 지점과 이어진 같은 색 영역만
                _paintCanvas.EndStroke();
                return;
            }

            if (_paintCanvas.IsInBounds(pixel.x, pixel.y))
            {
                _paintCanvas.BeginStroke();   // 드래그 첫 프레임에만 실제로 시작(그 외 no-op)
                switch (_brushMode)
                {
                    case BrushMode.Eraser:
                        _paintCanvas.EraseAt(pixel.x, pixel.y);
                        break;
                    case BrushMode.Brush:
                    default:
                        Color color = _boundPicker != null ? _boundPicker.SelectedColor : Color.white;
                        _paintCanvas.PaintAt(pixel.x, pixel.y, color);
                        break;
                }
            }
        }

        private void OnEyedropperColorSampled(Color color)
        {
            _eyedropperColor = color;
            _boundPicker?.SetEyedropperPreview(color);

            if (CursorManager.Instance != null)
                CursorManager.Instance.UpdateEyedropperPreview(color);
        }

        private bool TryGetMousePixel(out Vector2Int pixel)
        {
            pixel = default;

            if (_cachedCamera == null || Mouse.current == null || _paintCanvas == null)
                return false;

            Vector2 screenPos = Mouse.current.position.ReadValue();
            Vector3 worldPos = _cachedCamera.ScreenToWorldPoint(screenPos);
            worldPos.z = 0f;
            pixel = _paintCanvas.WorldToPixel(worldPos);
            return true;
        }

        private void OnUIBrushSizeChanged(int size)
        {
            if (_paintCanvas != null)
                _paintCanvas.BrushSize = size;

            RefreshCursor();
        }

        private void OnUIColorChanged(Color color)
        {
            // 통 커서를 쓰는 중이면 물감 색이 바로 따라오도록 한다.
            if (_owner == null || !_owner.isLocalPlayer || CursorManager.Instance == null) return;
            CursorManager.Instance.SetPaintColor(color);
        }

        private void OnUIToolButtonClicked(BrushMode mode)
        {
            SetBrushMode(mode);
        }

        private void OnUIUndoClicked()
        {
            if (_paintCanvas == null) return;
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) return;
            _paintCanvas.EndStroke();
            _paintCanvas.Undo();
        }

        private void OnUIRedoClicked()
        {
            if (_paintCanvas == null) return;
            if (Mouse.current != null && Mouse.current.leftButton.isPressed) return;
            _paintCanvas.EndStroke();
            _paintCanvas.Redo();
        }
    }
}
