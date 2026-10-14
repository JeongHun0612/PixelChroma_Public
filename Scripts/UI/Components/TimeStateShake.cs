using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

namespace PixelChroma
{
    // TimeState 전환 시 아주 약한 화면 흔들림을 발생시키는 순수 클라이언트 컴포넌트입니다.
    // CinemachineCamera 프리팹에 부착되어 모든 맵 씬에서 자동으로 동작합니다.
    [RequireComponent(typeof(CinemachineImpulseSource))]
    public class TimeStateShake : MonoBehaviour
    {
        [Header("Shake Strength")]
        [Tooltip("술래 시간 시작 시 흔들림 세기. 물총 반동(1) 기준으로 잡습니다.")]
        [SerializeField] private float seekTimeStrength = 0.8f;
        [Tooltip("라운드 종료 시 흔들림 세기. 조준을 방해할 사람이 없으므로 조금 더 셉니다.")]
        [SerializeField] private float checkTimeStrength = 2f;

        [Header("Sound Sync")]
        [Tooltip("호루라기가 실제로 들리기 시작하는 시점까지의 지연(초). 클립 앞부분의 무음/어택 길이만큼 줍니다.")]
        [SerializeField] private float shakeDelay = 0.05f;

        private CinemachineImpulseSource _impulseSource;
        private Coroutine _shakeRoutine;

        private void Awake()
        {
            _impulseSource = GetComponent<CinemachineImpulseSource>();
        }

        private void OnEnable()
        {
            GameRoundManager.OnTimeStateChanged += HandleTimeStateChanged;
        }

        private void OnDisable()
        {
            GameRoundManager.OnTimeStateChanged -= HandleTimeStateChanged;
        }

        private void HandleTimeStateChanged(TimeState state, int remainingSeconds)
        {
            if (_impulseSource == null || !GameSettings.ScreenShakeEnabled) return;

            float strength = state switch
            {
                TimeState.SeekTime => seekTimeStrength,
                TimeState.CheckTime => checkTimeStrength,
                _ => 0f,
            };

            if (strength <= 0f) return;

            if (shakeDelay <= 0f)
            {
                Shake(strength);
                return;
            }

            // 연속 전환 시 이전 예약이 겹치지 않도록 취소하고 다시 잡습니다.
            if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
            _shakeRoutine = StartCoroutine(ShakeAfterDelay(strength));
        }

        private IEnumerator ShakeAfterDelay(float strength)
        {
            yield return new WaitForSeconds(shakeDelay);
            _shakeRoutine = null;
            Shake(strength);
        }

        private void Shake(float strength)
        {
            // 임펄스 변위는 velocity 방향의 1차원 진동이라, 고정 벡터를 쓰면 항상 같은 축으로만 흔들립니다.
            // 매번 방향을 무작위로 잡아 전환마다 다른 축으로 흔들리게 합니다.
            Vector2 dir = Random.insideUnitCircle.normalized;
            _impulseSource.GenerateImpulse(new Vector3(dir.x, dir.y, 0f) * strength);
        }
    }
}