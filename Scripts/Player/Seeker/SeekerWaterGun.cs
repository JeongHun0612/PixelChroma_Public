using UnityEngine;
using Mirror;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PixelChroma.UI;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;

namespace PixelChroma
{
    [RequireComponent(typeof(WaterGunEffects))]
    public class SeekerWaterGun : NetworkBehaviour
    {
        // 이 각도(도)보다 크게 변할 때만 동기화 전송. ±180° 경계에서 매 프레임 전송되는 것을 막습니다.
        private const float AimDeadzoneDeg = 5f;
        // 서버 쿨타임 검증 시 빼주는 핑 지터 여유분(초).
        private const float ServerCooldownGrace = 0.25f;   // 0.1 → 0.25

        [Header("References")]
        [SerializeField] private SpriteRenderer weaponRenderer;
        [SerializeField] private Transform weaponPivot;
        [SerializeField] private Transform firePoint;

        [Header("Settings")]
        [SerializeField] private float fireCooldown = 1.5f;
        [SerializeField] private float fireRange = 8f;
        [SerializeField] private LayerMask runnerLayer;
        [SerializeField] private LayerMask obstacleLayer;

        [Header("Magazine")]
        [Tooltip("탄 1발이 회복되는 데 걸리는 시간(초)")]
        [SerializeField] private float ammoRegenInterval = 10f;

        [Header("Recoil (발사 반동)")]
        [Tooltip("발사 시 총이 총구 반대방향으로 밀리는 거리(월드 유닛). 0이면 반동 없음")]
        [SerializeField] private float recoilDistance = 0.15f;
        [Tooltip("발사 시 카메라 쉐이크 강도")]
        [SerializeField] private float impulseStrength = 1f;

        [Tooltip("발사 시 총구가 들리는 각도(도). 0이면 회전 킥 없음")]
        [SerializeField] private float recoilKickAngle = 8f;

        [Tooltip("밀린 총이 원위치로 복귀하는 시간(초). 짧을수록 탄력적")]
        [SerializeField] private float recoilReturnTime = 0.12f;

        [Header("Knockback Settings")]
        [SerializeField] private float knockbackImpulse = 15f;
        [SerializeField] private float knockbackDrag = 5f;
        [SerializeField] private float knockbackRotSpeed = 720f;

        [SerializeField] private float knockbackForwardBias = 0.7f;




        //---------------테스트-------------------------
#if UNITY_EDITOR
        [Header("=== Auto Fire Test (Editor Only) ===")]
        [SerializeField] private bool autoFireEnabled = false;
        [SerializeField] private float autoFireInterval = 3f;
        private float _autoFireTimer;
#endif
        //---------------------------------------------

        private SeekerPlayer _seeker;

        [SyncVar(hook = nameof(OnAimAngleChanged))]
        private float _syncAimAngle;

        private float _lastFireTime;
        private float _lastServerFireTime = -100f;
        private Vector2 _aimDirection = Vector2.right;
        private Camera _mainCam;
        private WaterGunEffects _effects;

        private int _maxAmmo;           // HUD 슬롯 수에서 받아옴
        private int _currentAmmo;
        private float _regenTimer;
        private int _serverCurrentAmmo;
        private float _serverRegenTimer;

        // 반동 연출용: 총 스프라이트의 원위치와 진행 중인 반동 코루틴
        private Vector3 _weaponRestLocalPos;
        private Coroutine _recoilRoutine;
        private Quaternion _weaponRestLocalRot;
        private CinemachineImpulseSource _impulseSource;

        // UI 발사 차단 판정용 재사용 버퍼(좌클릭 프레임에만 RaycastAll 수행)
        private static readonly List<RaycastResult> _uiHits = new List<RaycastResult>();

        public float FireCooldownDuration => fireCooldown;

        public float CooldownFill01 =>
            fireCooldown <= 0f ? 0f : 1f - Mathf.Clamp01((Time.time - _lastFireTime) / fireCooldown);

        private bool CanFireLocally => Time.time - _lastFireTime >= fireCooldown && _currentAmmo > 0;

        private static bool IsHideTimeNow =>
            GameRoundManager.Instance != null
            && GameRoundManager.Instance.CurrentTimeState == TimeState.HideTime;

     
        private static bool IsFreeFireLocal =>
            IsHideTimeNow && GameRoundManager.Instance.CurrentTimer > 1;


        private Vector2 FireOrigin =>
            firePoint != null ? (Vector2)firePoint.position :
            (weaponPivot != null ? (Vector2)weaponPivot.position : (Vector2)transform.position);

        public int CurrentAmmo => _currentAmmo;
        public int MaxAmmo => _maxAmmo;
        public float RegenFill01 => _currentAmmo >= _maxAmmo ? 0f : 1f - Mathf.Clamp01(_regenTimer / ammoRegenInterval);


        private void Awake()
        {
            _effects = GetComponent<WaterGunEffects>();
            _impulseSource = GetComponent<CinemachineImpulseSource>();
            _seeker = GetComponent<SeekerPlayer>();
            if (weaponRenderer != null)
            {
                _weaponRestLocalPos = weaponRenderer.transform.localPosition;
                _weaponRestLocalRot = weaponRenderer.transform.localRotation;
                
            }
        }

        public override void OnStartLocalPlayer()
        {
            _mainCam = Camera.main;
        }

        public void LocalTick(bool isPaintMode)
        {
            HandleAiming(isPaintMode);
            HandleShooting(isPaintMode);

#if UNITY_EDITOR
            AutoFireTick();
#endif
        }
        public void LocalRegenTick() => RegenerateAmmo();

        public void SetWeaponVisible(bool visible)
        {
            if (weaponRenderer != null)
                weaponRenderer.enabled = visible;
        }

        private void HandleAiming(bool isPaintMode)
        {
            if (_mainCam == null || Mouse.current == null || weaponPivot == null) return;

            if (isPaintMode)
                return;

            // 크로스헤어는 실제 마우스 위치를 그대로 따라간다(사거리 클램프 없음).
            // 사거리 제한은 발사 판정(Physics2D.Raycast fireRange)에서만 적용된다.
            Vector2 mouseScreenPos = Mouse.current.position.ReadValue();
            Vector3 mouseWorldPos = _mainCam.ScreenToWorldPoint(mouseScreenPos);
            mouseWorldPos.z = 0;

            // 방향은 회전 중심(pivot) 기준 — firePoint를 쓰면 회전 피드백 루프 발생
            Vector2 pivotPos = weaponPivot.position;
            Vector2 aimDirection = ((Vector2)mouseWorldPos - pivotPos).normalized;
            _aimDirection = aimDirection;

            float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
            ApplyAimRotation(angle);

            if (Mathf.Abs(Mathf.DeltaAngle(_syncAimAngle, angle)) > AimDeadzoneDeg)
            {
                CmdUpdateAimAngle(angle);
            }
        }

        private void ApplyAimRotation(float angle)
        {
            if (weaponPivot == null) return;

            weaponPivot.rotation = Quaternion.Euler(0, 0, angle);

            Vector3 localScale = Vector3.one;
            if (angle > 90 || angle < -90)
                localScale.y = -1f;
            else
                localScale.y = 1f;

            weaponPivot.localScale = localScale;
        }

        private void PlayRecoil()
        {
            if (weaponRenderer == null) return;
            if (recoilDistance <= 0f && recoilKickAngle <= 0f) return;

            if (_recoilRoutine != null) StopCoroutine(_recoilRoutine);
            _recoilRoutine = StartCoroutine(RecoilRoutine());
        }

        private IEnumerator RecoilRoutine()
        {
            Transform t = weaponRenderer.transform;

            Vector3 kickedPos = _weaponRestLocalPos - Vector3.right * recoilDistance;

            // 회전: 총구가 위로 들리도록 로컬 Z를 +방향으로 킥.
            // 반동은 weaponRenderer 로컬 공간에 적용되고, 뒤집힘(S(1,-1))+조준회전(~180°)이
            // 합쳐지면 Y축 반사가 되어 상하 방향이 보존됨 → 좌우 어느 쪽이든 +kick이 위로 향함.
            Quaternion kickedRot =
                _weaponRestLocalRot * Quaternion.Euler(0f, 0f, recoilKickAngle);

            // 즉발 킥
            t.localPosition = kickedPos;
            t.localRotation = kickedRot;

            float elapsed = 0f;
            while (elapsed < recoilReturnTime)
            {
                elapsed += Time.deltaTime;
                float k = Mathf.Clamp01(elapsed / recoilReturnTime);

                // ease-out: 처음 빠르게 돌아오다 끝에서 부드럽게 안착
                float eased = 1f - (1f - k) * (1f - k);

                t.localPosition = Vector3.Lerp(kickedPos, _weaponRestLocalPos, eased);
                t.localRotation = Quaternion.Slerp(kickedRot, _weaponRestLocalRot, eased);

                yield return null;
            }

            // 오차 없이 정확히 복귀
            t.localPosition = _weaponRestLocalPos;
            t.localRotation = _weaponRestLocalRot;
            _recoilRoutine = null;
        }

        [Command]
        private void CmdUpdateAimAngle(float angle)
        {
            _syncAimAngle = angle;
        }

        private void OnAimAngleChanged(float oldAngle, float newAngle)
        {
            if (isLocalPlayer) return;
            ApplyAimRotation(newAngle);
        }

        private void HandleShooting(bool isPaintMode)
        {
            if (_mainCam == null || Mouse.current == null) return;
            if (isPaintMode || !Mouse.current.leftButton.wasPressedThisFrame) return;
            if (IsPointerOverInteractiveUI()) return;

            // 빈 탄창 사격 시도
            if (_currentAmmo <= 0)
            {
                _seeker.NotifyEmptyFire();
                return;
            }

            if (!CanFireLocally) return;

            _lastFireTime = Time.time;

            if (!IsFreeFireLocal)
                _currentAmmo--;

            Vector2 mouseScreen = Mouse.current.position.ReadValue();
            Vector2 mouseWorld = _mainCam.ScreenToWorldPoint(mouseScreen);   // 조준 시 이미 사거리로 클램프된 위치
            CmdFireWaterGun(mouseWorld);   // 방향이 아니라 '목표점'을 전달

            PlayRecoil();

            // 카메라 쉐이크는 설정에서 끌 수 있다(모션 감수성 접근성). 총 반동(PlayRecoil)은 별개로 항상 유지.
            if (_impulseSource != null && GameSettings.ScreenShakeEnabled)
            {
                Vector2 recoilDir = -_aimDirection.normalized * impulseStrength;
                _impulseSource.GenerateImpulse(new Vector3(recoilDir.x, recoilDir.y, 0f));
            }
        }

        // 포인터 아래 UI 중 '상호작용 컨트롤(Selectable)'이 있을 때만 발사를 막는다.
        // 읽기전용 패널(스크롤만 받는 채팅 등)은 Raycast Target이 켜져 있어도 발사가 통과한다.
        private bool IsPointerOverInteractiveUI()
        {
            if (EventSystem.current == null || Mouse.current == null) return false;

            var ped = new PointerEventData(EventSystem.current)
            {
                position = Mouse.current.position.ReadValue()
            };

            _uiHits.Clear();
            EventSystem.current.RaycastAll(ped, _uiHits);

            for (int i = 0; i < _uiHits.Count; i++)
            {
                if (_uiHits[i].gameObject.GetComponentInParent<Selectable>() != null)
                    return true;   // 버튼·스크롤바 등 진짜 컨트롤 위 → 발사 취소
            }
            return false;          // 읽기전용 UI(또는 UI 없음) → 통과
        }

        //----------------------------------------탄창-------------------------------------------------

        /// HUD 슬롯 수 기반으로 최대 탄창을 설정한다. 클라이언트에서 호출.
        public void SetMaxAmmo(int max)
        {
            _maxAmmo = max;
            _currentAmmo = max;
            _regenTimer = 0f;
            CmdSetMaxAmmo(max);
        }



        [Command]
        private void CmdSetMaxAmmo(int max)
        {
            _maxAmmo = max;
            _serverCurrentAmmo = max;
            _serverRegenTimer = 0f;
        }

        private void RegenerateAmmo()
        {
            if (_maxAmmo <= 0 || _currentAmmo >= _maxAmmo) return;

            _regenTimer += Time.deltaTime;

            if (_regenTimer >= ammoRegenInterval)
            {
                _regenTimer -= ammoRegenInterval;
                _currentAmmo = Mathf.Min(_currentAmmo + 1, _maxAmmo);
            }
        }

        [Server]
        public void ServerTick()
        {
            if (_maxAmmo <= 0 || _serverCurrentAmmo >= _maxAmmo) return;

            _serverRegenTimer += Time.deltaTime;

            if (_serverRegenTimer >= ammoRegenInterval)
            {
                _serverRegenTimer -= ammoRegenInterval;
                _serverCurrentAmmo = Mathf.Min(_serverCurrentAmmo + 1, _maxAmmo);
            }
        }


        //----------------------------------------테스트--------------------------------------------------
#if UNITY_EDITOR
        private void AutoFireTick()
        {
            if (!autoFireEnabled) return;

            _autoFireTimer += Time.deltaTime;
            if (_autoFireTimer < autoFireInterval) return;
            _autoFireTimer = 0f;

            // 가장 가까운 생존 Runner 탐색
            var runners = FindObjectsByType<RunnerPlayer>(FindObjectsSortMode.None);
            RunnerPlayer nearest = null;
            float minDist = float.MaxValue;

            foreach (var r in runners)
            {
                if (r.IsDead) continue;
                float dist = Vector2.Distance(transform.position, r.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = r;
                }
            }

            if (nearest == null) return;
            if (!CanFireLocally) return;

            Vector2 targetPoint = nearest.transform.position;
            _lastFireTime = Time.time;
            _currentAmmo--;
            CmdFireWaterGun(targetPoint);
        }
#endif

        //---------------------------------------------------------------------------------------------
        [Command]
        private void CmdFireWaterGun(Vector2 targetPoint)
        {
            if (Time.time - _lastServerFireTime < fireCooldown - ServerCooldownGrace)
            {
                //Debug.LogWarning($"[WaterGun] 발사 거부(쿨타임): 경과 {Time.time - _lastServerFireTime:F2}s / 요구 {fireCooldown - ServerCooldownGrace:F2}s");
                return;
            }

            if (_serverCurrentAmmo <= 0)
            {
                //Debug.LogWarning($"[WaterGun] 발사 거부(탄창): 서버 0발 / 최대 {_maxAmmo}");
                return;
            }

            if (!IsHideTimeNow)
            {
                if (_serverCurrentAmmo <= 0) return;
                _serverCurrentAmmo--;
            }
            _lastServerFireTime = Mathf.Max(Time.time, _lastServerFireTime + fireCooldown);

            Vector2 origin = FireOrigin;
            Vector2 direction = targetPoint - origin;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
            direction.Normalize();

            RaycastHit2D hit = Physics2D.Raycast(origin, direction, fireRange, obstacleLayer | runnerLayer);
            bool didHit = hit.collider != null;

            Vector2 laserEndPos = didHit ? hit.point : origin + direction * fireRange;
            Vector2 hitNormal = didHit ? hit.normal : -direction;

            // Ray가 직접 맞힌 대상이 Runner인지 확인
            if (didHit)
            {
                var runner = hit.collider.GetComponentInParent<RunnerPlayer>();
                if (runner != null)
                {
                    runner.ServerNotifyCaught(
                        _seeker.playerName, _seeker.netId,
                        BuildKnockbackDir(hit.point, direction, runner.transform.position),
                        knockbackImpulse, knockbackDrag, knockbackRotSpeed);
                }
            }

            RpcPlayFireEffect(laserEndPos, didHit, direction, hitNormal);
        }

        // 착탄점 → 대상 중심 방향과 총알 진행방향을 블렌드한다.
        private Vector2 BuildKnockbackDir(Vector2 hitPoint, Vector2 direction, Vector2 targetCenter)
        {
            Vector2 radial = targetCenter - hitPoint;
            Vector2 radialDir = radial.sqrMagnitude > 0.0001f ? radial.normalized : direction;

            Vector2 blended = Vector2.Lerp(radialDir, direction, knockbackForwardBias);
            return blended.sqrMagnitude < 0.01f ? direction : blended.normalized;
        }

        [ClientRpc]
        private void RpcPlayFireEffect(Vector2 targetPos, bool didHit, Vector2 direction, Vector2 hitNormal)
        {

            if (!isLocalPlayer) PlayRecoil();

            SoundManager.Instance?.PlaySfxAt(SfxType.GunShoot, FireOrigin);

            if (_effects != null)
                _effects.PlayFireEffect(firePoint, targetPos, didHit, direction, hitNormal);
        }
    }
}
