using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace PixelChroma
{
    public class SoundManager : MonoSingleton<SoundManager>
    {
        // ── 영속화 키 / 믹서 파라미터 ─────────────────────────────
        private const string PrefPrefix = "Sound.";
        private const float MinVolumeDb = -80f;   // 0(선형) → -80dB(무음)

        // AudioSource.priority는 0이 가장 높고 256이 가장 낮다(기본 128).
        // 실제 보이스(AudioManager의 Real Voice Count = 32)가 모자라면 Unity가 낮은 쪽부터 가상화해 소리를 죽이므로,
        // 끊기면 가장 티가 나는 BGM을 맨 위에 두고 3D 효과음을 맨 아래에 둔다.
        private const int BgmPriority = 0;
        private const int SfxPriority = 128;
        private const int Sfx3DPriority = 160;

        private static readonly AudioChannel[] AllChannels =
            { AudioChannel.Master, AudioChannel.Bgm, AudioChannel.Sfx };

        [Header("References")]
        [SerializeField] private SoundLibrarySO library;
        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup bgmGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;

        [Header("SFX")]
        [Tooltip("3D 위치 효과음 동시 재생용 소스 풀 크기")]
        [SerializeField] private int sfx3DPoolSize = 12;

        // 화면 크기는 vcam이 아니라 PixelPerfectCamera가 정한다(PPU 16, 320x180).
        // orthoSize = 180/(2*16) = 5.625 → 화면 세로 반경 ≈ 5.6, 가로 ≈ 10, 모서리 ≈ 11.5.
        [Tooltip("3D 효과음 기본 감쇠 거리(월드 유닛). SoundLibrary 엔트리 값이 0일 때 쓰인다.")]
        [SerializeField] private float defaultSfx3DMinDistance = 2f;
        [SerializeField] private float defaultSfx3DMaxDistance = 20f;

        [Tooltip("3D 효과음 스테레오 확산 각도. 낮을수록 좌우 쏠림이 심해진다.")]
        [SerializeField, Range(0f, 360f)] private float sfx3DSpread = 60f;

        [Tooltip("패닝 기준 깊이(월드 유닛). 클수록 목표 근처에서 좌우가 덜 튄다. 0이면 하드 좌/우로 포화한다.")]
        [SerializeField, Min(0f)] private float sfx3DPanDepth = 6f;

        // 상하가 양방향으로 기울 여유를 두려고 완전 개방(22000)보다 낮게 잡는다.
        [Tooltip("가까울 때의 저역 통과 컷오프(Hz). 상하 기울기의 기준선이 된다.")]
        [SerializeField, Range(1000f, 22000f)] private float sfx3DNearCutoff = 12000f;

        [Tooltip("maxDistance에서의 저역 통과 컷오프(Hz). 멀수록 먹먹해진다. 0이면 거리 음색 변화 사용 안 함.")]
        [SerializeField, Range(0f, 22000f)] private float sfx3DFarCutoff = 900f;

        [Tooltip("상하 음색 기울기 배율. 화면 위쪽은 컷오프 x배(밝게), 아래쪽은 ÷배(먹먹). 1이면 사용 안 함.")]
        [SerializeField, Range(1f, 6f)] private float sfx3DVerticalCueTilt = 2.5f;

        // 화면 세로 반경(≈5.6) 근처에서 최대에 닿아야 화면 안에서 위/아래가 갈린다.
        [Tooltip("상하 음색 기울기가 최대가 되는 높이차(월드 유닛). 작을수록 가까이서도 확실히 구분된다.")]
        [SerializeField, Min(0f)] private float sfx3DVerticalCueRange = 6f;

        // ── BGM (A/B 크로스페이드) ────────────────────────────────
        private AudioSource _activeBgm;   // 현재 재생 중
        private AudioSource _idleBgm;     // 다음 트랙 준비용
        private BgmType _currentBgm = BgmType.None;
        private AsyncOperationHandle<AudioClip> _currentBgmHandle;
        private bool _hasBgmHandle;
        private CancellationTokenSource _bgmCts;

        // ── SFX ───────────────────────────────────────────────────
        private AudioSource _sfx2D;       // 2D one-shot(PlayOneShot로 겹침 처리)
        private AudioSource[] _sfx3DPool;
        private AudioLowPassFilter[] _sfx3DLowPass;   // 인덱스가 _sfx3DPool과 1:1 대응
        private int _sfx3DIndex;
        private AudioListener _listener;  // 3D 재생 z 평면 기준

        // ── 볼륨 상태 ─────────────────────────────────────────────
        private readonly float[] _volumes = { 1f, 1f, 1f };
        private readonly bool[] _mutes = { false, false, false };



        // ==========================================================
        // Lifecycle
        // ==========================================================
        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            BuildAudioSources();
        }

        private void Start()
        {
            // AudioMixer.SetFloat은 믹서가 활성화된 다음 프레임(Start)부터 안정적으로 반영된다.
            LoadAndApplyVolumes();
        }

        protected virtual void OnDestroy()
        {
            if (Instance != this) return;
            CancelBgmTransition();
            ReleaseBgmHandle();
        }

        // ==========================================================
        // BGM
        // ==========================================================
        public async UniTask PlayBgm(BgmType type, float fade = 1f)
        {
            if (type == _currentBgm) return;   // 이미 재생 중이면 무시

            CancelBgmTransition();
            var cts = _bgmCts = new CancellationTokenSource();

            try
            {
                await TransitionBgm(type, fade, cts.Token);
            }
            catch (OperationCanceledException)
            {
                // 더 최근의 PlayBgm 요청으로 대체됨 — 정상 흐름.
            }
            finally
            {
                if (_bgmCts == cts)
                {
                    _bgmCts.Dispose();
                    _bgmCts = null;
                }
            }
        }

        // 새 BGM 전환 요청을 취소하고 현재 사용 중인 두 소스를 함께 Fade-out한다.
        // 로딩 팝업은 이 작업을 기다리지 않고 바로 표시하므로 전환 초반에만 이전 BGM이 짧게 남는다.
        public async UniTask StopBgm(float fade = 1f)
        {
            CancelBgmTransition();
            var cts = _bgmCts = new CancellationTokenSource();

            try
            {
                float activeStart = _activeBgm.volume;
                float idleStart = _idleBgm.volume;

                if (fade > 0f)
                {
                    float elapsed = 0f;
                    while (elapsed < fade)
                    {
                        elapsed += Time.unscaledDeltaTime;
                        float k = Mathf.Clamp01(elapsed / fade);
                        _activeBgm.volume = Mathf.Lerp(activeStart, 0f, k);
                        _idleBgm.volume = Mathf.Lerp(idleStart, 0f, k);
                        await UniTask.Yield(PlayerLoopTiming.Update, cts.Token);
                    }
                }

                _activeBgm.Stop();
                _activeBgm.clip = null;
                _activeBgm.volume = 0f;

                _idleBgm.Stop();
                _idleBgm.clip = null;
                _idleBgm.volume = 0f;

                ReleaseBgmHandle();
                _currentBgm = BgmType.None;
            }
            catch (OperationCanceledException)
            {
                // 로딩이 끝나 새 BGM 요청으로 대체됨 — 정상 흐름.
            }
            finally
            {
                if (_bgmCts == cts)
                {
                    _bgmCts.Dispose();
                    _bgmCts = null;
                }
            }
        }

        private async UniTask TransitionBgm(BgmType type, float fade, CancellationToken token)
        {
            AsyncOperationHandle<AudioClip> newHandle = default;
            bool hasNew = false;
            float targetVolume = 1f;

            try
            {
                // None이 아니면 새 트랙을 로드해 idle 소스에 준비한다.
                if (type != BgmType.None
                    && library != null
                    && library.TryGetBgm(type, out var entry)
                    && entry.clip != null
                    && entry.clip.RuntimeKeyIsValid())
                {
                    newHandle = Addressables.LoadAssetAsync<AudioClip>(entry.clip.RuntimeKey);
                    hasNew = true;
                    var clip = await newHandle.ToUniTask(cancellationToken: token);
                    targetVolume = entry.volume;

                    _idleBgm.clip = clip;
                    _idleBgm.volume = 0f;
                    _idleBgm.loop = true;
                    _idleBgm.Play();
                }

                // 현재 트랙 → 0, 새 트랙 → targetVolume 크로스페이드.
                await CrossFade(_activeBgm, _idleBgm, targetVolume, fade, token);
            }
            catch (OperationCanceledException)
            {
                if (hasNew)
                {
                    _idleBgm.Stop();
                    _idleBgm.clip = null;
                    Addressables.Release(newHandle);
                }
                throw;
            }

            // 커밋: 이전 트랙 정지·해제 후 active/idle 스왑.
            _activeBgm.Stop();
            _activeBgm.clip = null;
            ReleaseBgmHandle();

            (_activeBgm, _idleBgm) = (_idleBgm, _activeBgm);
            _currentBgmHandle = newHandle;
            _hasBgmHandle = hasNew;
            _currentBgm = type;
        }

        private async UniTask CrossFade(
            AudioSource from, AudioSource to, float toTarget, float duration, CancellationToken token)
        {
            if (duration <= 0f)
            {
                from.volume = 0f;
                to.volume = toTarget;
                return;
            }

            float fromStart = from.volume;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                // 씬 전환/일시정지(timeScale=0) 중에도 페이드가 진행되도록 unscaled 시간을 쓴다.
                elapsed += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(elapsed / duration);
                from.volume = Mathf.Lerp(fromStart, 0f, k);
                to.volume = Mathf.Lerp(0f, toTarget, k);
                await UniTask.Yield(PlayerLoopTiming.Update, token);
            }

            from.volume = 0f;
            to.volume = toTarget;
        }

        // ==========================================================
        // SFX
        // ==========================================================
        // 2D 효과음(UI 클릭·팝업 등). PlayOneShot으로 겹쳐 재생된다.
        public void PlaySfx(SfxType type)
        {
            if (library == null || !library.TryGetSfx(type, out var entry) || entry.clip == null)
                return;

            _sfx2D.PlayOneShot(entry.clip, entry.volume);
        }

        // 3D 위치 효과음(페인트 튐·피격 등). 풀에서 라운드로빈으로 소스를 사용한다.
        // 소스를 돌려 쓰므로 거리 값은 매 재생마다 다시 지정해야 이전 효과음 설정이 남지 않는다.
        public void PlaySfxAt(SfxType type, Vector3 position)
        {
            if (library == null || !library.TryGetSfx(type, out var entry) || entry.clip == null)
                return;

            int slot = _sfx3DIndex;
            var src = _sfx3DPool[slot];
            _sfx3DIndex = (_sfx3DIndex + 1) % _sfx3DPool.Length;

            float min = entry.minDistance > 0f ? entry.minDistance : defaultSfx3DMinDistance;
            float max = entry.maxDistance > 0f ? entry.maxDistance : defaultSfx3DMaxDistance;

            // _listener를 갱신하므로 이후 계산보다 먼저 호출한다.
            Vector3 pos = PlaceOnPanPlane(position);

            // 감쇠·단서는 모두 XY 평면 거리 기준이다(패닝용 깊이 제외).
            float dy = 0f;
            float planar = 0f;
            if (_listener != null)
            {
                Vector3 lp = _listener.transform.position;
                dy = pos.y - lp.y;
                planar = new Vector2(pos.x - lp.x, dy).magnitude;
            }

            src.transform.position = pos;
            src.clip = entry.clip;
            src.volume = entry.volume;

            // Unity가 쓰는 거리는 √(planar² + depth²)라, 끝점도 같은 식으로 변환해야
            // 평면 거리 기준으로 잡아 둔 튜닝이 유지된다.
            float depth = sfx3DPanDepth;
            src.minDistance = Mathf.Sqrt(min * min + depth * depth);
            src.maxDistance = Mathf.Max(Mathf.Sqrt(max * max + depth * depth), src.minDistance + 0.01f);

            // 소스를 돌려 쓰므로 아래 값들은 매 재생마다 반드시 다시 지정한다.
            src.spread = sfx3DSpread;
            _sfx3DLowPass[slot].cutoffFrequency = SpatialCutoff(planar, dy, min, max);

            src.Play();
        }

        // 거리와 상하를 하나의 저역 통과 필터로 표현한다. 둘 다 멀거나 아래일수록 먹먹해진다.
        // 상하를 음색으로 주는 이유: 스테레오 패너로는 표현이 안 되고, 탑다운이라 화면 아래는
        // 고도가 아니라 캐릭터 후방이다. "뒤에서 나는 소리는 먹먹하다"는 이미 학습된 단서다.
        // 주파수는 로그로 인지되므로 보간과 기울기를 모두 로그(배율) 공간에서 처리한다.
        private float SpatialCutoff(float planar, float dy, float min, float max)
        {
            const float FloorCutoff = 300f;
            const float OpenCutoff = 22000f;   // 사실상 필터가 꺼진 상태

            float cutoff = sfx3DNearCutoff;

            if (sfx3DFarCutoff > 0f && max > min)
            {
                float t = Mathf.Clamp01((planar - min) / (max - min));
                cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(sfx3DNearCutoff), Mathf.Log(sfx3DFarCutoff), t));
            }

            if (sfx3DVerticalCueTilt > 1f && sfx3DVerticalCueRange > 0f)
            {
                float t = Mathf.Clamp(dy / sfx3DVerticalCueRange, -1f, 1f);
                cutoff *= Mathf.Pow(sfx3DVerticalCueTilt, t);   // 위=배수, 아래=제수
            }

            return Mathf.Clamp(cutoff, FloorCutoff, OpenCutoff);
        }

        // 리스너(메인 카메라)와 같은 평면에 소스를 두면 방향이 (Δx, Δy, 0)이 되어 팬이
        // 거리와 무관한 각도 Δx/√(Δx²+Δy²)가 되고, 가로로 조금만 벗어나도 하드 좌/우로 포화한다.
        // 앞쪽으로 depth만큼 밀어 (Δx, Δy, depth)를 만들면 근거리에서 팬이 완만해진다.
        private Vector3 PlaceOnPanPlane(Vector3 world)
        {
            // 씬 전환 때 카메라가 새로 생성되므로, 파괴됐으면 다시 찾는다.
            if (_listener == null)
                _listener = FindFirstObjectByType<AudioListener>();

            if (_listener != null)
                world.z = _listener.transform.position.z + sfx3DPanDepth;   // forward = +Z

            return world;
        }

        // ==========================================================
        // Volume / Mute  (옵션 UI가 호출할 공개 API)
        // ==========================================================
        public void SetMasterVolume(float v01) => SetVolume(AudioChannel.Master, v01);
        public void SetBgmVolume(float v01) => SetVolume(AudioChannel.Bgm, v01);
        public void SetSfxVolume(float v01) => SetVolume(AudioChannel.Sfx, v01);

        public void SetVolume(AudioChannel channel, float v01)
        {
            v01 = Mathf.Clamp01(v01);
            _volumes[(int)channel] = v01;
            PlayerPrefs.SetFloat(VolumeKey(channel), v01);
            ApplyChannel(channel);
        }

        public void SetMute(AudioChannel channel, bool mute)
        {
            _mutes[(int)channel] = mute;
            PlayerPrefs.SetInt(MuteKey(channel), mute ? 1 : 0);
            ApplyChannel(channel);
        }

        public float GetVolume(AudioChannel channel) => _volumes[(int)channel];
        public bool IsMuted(AudioChannel channel) => _mutes[(int)channel];

        // ==========================================================
        // Internal
        // ==========================================================
        private void LoadAndApplyVolumes()
        {
            foreach (var channel in AllChannels)
            {
                _volumes[(int)channel] = PlayerPrefs.GetFloat(VolumeKey(channel), 1f);
                _mutes[(int)channel] = PlayerPrefs.GetInt(MuteKey(channel), 0) == 1;
                ApplyChannel(channel);
            }
        }

        private void ApplyChannel(AudioChannel channel)
        {
            if (mixer == null) return;

            // 음소거면 무음, 아니면 선형 볼륨을 dB로 변환(사람 귀에 자연스러운 로그 스케일).
            float linear = _mutes[(int)channel] ? 0f : _volumes[(int)channel];
            float dB = linear <= 0.0001f ? MinVolumeDb : Mathf.Log10(linear) * 20f;
            mixer.SetFloat(MixerParam(channel), dB);
        }

        private void BuildAudioSources()
        {
            _activeBgm = CreateSource("BGM_A", bgmGroup, loop: true, spatialBlend: 0f, priority: BgmPriority);
            _idleBgm = CreateSource("BGM_B", bgmGroup, loop: true, spatialBlend: 0f, priority: BgmPriority);

            _sfx2D = CreateSource("SFX_2D", sfxGroup, loop: false, spatialBlend: 0f, priority: SfxPriority);

            int size = Mathf.Max(1, sfx3DPoolSize);
            _sfx3DPool = new AudioSource[size];
            _sfx3DLowPass = new AudioLowPassFilter[size];
            for (int i = 0; i < size; i++)
            {
                var src = CreateSource($"SFX_3D_{i}", sfxGroup, loop: false, spatialBlend: 1f, priority: Sfx3DPriority);

                // Logarithmic은 maxDistance 바깥에서 감쇠를 멈추고 그 볼륨을 유지해 맵 전체에 소리가 깔린다.
                // Linear여야 maxDistance가 "여기서 완전히 안 들림"이 된다.
                src.rolloffMode = AudioRolloffMode.Linear;

                // 거리별 음색 변화용. 컷오프는 재생할 때마다 지정하므로 여기서는 열어만 둔다.
                var lowPass = src.gameObject.AddComponent<AudioLowPassFilter>();
                lowPass.cutoffFrequency = 22000f;

                _sfx3DPool[i] = src;
                _sfx3DLowPass[i] = lowPass;
            }
        }

        private AudioSource CreateSource(
            string name, AudioMixerGroup group, bool loop, float spatialBlend, int priority)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = spatialBlend;
            src.priority = priority;
            src.outputAudioMixerGroup = group;
            return src;
        }

        private void CancelBgmTransition()
        {
            if (_bgmCts == null) return;
            _bgmCts.Cancel();
            _bgmCts.Dispose();
            _bgmCts = null;
        }

        private void ReleaseBgmHandle()
        {
            if (!_hasBgmHandle) return;
            Addressables.Release(_currentBgmHandle);
            _currentBgmHandle = default;
            _hasBgmHandle = false;
        }

        private static string MixerParam(AudioChannel channel) => channel switch
        {
            AudioChannel.Master => "MasterVolume",
            AudioChannel.Bgm => "BgmVolume",
            AudioChannel.Sfx => "SfxVolume",
            _ => "MasterVolume",
        };

        private static string VolumeKey(AudioChannel channel) => PrefPrefix + MixerParam(channel);
        private static string MuteKey(AudioChannel channel) => PrefPrefix + "Mute" + channel;

    }

}
