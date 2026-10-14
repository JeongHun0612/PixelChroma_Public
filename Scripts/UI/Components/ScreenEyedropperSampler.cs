using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PixelChroma.UI
{
    public sealed class ScreenEyedropperSampler : IDisposable
    {
        private readonly MonoBehaviour _owner;
        private Texture2D _capture;
        private Coroutine _coroutine;
        private BaseInputModule _cachedInputModule;

        public Color CurrentColor { get; private set; } = Color.white;
        public bool IsRunning => _coroutine != null;
        public bool HasValidSample { get; private set; }

        public event Action<Color> OnColorSampled;

        public ScreenEyedropperSampler(MonoBehaviour owner)
        {
            _owner = owner;
        }

        public void Start(Color initialColor)
        {
            if (_owner == null) return;

            Stop(restoreEventSystem: false);
            CurrentColor = initialColor;
            HasValidSample = false;
            SetEventSystemEnabled(false);
            _coroutine = _owner.StartCoroutine(CaptureLoop());
            OnColorSampled?.Invoke(CurrentColor);
        }

        public void Stop(bool restoreEventSystem)
        {
            if (_coroutine != null && _owner != null)
            {
                _owner.StopCoroutine(_coroutine);
                _coroutine = null;
            }

            if (restoreEventSystem)
                SetEventSystemEnabled(true);
        }

        public void RestoreEventSystem()
        {
            SetEventSystemEnabled(true);
        }

        public void Dispose()
        {
            Stop(restoreEventSystem: true);

            if (_capture != null)
            {
                UnityEngine.Object.Destroy(_capture);
                _capture = null;
            }
        }

        private IEnumerator CaptureLoop()
        {
            if (_capture == null)
                _capture = new Texture2D(1, 1, TextureFormat.RGB24, false);

            while (true)
            {
                yield return new WaitForEndOfFrame();

                if (Mouse.current == null) continue;

                Vector2 screenPos = Mouse.current.position.ReadValue();
                int x = (int)screenPos.x;
                int y = (int)screenPos.y;

                if (x < 0 || x >= Screen.width || y < 0 || y >= Screen.height) continue;

                _capture.ReadPixels(new Rect(x, y, 1, 1), 0, 0);
                _capture.Apply();
                CurrentColor = _capture.GetPixel(0, 0);
                HasValidSample = true;
                OnColorSampled?.Invoke(CurrentColor);
            }
        }

        private void SetEventSystemEnabled(bool enabled)
        {
            if (enabled)
            {
                if (_cachedInputModule != null)
                    _cachedInputModule.enabled = true;
            }
            else
            {
                var es = EventSystem.current;
                if (es == null) return;

                _cachedInputModule = es.currentInputModule != null
                    ? es.currentInputModule
                    : es.GetComponent<BaseInputModule>();

                if (_cachedInputModule != null)
                    _cachedInputModule.enabled = false;
            }
        }
    }
}
