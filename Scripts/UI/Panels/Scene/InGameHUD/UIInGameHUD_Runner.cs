using UnityEngine;
using OneText.UGUI;
using UnityEngine.UI;

namespace PixelChroma.UI
{
    public class UIInGameHUD_Runner : UIInGameHUD
    {
        [Header("Runner Expose Skill UI Only")]
        [SerializeField] private Image exposeSkillCooldownOverlay;
        [SerializeField] private OneTextLabel exposeSkillCooldownText;

        [Header("Runner Indicator")]
        [SerializeField] private TeammateIndicatorPanel runnerIndicatorPanel;

        private bool _isExposeCounting;
        private float _exposeCooldownDuration;
        private float _exposeTimer;

        // 부모의 Update(전역 타이머) 기능에 더해, 내 스킬 쿨타임 차감 연산만 덧붙입니다.
        protected override void Update()
        {
            base.Update(); // 부모의 룸 타이머 연산 실행

            if (_isExposeCounting)
            {
                _exposeTimer -= Time.deltaTime;

                if (_exposeTimer <= 0f)
                {
                    _isExposeCounting = false;
                    if (exposeSkillCooldownOverlay != null) exposeSkillCooldownOverlay.gameObject.SetActive(false);
                    if (exposeSkillCooldownText != null) exposeSkillCooldownText.gameObject.SetActive(false);
                }
                else
                {
                    if (exposeSkillCooldownOverlay != null)
                        exposeSkillCooldownOverlay.fillAmount = _exposeTimer / _exposeCooldownDuration;

                    if (exposeSkillCooldownText != null)
                        exposeSkillCooldownText.text = _exposeTimer.ToString("F1");
                }
            }
        }

        protected override void ResetHUDState()
        {
            base.ResetHUDState();

            _isExposeCounting = false;
            _exposeTimer = 0f;
            _exposeCooldownDuration = 0f;
            if (exposeSkillCooldownOverlay != null) exposeSkillCooldownOverlay.gameObject.SetActive(false);
            if (exposeSkillCooldownText != null) exposeSkillCooldownText.gameObject.SetActive(false);
            if (runnerIndicatorPanel != null) runnerIndicatorPanel.Deactivate();
        }

        public void StartExposeSkillCooldown(float duration)
        {
            _exposeCooldownDuration = duration;
            _exposeTimer = duration;
            _isExposeCounting = true;

            if (exposeSkillCooldownOverlay != null)
            {
                exposeSkillCooldownOverlay.gameObject.SetActive(true);
                exposeSkillCooldownOverlay.fillAmount = 1f;
            }
            if (exposeSkillCooldownText != null)
            {
                exposeSkillCooldownText.gameObject.SetActive(true);
                exposeSkillCooldownText.text = duration.ToString("F1");
            }
        }

        
    }
}