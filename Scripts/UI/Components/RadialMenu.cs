using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;

namespace PixelChroma.UI
{
    public class RadialMenu : MonoBehaviour
    {
        // ── 데이터 ──────────────────────────────────────────
        [System.Serializable]
        public class MenuItem
        {
            public Sprite icon;
        }

        // ── Inspector ───────────────────────────────────────
        [Header("Items")]
        [SerializeField] private MenuItem[] items;

        [Header("Layout")]
        [SerializeField] private float innerRadius = 100f;
        [SerializeField] private float outerRadius = 250f;
        [SerializeField] private float gapAngle = 4f;
        [SerializeField] private float iconSize = 60f;
        [SerializeField] private float deadZone = 50f;

        [Header("Colors")]
        [SerializeField] private Color normalColor = new Color(0.15f, 0.15f, 0.25f, 0.85f);
        [SerializeField] private Color highlightColor = new Color(0.35f, 0.35f, 0.75f, 0.95f);

        [Header("Animation")]
        [SerializeField] private float openDuration = 0.2f;

        // ── 런타임 ──────────────────────────────────────────
        private readonly List<PieSlice> _slices = new();
        private readonly List<Image> _icons = new();
        private int _selectedIndex = -1;
        private int _prevSelectedIndex = -1;
        private int _slotCount;
        private float _stepAngle;
        private bool _isOpen;
        private bool _isBuilt;

        public int SelectedIndex => _selectedIndex;
        public bool IsOpen => _isOpen;
        public event Action<int> OnSelected;

        // ── 초기화 ──────────────────────────────────────────

        private void Awake()
        {
            Build();
            gameObject.SetActive(false);
        }

        private void Build()
        {
            if (_isBuilt) return;
            _isBuilt = true;

            _slotCount = items.Length;
            if (_slotCount == 0) return;

            _stepAngle = 360f / _slotCount;

            for (int i = 0; i < _slotCount; i++)
            {
                CreateSlice(i);
                CreateIcon(i);
            }
        }

        //
        // 좌표계 변환 설명:
        //
        //   메뉴 각도(menu): 0° = 12시(위), 시계방향 증가
        //   수학 각도(math): 0° = 3시(오른쪽), 반시계방향 증가
        //
        //   변환: mathAngle = 90° - menuAngle
        //
        //   메뉴에서 i번째 항목의 중심 = i * stepAngle (메뉴 각도)
        //   시계방향이 반시계로 뒤집히므로 start/end도 뒤집어서 전달
        //

        private void CreateSlice(int index)
        {
            float menuStart = index * _stepAngle - _stepAngle / 2f + gapAngle / 2f;
            float menuEnd = index * _stepAngle + _stepAngle / 2f - gapAngle / 2f;

            // 메뉴→수학 변환 (CW→CCW이므로 start/end 뒤집힘)
            float mathStart = 90f - menuEnd;
            float mathEnd = 90f - menuStart;

            var go = new GameObject($"Slice_{index}");
            go.transform.SetParent(transform, false);

            var slice = go.AddComponent<PieSlice>();
            slice.raycastTarget = false;
            slice.Setup(mathStart, mathEnd, innerRadius, outerRadius);
            slice.color = normalColor;

            _slices.Add(slice);
        }

        private void CreateIcon(int index)
        {
            float menuCenter = index * _stepAngle;
            float mathCenter = (90f - menuCenter) * Mathf.Deg2Rad;
            float midRadius = (innerRadius + outerRadius) / 2f;

            var go = new GameObject($"Icon_{index}");
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.anchoredPosition = new Vector2(
                Mathf.Cos(mathCenter) * midRadius,
                Mathf.Sin(mathCenter) * midRadius
            );
            rect.sizeDelta = new Vector2(iconSize, iconSize);

            var img = go.AddComponent<Image>();
            img.sprite = items[index].icon;
            img.raycastTarget = false;
            img.preserveAspect = true;

            _icons.Add(img);
        }

        // ── 열기 / 닫기 ─────────────────────────────────────

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;
            _selectedIndex = -1;
            _prevSelectedIndex = -1;

            ResetVisuals();

            gameObject.SetActive(true);
            transform.localScale = Vector3.zero;
            transform.DOKill();
            transform.DOScale(Vector3.one, openDuration).SetEase(Ease.OutBack);
        }

        public int Close()
        {
            if (!_isOpen) return -1;
            _isOpen = false;

            transform.DOKill();
            gameObject.SetActive(false);
            transform.localScale = Vector3.one;

            int result = _selectedIndex;
            if (result >= 0)
                OnSelected?.Invoke(result);

            return result;
        }

        // ── 매 프레임 ───────────────────────────────────────

        private void Update()
        {
            if (!_isOpen) return;

            UpdateSelection();

            if (_selectedIndex != _prevSelectedIndex)
            {
                UpdateVisuals();
                _prevSelectedIndex = _selectedIndex;
            }
        }

        private void UpdateSelection()
        {
            if (Mouse.current == null) return;

            // 메뉴 RectTransform의 화면 중심과 마우스 위치의 차이
            Vector2 center = transform.position;
            Vector2 mousePos = Mouse.current.position.ReadValue();
            Vector2 dir = mousePos - center;

            // 데드존 안이면 선택 없음
            if (dir.sqrMagnitude < deadZone * deadZone)
            {
                _selectedIndex = -1;
                return;
            }

            // Atan2(x, y) → 0°가 위(12시), 양수가 시계방향 = 메뉴 각도
            float angle = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;

            _selectedIndex = Mathf.FloorToInt((angle + _stepAngle * 0.5f) % 360f / _stepAngle);
            if (_selectedIndex >= _slotCount) _selectedIndex = 0;
        }

        private void UpdateVisuals()
        {
            for (int i = 0; i < _slices.Count; i++)
                _slices[i].color = (i == _selectedIndex) ? highlightColor : normalColor;
        }

        private void ResetVisuals()
        {
            for (int i = 0; i < _slices.Count; i++)
                _slices[i].color = normalColor;
        }

        public void ForceClose()
        {
            if (!_isOpen) return;
            _isOpen = false;

            transform.DOKill();
            gameObject.SetActive(false);
            transform.localScale = Vector3.one;

            // 선택 결과를 소비하지 않음 (OnSelected 미발동, 반환값 없음)
        }
    }
}