using UnityEngine;
using UnityEngine.UI;
using OneText.UGUI;
using DG.Tweening;

namespace PixelChroma.UI
{
    public class UIInGameHUD_Seeker : UIInGameHUD
    {

        [Header("Water Gun")]
        [SerializeField] private Image waterGunCooldownOverlay;
        [SerializeField] private OneTextLabel waterGunCooldownText;

        [Header("Seeker Indicator")]
        [SerializeField] private TeammateIndicatorPanel seekerIndicatorPanel;

        [Header("Spectate Key Hint")]
        [SerializeField] private GameObject spectateKeyHint;

        [Header("Ammo")]
        [SerializeField] private Transform ammoContainer;    // Ammo (HorizontalLayoutGroup)
        [SerializeField] private OneTextLabel ammoCountText;
        [SerializeField] private Color emptySlotColor = Color.gray;
        [SerializeField] private Image ammoRegenGauge;

        [Header("Ammo Recharge Effect")]
        [SerializeField] private float punchScale = 0.3f;
        [SerializeField] private float punchDuration = 0.3f;

        [SerializeField] private GameObject ammoUIRoot;

        private int _prevAmmo = -1;
        private float _cooldownDuration;
        private bool _wasCooling;

        private Image[] _slotImages;

        private RectTransform _ammoTextRt;     
        private Vector2 _ammoTextBasePos;        
        private bool _ammoTextBaseCaptured;

        public int SlotCount => _slotImages != null ? _slotImages.Length : 0;

        protected override void ResetHUDState()
        {
            base.ResetHUDState();

            _wasCooling = false;
            _cooldownDuration = 0f;
            if (waterGunCooldownOverlay != null) waterGunCooldownOverlay.fillAmount = 0f;
            if (waterGunCooldownText != null) waterGunCooldownText.gameObject.SetActive(false);
            if (seekerIndicatorPanel != null) seekerIndicatorPanel.Deactivate();

            if (ammoCountText != null)
            {
                ammoCountText.text = "";
                ResetAmmoTextTransform();
            }

            if (_slotImages != null)
            {
                for (int i = 0; i < _slotImages.Length; i++)
                    if (_slotImages[i] != null) _slotImages[i].color = Color.white;
            }
            if (ammoRegenGauge != null) ammoRegenGauge.fillAmount = 1f;
            _prevAmmo = -1;
        }
        public void SetWaterGunCooldownDuration(float duration)
        {
            _cooldownDuration = duration;
        }

        public void UpdateWaterGunCooldown(float fillAmount)
        {
            if (waterGunCooldownOverlay != null &&
                !Mathf.Approximately(waterGunCooldownOverlay.fillAmount, fillAmount))
                waterGunCooldownOverlay.fillAmount = fillAmount;

            if (waterGunCooldownText == null)
                return;

            bool isCooling = fillAmount > 0f;

            if (isCooling)
            {
                if (!_wasCooling)
                    waterGunCooldownText.gameObject.SetActive(true);

                float remaining = fillAmount * _cooldownDuration;
                waterGunCooldownText.text = remaining.ToString("F1");
            }
            else if (_wasCooling)
            {
                waterGunCooldownText.gameObject.SetActive(false);
            }

            _wasCooling = isCooling;
        }

        public void SetSpectateKeyHintVisible(bool visible)
        {
            if (spectateKeyHint != null)
                spectateKeyHint.SetActive(visible);
        }

        //------------------탄창------------------------------------------

        public void InitializeSlots()
        {
            if (ammoContainer == null) return;

            int count = ammoContainer.childCount;
            _slotImages = new Image[count];

            for (int i = 0; i < count; i++)
            {
                Transform slot = ammoContainer.GetChild(i);
                Transform ammoImage = slot.Find("AmmoImage");
                if (ammoImage != null)
                    _slotImages[i] = ammoImage.GetComponent<Image>();
            }
        }

        public void UpdateAmmoDisplay(int currentAmmo, int maxAmmo, float regenFill01)
        {
            // 텍스트
            if (ammoCountText != null)
            {
                ammoCountText.text = $"{currentAmmo}/{maxAmmo}";
                ammoCountText.color = currentAmmo == 0 ? Color.red : Color.white;
            }

            // 탄 수 변동 시 텍스트 펀치
            if (_prevAmmo >= 0 && currentAmmo != _prevAmmo)
            {
                CaptureAmmoTextBase();
                ResetAmmoTextTransform();

                if (_ammoTextRt != null)
                    _ammoTextRt.DOPunchScale(Vector3.one * punchScale, punchDuration, 6, 0.5f);
            }

            // 탄약 재충전 시 해당 슬롯 펀치
            if (_prevAmmo >= 0 && currentAmmo > _prevAmmo)
            {
                int rechargedIndex = currentAmmo - 1;  // 새로 채워진 슬롯
                if (_slotImages != null && rechargedIndex < _slotImages.Length && _slotImages[rechargedIndex] != null)
                {
                    Transform t = _slotImages[rechargedIndex].transform;
                    t.DOKill();
                    t.localScale = Vector3.one;
                    t.DOPunchScale(Vector3.one * punchScale, punchDuration, 6, 0.5f);
                }
            }

            _prevAmmo = currentAmmo;

            // 슬롯별 상태
            if (_slotImages != null)
            {
                for (int i = 0; i < _slotImages.Length; i++)
                {
                    if (_slotImages[i] == null) continue;

                    if (i < currentAmmo)
                        _slotImages[i].color = Color.white;       // 채워진 탄
                    else
                        _slotImages[i].color = emptySlotColor;    // 빈 탄 (회색)
                }
            }

            // 회복 게이지바
            if (ammoRegenGauge != null)
            {
                if (currentAmmo < maxAmmo)
                {
                    ammoRegenGauge.gameObject.SetActive(true);
                    ammoRegenGauge.fillAmount = 1f - regenFill01;
                }
                else
                {
                    ammoRegenGauge.fillAmount = 1f;
                }
            }
        }

        public void ShakeAmmoText()
        {
            if (ammoCountText == null) return;

            CaptureAmmoTextBase();
            if (_ammoTextRt == null) return;

            ResetAmmoTextTransform();
            _ammoTextRt.DOShakeAnchorPos(0.2f, 5f, 10);
        }

        public new void SetSpectatorMode(bool active)
        {
            base.SetSpectatorMode(active);
            if (ammoUIRoot != null) ammoUIRoot.SetActive(!active);
        }

        // 레이아웃이 자리를 잡은 뒤(첫 연출 시점) 캡처해야 정확한 원위치를 얻습니다.
        private void CaptureAmmoTextBase()
        {
            if (_ammoTextBaseCaptured || ammoCountText == null) return;

            _ammoTextRt = ammoCountText.GetComponent<RectTransform>();
            if (_ammoTextRt == null) return;

            _ammoTextBasePos = _ammoTextRt.anchoredPosition;
            _ammoTextBaseCaptured = true;
        }

        // 진행 중인 연출을 끊고 위치·스케일을 원상복구합니다.
        private void ResetAmmoTextTransform()
        {
            if (_ammoTextRt == null) return;

            _ammoTextRt.DOKill();
            if (_ammoTextBaseCaptured) _ammoTextRt.anchoredPosition = _ammoTextBasePos;
            _ammoTextRt.localScale = Vector3.one;
        }
    }
}
