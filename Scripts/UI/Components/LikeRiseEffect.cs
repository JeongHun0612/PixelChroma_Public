using System.Collections.Generic;
using DG.Tweening;
using OneText.UGUI;
using UnityEngine;
using UnityEngine.UI;

namespace PixelChroma
{
    /// <summary>좋아요를 받을 때마다 반투명 아이콘이 하나씩 떠오르고, 카운트 텍스트가 같이 튀어오르는 연출.</summary>
    public class LikeRiseEffect : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("복제해서 띄울 아이콘 원본")]
        [SerializeField] private Image iconTemplate;
        [Tooltip("아이콘이 뜰 때마다 같이 튀어오를 카운트 텍스트. 비워두면 아이콘만 나옵니다.")]
        [SerializeField] private OneTextLabel countText;

        [Header("Burst")]
        [Tooltip("한 번에 띄울 최대 개수. 여러 명이 동시에 누르거나 델타가 크게 들어와도 여기까지만 나옵니다.")]
        [SerializeField] private int maxBurst = 5;
        [Tooltip("연속으로 띄울 때의 간격(초). 0이면 전부 같은 프레임에 겹칩니다.")]
        [SerializeField] private float burstInterval = 0.08f;

        [Header("Motion")]
        [SerializeField] private float riseDistance = 90f;
        [Tooltip("올라가는 데 걸리는 시간 범위. 개체마다 다르게 뽑아 뭉치지 않게 합니다.")]
        [SerializeField] private Vector2 durationRange = new(0.9f, 1.3f);
        [Tooltip("생성 위치 랜덤 오프셋(x, y). x가 곧 좌우로 흩어지는 폭입니다.")]
        [SerializeField] private Vector2 spawnOffsetRange = new(10f, 4f);
        [SerializeField] private Vector2 scaleRange = new(0.75f, 1.15f);
        [Tooltip("생성 시 기울일 최대 각도")]
        [SerializeField] private float maxTilt = 15f;

        [Header("Fade")]
        [Range(0f, 1f)]
        [SerializeField] private float startAlpha = 0.7f;
        [Tooltip("전체 진행률 중 어느 지점부터 사라지기 시작할지")]
        [Range(0f, 1f)]
        [SerializeField] private float fadeStartRatio = 0.45f;

        [Header("Count Text Punch")]
        [Tooltip("텍스트가 커졌다 돌아오는 비율. 0.35면 최대 1.35배까지 커집니다.")]
        [SerializeField] private float punchScale = 0.35f;
        [Tooltip("커지는 데 걸리는 시간(초)")]
        [SerializeField] private float punchInDuration = 0.1f;
        [Tooltip("원래 크기로 돌아오는 데 걸리는 시간(초). 살짝 작아졌다 복귀합니다.")]
        [SerializeField] private float punchOutDuration = 0.25f;
        [Tooltip("펀치하는 순간 입힐 강조 색. 원래 색과 같게 두면 색 변화 없이 크기만 튑니다.")]
        [SerializeField] private Color punchColor = new(1f, 0.42f, 0.55f);

        private readonly Stack<Image> _pool = new();
        private readonly List<Image> _active = new();
        private readonly List<Tween> _running = new();

        private Color _countBaseColor = Color.white;
        private Sequence _countPunch;

        private void Awake()
        {
            if (countText != null)
                _countBaseColor = countText.color;
        }

        /// <summary>좋아요 증가분만큼(최대 maxBurst) 아이콘을 띄우고 카운트 텍스트를 튕깁니다.</summary>
        public void Play(int count)
        {
            if (!isActiveAndEnabled) return;

            int spawnCount = Mathf.Clamp(count, 1, Mathf.Max(1, maxBurst));
            for (int i = 0; i < spawnCount; i++)
            {
                if (i == 0)
                {
                    PlayOne();
                    continue;
                }

                Track(DOVirtual.DelayedCall(i * burstInterval, PlayOne, false));
            }
        }

        private void OnDisable()
        {
            var tweens = _running.ToArray();
            _running.Clear();
            foreach (var tween in tweens)
                tween?.Kill();

            foreach (var icon in _active.ToArray())
                Release(icon);
            _active.Clear();

            ResetCountText();
        }

        // 아이콘 하나가 뜰 때 텍스트도 같이 튕겨야 연출이 한 덩어리로 보입니다.
        private void PlayOne()
        {
            if (!isActiveAndEnabled) return;

            SpawnOne();
            PunchCountText();
        }

        private void PunchCountText()
        {
            if (countText == null) return;

            // 연타로 들어와도 항상 기본 크기에서 다시 시작하게 만듭니다.
            ResetCountText();

            RectTransform rect = countText.rectTransform;
            _countPunch = DOTween.Sequence()
                .Append(rect.DOScale(1f + punchScale, punchInDuration).SetEase(Ease.OutQuad))
                .Join(countText.DOColor(punchColor, punchInDuration))
                .Append(rect.DOScale(1f, punchOutDuration).SetEase(Ease.OutBack))
                .Join(countText.DOColor(_countBaseColor, punchOutDuration));

            // 시퀀스는 자체 정리가 필요해 _running 대신 _countPunch로만 관리합니다.
        }

        private void ResetCountText()
        {
            _countPunch?.Kill();
            _countPunch = null;

            if (countText == null) return;

            countText.rectTransform.localScale = Vector3.one;
            countText.color = _countBaseColor;
        }

        private void SpawnOne()
        {
            if (iconTemplate == null) return;

            Image icon = Rent();
            if (icon == null) return;

            RectTransform rect = icon.rectTransform;
            Color baseColor = iconTemplate.color;

            Vector2 start = iconTemplate.rectTransform.anchoredPosition + new Vector2(
                Random.Range(-spawnOffsetRange.x, spawnOffsetRange.x),
                Random.Range(-spawnOffsetRange.y, spawnOffsetRange.y));

            rect.anchoredPosition = start;
            rect.localScale = Vector3.one * Random.Range(scaleRange.x, scaleRange.y);
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-maxTilt, maxTilt));
            icon.color = new Color(baseColor.r, baseColor.g, baseColor.b, startAlpha);
            icon.gameObject.SetActive(true);
            _active.Add(icon);

            float duration = Random.Range(durationRange.x, durationRange.y);

            Tween rise = DOVirtual.Float(0f, 1f, duration, progress =>
                {
                    rect.anchoredPosition = new Vector2(start.x, start.y + riseDistance * progress);
                    icon.color = new Color(baseColor.r, baseColor.g, baseColor.b, ResolveAlpha(progress));
                })
                .SetEase(Ease.OutSine)
                .OnComplete(() => Release(icon));

            Track(rise);
        }

        private float ResolveAlpha(float progress)
        {
            if (progress <= fadeStartRatio) return startAlpha;
            return Mathf.Lerp(startAlpha, 0f, Mathf.InverseLerp(fadeStartRatio, 1f, progress));
        }

        private Image Rent()
        {
            while (_pool.Count > 0)
            {
                Image pooled = _pool.Pop();
                if (pooled != null) return pooled;   // 씬 전환 등으로 파괴된 항목은 버립니다.
            }

            return Instantiate(iconTemplate, iconTemplate.transform.parent, false);
        }

        private void Release(Image icon)
        {
            if (icon == null) return;

            _active.Remove(icon);
            icon.gameObject.SetActive(false);
            _pool.Push(icon);
        }

        private void Track(Tween tween)
        {
            if (tween == null) return;

            _running.Add(tween);
            tween.OnKill(() => _running.Remove(tween));
        }
    }
}
