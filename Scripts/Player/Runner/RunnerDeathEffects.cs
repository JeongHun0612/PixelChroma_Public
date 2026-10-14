using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace PixelChroma
{
    public class RunnerDeathEffects : MonoBehaviour
    {
        [Header("피격 카메라 쉐이크")]
        [SerializeField] private float hitImpulseStrength = 1f;

        [Header("넉백")]
        [SerializeField] private float knockbackDuration = 0.4f;

        [Header("사망 깜빡임")]
        [SerializeField] private float deathBlinkDuration = 1.5f;
        [SerializeField] private int deathBlinkCount = 3;

        [Header("피격 점멸")]
        [SerializeField] private float flashDuration = 0.1f;



        private static readonly int FlashAmountID = Shader.PropertyToID("_FlashAmount");

        private Rigidbody2D _rb;
        private SpriteRenderer _visualRenderer;
        private CinemachineImpulseSource _impulseSource;

        private bool _isKnockedBack;
        public bool IsKnockedBack => _isKnockedBack;
        public float KnockbackDuration => knockbackDuration;
        public float DeathBlinkDuration => deathBlinkDuration;

        public void Init(Rigidbody2D rb, SpriteRenderer visualRenderer, Animator animator)  
        {
            _rb = rb;
            _visualRenderer = visualRenderer;
            _impulseSource = GetComponent<CinemachineImpulseSource>();
        }

        public void PlayKnockbackVisuals(PlayerPaintCanvas paintCanvas)
        {

            StartCoroutine(HitFlash());
        }

        public void PlayKnockbackPhysics(KnockbackInfo info)
        {
            StartCoroutine(Knockback(info));
        }

        public void PlayDeathSequence() => StartCoroutine(DeathSequenceRoutine());

        private IEnumerator Knockback(KnockbackInfo info)
        {
            _isKnockedBack = true;
            _rb.constraints = RigidbodyConstraints2D.None;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (_impulseSource != null && GameSettings.ScreenShakeEnabled)
                _impulseSource.GenerateImpulse(new Vector3(info.dir.x, info.dir.y, 0f) * hitImpulseStrength);

            _rb.linearDamping = info.drag;
            _rb.angularDamping = info.drag;
            _rb.angularVelocity = info.rotSpeed * (info.dir.x >= 0 ? -1f : 1f);
            _rb.linearVelocity = info.dir * (info.impulse / _rb.mass);

            yield return new WaitForSeconds(knockbackDuration);

            _isKnockedBack = false;
            _rb.linearVelocity = Vector2.zero;
            _rb.angularVelocity = 0f;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        }

        private IEnumerator HitFlash() // 번쩍이기
        {
            if (_visualRenderer == null) yield break;

            _visualRenderer.material.SetFloat(FlashAmountID, 1f);
            yield return new WaitForSeconds(flashDuration);
            _visualRenderer.material.SetFloat(FlashAmountID, 0f);
        }

        private IEnumerator DeathSequenceRoutine() // 깜빡이기
        {
            if (_rb != null) { _rb.linearVelocity = Vector2.zero; _rb.angularVelocity = 0f; }

            if (_visualRenderer != null)
            {
                float blinkInterval = deathBlinkDuration / (deathBlinkCount * 2);
                for (int i = 0; i < deathBlinkCount; i++)
                {
                    _visualRenderer.color = new Color(1f, 1f, 1f, 0.2f);
                    yield return new WaitForSeconds(blinkInterval);
                    _visualRenderer.color = new Color(1f, 1f, 1f, 1f);
                    yield return new WaitForSeconds(blinkInterval);
                }
                _visualRenderer.color = Color.white;
            }
        }
    }
}