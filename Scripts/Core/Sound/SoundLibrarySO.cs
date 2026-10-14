using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace PixelChroma
{
    // enum 키 → 클립을 매핑하는 사운드 카탈로그.
    //
    // 로딩 전략(하이브리드):
    //  - BGM : 용량이 크고 동시에 1개만 필요하므로 AssetReference(Addressables)로 온디맨드 로드한다.
    //  - SFX : 작고 즉발적으로 자주 재생되므로 AudioClip을 직접 참조해 항상 메모리에 상주시킨다.
    [CreateAssetMenu(fileName = "SoundLibrary", menuName = "PixelChroma/Sound/SoundLibrary")]
    public class SoundLibrarySO : ScriptableObject
    {
        [Serializable]
        public struct BgmEntry
        {
            public BgmType type;
            public AssetReferenceT<AudioClip> clip;    // Addressables 온디맨드
            [Range(0f, 1f)] public float volume;       // 트랙별 기준 볼륨(0이면 무음)
        }

        [Serializable]
        public struct SfxEntry
        {
            public SfxType type;
            public AudioClip clip;                      // 직접 참조(상시 상주)
            [Range(0f, 1f)] public float volume;        // 효과음별 기준 볼륨(0이면 무음)

            // 3D 재생(PlaySfxAt) 전용 감쇠 거리. z는 무시되므로 XY 평면 거리(월드 유닛)다.
            [Tooltip("이 거리까지는 원음 그대로 들린다. 0이면 SoundManager 기본값 사용.")]
            public float minDistance;

            [Tooltip("이 거리에서 완전히 안 들린다. 0이면 SoundManager 기본값 사용.")]
            public float maxDistance;

            // 방향 단서는 소리마다 달라지면 안 되므로 SoundManager에서 전역으로 다룬다.
        }

        [SerializeField] private BgmEntry[] bgmEntries;
        [SerializeField] private SfxEntry[] sfxEntries;

        private Dictionary<BgmType, BgmEntry> _bgmMap;
        private Dictionary<SfxType, SfxEntry> _sfxMap;

        // 최초 조회 시 O(1) 조회용 Dictionary를 지연 생성한다(도메인 리로드 전까지 유지).
        private void EnsureBuilt()
        {
            if (_bgmMap != null) return;

            _bgmMap = new Dictionary<BgmType, BgmEntry>(bgmEntries != null ? bgmEntries.Length : 0);
            if (bgmEntries != null)
                foreach (var e in bgmEntries)
                    _bgmMap[e.type] = e;

            _sfxMap = new Dictionary<SfxType, SfxEntry>(sfxEntries != null ? sfxEntries.Length : 0);
            if (sfxEntries != null)
                foreach (var e in sfxEntries)
                    _sfxMap[e.type] = e;
        }

#if UNITY_EDITOR
        // 조회 캐시는 도메인 리로드 전까지 유지된다. 버리지 않으면 플레이 중 인스펙터 수정이 반영되지 않는다.
        private void OnValidate()
        {
            _bgmMap = null;
            _sfxMap = null;
        }
#endif

        public bool TryGetBgm(BgmType type, out BgmEntry entry)
        {
            EnsureBuilt();
            return _bgmMap.TryGetValue(type, out entry);
        }

        public bool TryGetSfx(SfxType type, out SfxEntry entry)
        {
            EnsureBuilt();
            return _sfxMap.TryGetValue(type, out entry);
        }
    }
}
