using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mirror;

namespace PixelChroma
{
    // 셰이더 마스크 방식 페인트 캔버스. 원본 스프라이트는 그대로 두고 "어디에 무슨 색을
    // 칠했나"만 마스크 텍스처 1장에 기록, PlayerPaintMask 셰이더가 렌더링 시 합성한다.
    // 초기화는 .Init.cs, Undo/Redo는 .History.cs 참고.
    public partial class PlayerPaintCanvas : NetworkBehaviour
    {
        // ── 상수 / 내부 타입 ────────────────────────────────────────────────────

        public enum PaintFormType
        {
            Default,
            Mushroom,
            Star,
            BigCube,
            Circle,
        }


        [System.Serializable]
        public class PaintForm
        {
            public PaintFormType formType;
            public Sprite[] sprites;

            [Tooltip("이 폼 전용 AnimatorOverrideController. 기본 폼(index 0)은 비워두면 원본 컨트롤러를 사용합니다.")]
            public AnimatorOverrideController overrideController;

            public Collider2D formCollider;

            [System.NonSerialized] public int texWidth;
            [System.NonSerialized] public int texHeight;
            //[System.NonSerialized] public Texture2D paintMask;
            [System.NonSerialized] public Color32[] maskPixels;
            //[System.NonSerialized] public Texture2D[] embossTextures;
            //[System.NonSerialized] public Vector4[] maskRects;
            [System.NonSerialized] public bool[] unionOpaque;
            [System.NonSerialized] public bool[] referenceOpaque;
            [System.NonSerialized] public Dictionary<Sprite, int> spriteIndex;
            [System.NonSerialized] public int paintedCells;
            [System.NonSerialized] public int totalOpaquePixels;
            [System.NonSerialized] public bool isDirty;

            // Undo/Redo 스택(폼=도화지마다 독립). 구현은 PlayerPaintCanvas.History.cs 참고.
            [System.NonSerialized] internal List<PaintCommand> undo;
            [System.NonSerialized] internal List<PaintCommand> redo;

            [System.NonSerialized] public Color[] originalPixels;        // 현재 프레임의 원본 픽셀 (Recomposite 입력)
            [System.NonSerialized] public Color[][] allOriginalPixels;   // 모든 프레임의 원본 픽셀
            [System.NonSerialized] public Vector2[][] allEdgeNormals;    // 모든 프레임의 외곽선 법선
            [System.NonSerialized] public Vector2[] edgeNormals;         // 현재 프레임의 외곽선 법선
            [System.NonSerialized] public Color32[] compositedPixels;    // CPU 합성 결과
            [System.NonSerialized] public Texture2D compositedTexture;   // GPU에 올릴 합성 텍스처
            [System.NonSerialized] public Sprite compositedSprite;       // SpriteRenderer에 세팅할 Sprite
            [System.NonSerialized] public bool paintVisible = true;      // paint 표시 여부 (셰이더 파라미터 대체)

        }

        // ── 직렬화 필드 ─────────────────────────────────────────────────────────

        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private Animator targetAnimator;
        public Animator TargetAnimator => targetAnimator;
        [SerializeField] private PaintForm[] forms = new PaintForm[]
        {
            new PaintForm { formType = PaintFormType.Default }
        };
        [SerializeField] private int brushSize = 1;

        [Tooltip("PixelChroma/PlayerPaintMask 셰이더. 비워두면 Shader.Find로 찾습니다(빌드 포함을 위해 배정 권장).")]
        [SerializeField] private Shader paintShader;

        [Header("입체감 (외곽선 엠보스)")]
        [Tooltip("엠보스 효과 전체 세기. 0 = 완전 단색. 몸통 내부는 항상 페인트색 그대로 유지됨")]
        [Range(0f, 1f)]
        [SerializeField] private float shadingStrength = 1f;
        [Tooltip("빛(위쪽)을 받는 외곽선이 페인트색에서 흰색 쪽으로 밝아지는 정도")]
        [Range(0f, 1f)]
        [SerializeField] private float edgeHighlight = 0.05f;
        [Tooltip("그림자 지는(아래쪽) 외곽선이 페인트색에서 검은색 쪽으로 어두워지는 정도")]
        [Range(0f, 1f)]
        [SerializeField] private float edgeShadow = 0.05f;
        [Tooltip("빛이 들어오는 각도(도). 90 = 바로 위, 135 = 좌상단, 45 = 우상단")]
        [Range(0f, 360f)]
        [SerializeField] private float lightAngle = 135f;

        // ── 런타임 필드 / 이벤트 ────────────────────────────────────────────────

        private PaintForm _activeForm;
        private Dictionary<PaintFormType, PaintForm> _formLookup;
        private bool _collidersLockedOff;
        private int _currentIndex = -1;
        private Material _material;
        private RuntimeAnimatorController _baseAnimatorController;

        public event System.Action<float> OnFormTopYChanged;
        public event System.Action<float> OnFormBottomYChanged;

        private Vector2 _cachedLightDir;
        private float _cachedLightAngle = -1f;

        // 채우기(flood fill) 작업용 스크래치 버퍼. 매 클릭마다 새로 할당하지 않도록 재사용한다.
        private bool[] _floodVisited;
        private Stack<int> _floodStack;
        private List<int> _floodResult;

        // ── Public 프로퍼티 ─────────────────────────────────────────────────────

        public int BrushSize { get => brushSize; set => brushSize = value; }
        public PaintFormType _activeFormType => _activeForm.formType;
        public int TextureWidth => _activeForm.texWidth;
        public int TextureHeight => _activeForm.texHeight;
        public float PixelsPerUnit
        {
            get
            {
                var form = _activeForm;
                return form.sprites != null && form.sprites.Length > 0
                    ? form.sprites[0].pixelsPerUnit : 16f;
            }
        }
        public int FormCount => forms != null ? forms.Length : 0;
        public int SortingLayerID => targetRenderer != null ? targetRenderer.sortingLayerID : 0;
        public int SortingOrder => targetRenderer != null ? targetRenderer.sortingOrder : 0;

        // ── Unity 라이프사이클 ──────────────────────────────────────────────────

        void Start()
        {
            InitializeAllForms();
        }

        // 로비/라운드에서 방 옵션(엠보스)이 갱신되면 즉시 반영하기 위해 구독한다.
        // static 이벤트 누수→좀비 핸들러 방지를 위해 OnEnable/OnDisable 짝으로만 다룬다.
        private void OnEnable()
        {
            LobbyPlayerState.OnRoomOptionSynced += HandleRoomOptionSynced;
        }

        private void OnDisable()
        {
            LobbyPlayerState.OnRoomOptionSynced -= HandleRoomOptionSynced;
        }

        // 방장이 로비에서 옵션을 바꾸면 모든 클라의 PlayerPaintCanvas가 이 핸들러로 즉시 재합성한다.
        private void HandleRoomOptionSynced(RoomOption option)
        {
            if (option == null) return;
            ApplyEmboss(option.edgeHighlight, option.edgeShadow);
        }

        void LateUpdate()
        {
            var form = _activeForm;
            if (form == null || form.spriteIndex == null || _material == null) return;

            var current = targetRenderer.sprite;
            if (current == null) return;

            // Animator가 세팅한 atlas sprite → frame index 조회
            if (form.spriteIndex.TryGetValue(current, out int index) && index != _currentIndex)
            {
                _currentIndex = index;
                //ApplyFrameToMaterial(form, index);  // ← 삭제

                // 현재 프레임의 원본/법선 참조를 갱신
                form.originalPixels = form.allOriginalPixels[index];
                form.edgeNormals = form.allEdgeNormals[index];
                Recomposite();
            }

            // compositedSprite로 교체 (Animator가 다음 프레임에 다시 덮어쓸 것)
            if (form.compositedSprite != null)
                targetRenderer.sprite = form.compositedSprite;
        }

        private void OnDestroy()
        {
            if (forms != null)
            {
                foreach (var form in forms)
                {

                    if (form.compositedSprite != null) Destroy(form.compositedSprite);
                    if (form.compositedTexture != null) Destroy(form.compositedTexture);

                }
            }

            if (_material != null) Destroy(_material);
        }

        // ── 포즈 전환 ───────────────────────────────────────────────────────────

        public void SwitchForm(PaintFormType formType)
        {
            if (_formLookup == null || !_formLookup.TryGetValue(formType, out var form)) return;
            if (form == _activeForm) return;

            ActivateForm(formType);
            CmdSwitchForm(formType);
        }

        private void ActivateForm(PaintFormType formType)
        {
            if (_formLookup == null || !_formLookup.TryGetValue(formType, out var form)) return;

            EndStroke();
            _activeForm = form;
            _currentIndex = -1;

            if (targetAnimator != null && _baseAnimatorController != null)
            {
                targetAnimator.runtimeAnimatorController =
                    form.overrideController != null
                        ? form.overrideController
                        : _baseAnimatorController;
            }

            // 콜라이더 전환 추가
            foreach (var f in forms)
            {
                if (f.formCollider != null)
                    f.formCollider.enabled = false;
            }
            if (form.formCollider != null)
                form.formCollider.enabled = !_collidersLockedOff;

            float topY = GetFormTopY(form);
            OnFormTopYChanged?.Invoke(topY);

            float bottomY = GetFormBottomY(form);
            OnFormBottomYChanged?.Invoke(bottomY);
        }

        private float GetFormTopY(PaintForm form)
        {
            if (form.sprites == null || form.sprites.Length == 0) return 0.5f;

            // Sprite.bounds.max.y = 피벗 기준 스프라이트 상단까지의 거리 (월드 유닛, 스케일 1 기준)
            // 예: 16×16 @PPU16 중앙피벗 → 0.5,  32×32 @PPU16 중앙피벗 → 1.0
            return form.sprites[0].bounds.max.y;
        }

        private float GetFormBottomY(PaintForm form)
        {
            if (form.sprites == null || form.sprites.Length == 0) return -0.5f;

            // Sprite.bounds.min.y = 피벗 기준 스프라이트 하단까지의 거리 (월드 유닛, 스케일 1 기준)
            // 피벗이 중앙이 아닌 폼도 실제 하단 위치를 그대로 반영합니다.
            return form.sprites[0].bounds.min.y;
        }

        [Command]
        private void CmdSwitchForm(PaintFormType formType)
        {
            RpcSwitchForm(formType);
        }

        [ClientRpc]
        private void RpcSwitchForm(PaintFormType formType)
        {
            if (isOwned) return;
            ActivateForm(formType);
        }

        //-------- 사망시 캡쳐--------------------------------------------
        public void DisableActiveCollider()//콜라이더 끄기 및 잠그기
        {
            _collidersLockedOff = true;

            if (forms == null) return;
            foreach (var f in forms)
            {
                if (f != null && f.formCollider != null)
                    f.formCollider.enabled = false;
            }
        }

        public int GetCurrentFrameIndex()
        {
            var form = _activeForm;
            if (form == null || form.spriteIndex == null || targetRenderer == null || targetRenderer.sprite == null)
                return _currentIndex;
            return form.spriteIndex.TryGetValue(targetRenderer.sprite, out int idx) ? idx : _currentIndex;
        }

        public bool CurrentFlipX => targetRenderer != null && targetRenderer.flipX;

        // 애니메이터 없이 사망 프레임을 직접 고정: sprite + maskRect + emboss + flip을 인덱스로 못박음.
        // form.sprites/maskRects/embossTextures는 같은 index로 정렬돼 있어 sprite↔maskRect가 정의상 일치.
        public void FreezeFrame(PaintFormType formType, int index, bool flipX)
        {
            if (_formLookup == null || !_formLookup.TryGetValue(formType, out var form)) return;
            if (form.sprites == null || index < 0 || index >= form.sprites.Length) return;

            ActivateForm(formType);              // 활성 폼/마스크텍스처/컨트롤러 정렬(폼 불일치 방지)

            if (targetAnimator != null)
                targetAnimator.enabled = false;  // 애니메이터가 sprite를 덮어쓰지 못하게 정지

            if (targetRenderer != null)
            {
                targetRenderer.flipX = flipX;
            }

            _currentIndex = index;

            // 현재 프레임 참조 갱신 + 합성
            form.originalPixels = form.allOriginalPixels[index];
            form.edgeNormals = form.allEdgeNormals[index];
            Recomposite();

            // compositedSprite 적용 (Animator 정지 상태이므로 덮어쓰기 없음)
            if (targetRenderer != null && form.compositedSprite != null)
                targetRenderer.sprite = form.compositedSprite;

        }

        public void SetPaintVisible(bool visible)
        {
            var form = _activeForm;
            if (form == null) return;
            form.paintVisible = visible;
            Recomposite();

        }

        // ── Late-join 동기화 ─────────────────────────────────────────────────────
        //   접속 전에 칠해진 내용을 새 클라에 반영. 호스트 maskPixels가 최신 상태이므로
        //   요청자에게만 스냅샷 전송(마스크가 커서 청크 분할). 트리거는 PlayerBase.

        [Server]
        public void SendPaintSnapshotTo(NetworkConnectionToClient conn)
        {
            if (_formLookup == null) return;

            var idxBuf = new List<int>(RelayMaxCells);
            var colBuf = new List<Color32>(RelayMaxCells);

            foreach (var form in _formLookup.Values)
            {
                if (form.maskPixels == null) continue;

                var pixels = form.maskPixels;
                bool firstChunk = true;

                for (int i = 0; i < pixels.Length; i++)
                {
                    if (pixels[i].a == 0) continue;

                    idxBuf.Add(i);
                    colBuf.Add(pixels[i]);

                    if (idxBuf.Count == RelayMaxCells)
                    {
                        TargetLoadSnapshot(conn, form.formType, firstChunk, idxBuf.ToArray(), colBuf.ToArray());
                        firstChunk = false;
                        idxBuf.Clear();
                        colBuf.Clear();
                    }
                }

                if (idxBuf.Count > 0)
                {
                    TargetLoadSnapshot(conn, form.formType, firstChunk, idxBuf.ToArray(), colBuf.ToArray());
                    idxBuf.Clear();
                    colBuf.Clear();
                }
            }

            TargetRestoreActiveForm(conn, _activeForm.formType);
        }

        [TargetRpc]
        private void TargetLoadSnapshot(NetworkConnectionToClient target,
            PaintFormType formType, bool activateForm, int[] indices, Color32[] colors)
        {
            if (isOwned) return;
            if (_formLookup == null || !_formLookup.TryGetValue(formType, out var form)) return;
            if (form.maskPixels == null || indices == null) return;

            int count = Mathf.Min(indices.Length, colors.Length);
            for (int i = 0; i < count; i++)
            {
                int idx = indices[i];
                if (idx < 0 || idx >= form.maskPixels.Length) continue;
                ApplyCellRaw(form, idx, colors[i]);
            }

            //form.paintMask.SetPixels32(form.maskPixels);
            //form.paintMask.Apply();
            if (form == _activeForm) Recomposite();

            form.isDirty = false;
        }

        [TargetRpc]
        private void TargetRestoreActiveForm(NetworkConnectionToClient target,
            PaintFormType formType)
        {
            if (isOwned) return;
            ActivateForm(formType);
        }

        // ── 색칠 ────────────────────────────────────────────────────────────────

        public bool PaintAt(int px, int py, Color color)
        {
            bool painted = PaintLocal(px, py, color, brushSize);
            if (painted)
            {
                ApplyChanges();
                CmdPaint(px, py, color, brushSize);
            }
            return painted;
        }

        // 채우기(페인트통): 클릭한 셀과 같은 색으로 이어진 영역만 칠한다.
        // (닉네임 편집기의 채우기와 동일 규칙 — 빈 칸을 찍으면 그 빈 영역만 채워짐)
        public bool FloodFill(int px, int py, Color color)
        {
            bool painted = FloodFillLocal(px, py, color);
            if (painted)
            {
                ApplyChanges();
                CmdFloodFill(px, py, color);
            }
            return painted;
        }

        private bool PaintLocal(int px, int py, Color color, int size)
        {
            var form = _activeForm;
            if (form.maskPixels == null) return false;

            bool anyPainted = false;
            Color32 fill = PaintColorUtil.ToOpaqueColor32(color);
            GetBrushBounds(form, px, py, size, out int x0, out int y0, out int x1, out int y1);

            for (int x = x0; x < x1; x++)
            {
                for (int y = y0; y < y1; y++)
                {
                    int idx = y * form.texWidth + x;
                    if (!form.unionOpaque[idx]) continue;
                    // 이미 같은 색이면 변화 없음 → 칠한 것으로 치지 않는다(중복 전송·재합성 방지).
                    if (PaintColorUtil.Equals(form.maskPixels[idx], fill)) continue;

                    RecordBefore(form, idx);
                    SetMaskCell(form, idx, color);

                    anyPainted = true;
                }
            }

            if (anyPainted) form.isDirty = true;
            return anyPainted;
        }

        // 4방향 flood fill. 시작 셀의 현재 마스크 색(알파 0 = 아직 안 칠한 빈 칸)과 같은
        // 셀만 따라가고, 실루엣 밖(unionOpaque == false)은 벽으로 취급해 넘지 않는다.
        private bool FloodFillLocal(int px, int py, Color color)
        {
            var form = _activeForm;
            if (form == null || form.maskPixels == null || form.unionOpaque == null) return false;
            if (px < 0 || px >= form.texWidth || py < 0 || py >= form.texHeight) return false;

            int startIdx = py * form.texWidth + px;
            if (!form.unionOpaque[startIdx]) return false;   // 몸통 밖을 찍으면 아무 일도 안 함

            Color32 target = form.maskPixels[startIdx];
            Color32 fill = PaintColorUtil.ToOpaqueColor32(color);
            if (PaintColorUtil.Equals(target, fill)) return false;   // 같은 색 → 채울 것 없음

            int cellCount = form.texWidth * form.texHeight;
            if (_floodVisited == null || _floodVisited.Length < cellCount)
                _floodVisited = new bool[cellCount];
            _floodStack ??= new Stack<int>(256);
            _floodResult ??= new List<int>(256);

            int changedCount = PaintOps.CollectFloodRegion(
                form.maskPixels,
                form.texWidth,
                form.texHeight,
                startIdx,
                form.unionOpaque,
                0,
                _floodVisited,
                _floodStack,
                _floodResult);

            for (int i = 0; i < changedCount; i++)
            {
                int idx = _floodResult[i];
                RecordBefore(form, idx);
                SetMaskCell(form, idx, color);
            }

            if (changedCount == 0) return false;
            form.isDirty = true;
            return true;
        }

        private static void SetMaskCell(PaintForm form, int idx, Color color)
        {
            if (form.maskPixels[idx].a == 0 && form.referenceOpaque[idx]) form.paintedCells++;
            form.maskPixels[idx] = PaintColorUtil.ToOpaqueColor32(color);
        }

        [Command]
        private void CmdPaint(int px, int py, Color color, int size)
        {
            RpcPaint(px, py, color, size);
        }

        [ClientRpc]
        private void RpcPaint(int px, int py, Color color, int size)
        {
            if (isOwned) return;

            if (PaintLocal(px, py, color, size))
                ApplyChanges();
        }

        [Command]
        private void CmdFloodFill(int px, int py, Color color)
        {
            RpcFloodFill(px, py, color);
        }

        [ClientRpc]
        private void RpcFloodFill(int px, int py, Color color)
        {
            if (isOwned) return;

            // 원격도 같은 마스크 상태에서 같은 시드로 재실행 → 동일한 영역이 칠해진다.
            if (FloodFillLocal(px, py, color))
                ApplyChanges();
        }

        // ── 색하나 차출 ────────────────────────────────────────

        public Color32 GetRandomPaintedColor()
        {
            var form = _activeForm;
            if (form?.maskPixels == null) return default;

            // 칠해진 픽셀의 인덱스를 수집
            var painted = new System.Collections.Generic.List<int>();
            for (int i = 0; i < form.maskPixels.Length; i++)
            {
                if (form.maskPixels[i].a > 0)
                    painted.Add(i);
            }

            if (painted.Count == 0) return default;

            int pick = painted[UnityEngine.Random.Range(0, painted.Count)];
            return form.maskPixels[pick];
        }
        // ── 지우기 (즉시) ───────────────────────────────────────────────────────

        public void EraseAt(int px, int py)
        {
            if (!EraseLocal(px, py, brushSize)) return;

            ApplyChanges();
            CmdErase(px, py, brushSize);
        }

        private bool EraseLocal(int px, int py, int size)
        {
            var form = _activeForm;
            if (form.maskPixels == null) return false;

            GetBrushBounds(form, px, py, size, out int x0, out int y0, out int x1, out int y1);
            if (x0 >= x1 || y0 >= y1) return false;

            bool anyErased = false;
            for (int x = x0; x < x1; x++)
            {
                for (int y = y0; y < y1; y++)
                {
                    int idx = y * form.texWidth + x;
                    // 이미 빈 칸이면 변화 없음 → 지운 것으로 치지 않는다(중복 전송·재합성 방지).
                    if (form.maskPixels[idx].a == 0) continue;

                    RecordBefore(form, idx);
                    ClearMaskCell(form, idx);
                    anyErased = true;
                }
            }

            if (anyErased) form.isDirty = true;
            return anyErased;
        }

        private static void ClearMaskCell(PaintForm form, int idx)
        {
            if (form.maskPixels[idx].a != 0 && form.referenceOpaque[idx]) form.paintedCells--;
            form.maskPixels[idx] = new Color32(0, 0, 0, 0);
        }

        [Command]
        private void CmdErase(int px, int py, int size)
        {
            RpcErase(px, py, size);
        }

        [ClientRpc]
        private void RpcErase(int px, int py, int size)
        {
            if (isOwned) return;

            if (EraseLocal(px, py, size))
                ApplyChanges();
        }

        // ── 판정 ────────────────────────────────────────────────────────────────

        public float GetPaintRatio()
        {
            var form = _activeForm;
            if (form == null || form.maskPixels == null || form.totalOpaquePixels == 0) return 0f;
            return (float)form.paintedCells / form.totalOpaquePixels;
        }

        // ── 좌표 변환 / 브러시 유틸리티 ─────────────────────────────────────────

        public Vector2Int WorldToPixel(Vector3 worldPos)
        {
            if (targetRenderer == null) return Vector2Int.zero;

            var form = _activeForm;
            Vector3 localPos = targetRenderer.transform.InverseTransformPoint(worldPos);
            float ppu = PixelsPerUnit;

            int px = Mathf.FloorToInt((localPos.x + 0.5f * form.texWidth / ppu) * ppu);
            int py = Mathf.FloorToInt((localPos.y + 0.5f * form.texHeight / ppu) * ppu);

            return new Vector2Int(px, py);
        }

        public bool IsInBounds(int px, int py)
        {
            var form = _activeForm;
            return px >= 0 && px < form.texWidth && py >= 0 && py < form.texHeight;
        }

        public Vector3 GetBrushWorldCenter(int px, int py, int size)
        {
            if (targetRenderer == null) return Vector3.zero;

            var form = _activeForm;
            float ppu = PixelsPerUnit;
            PaintOps.GetBrushRect(px, py, size, out int minX, out int minY, out int maxX, out int maxY);
            float localX = (minX + 0.5f * (maxX - minX)) / ppu - 0.5f * form.texWidth / ppu;
            float localY = (minY + 0.5f * (maxY - minY)) / ppu - 0.5f * form.texHeight / ppu;
            return targetRenderer.transform.TransformPoint(new Vector3(localX, localY, 0f));
        }

        public float GetBrushWorldSize(int size)
        {
            float scale = targetRenderer != null ? Mathf.Abs(targetRenderer.transform.lossyScale.x) : 1f;
            return size / PixelsPerUnit * scale;
        }

        // ── 텍스처 · 머티리얼 유틸리티 ──────────────────────────────────────────

        public void ResetTexture()
        {
            var form = _activeForm;
            if (form.maskPixels == null) return;

            ClearHistory();
            System.Array.Clear(form.maskPixels, 0, form.maskPixels.Length);
            form.paintedCells = 0;

            Recomposite();
            form.isDirty = false;
        }

        public void ApplyChanges()
        {
            var form = _activeForm;
            if (!form.isDirty) return;

            Recomposite();
            form.isDirty = false;
        }

        private void GetBrushBounds(PaintForm form, int px, int py, int size,
            out int x0, out int y0, out int x1, out int y1)
        {
            PaintOps.GetBrushBounds(px, py, size, form.texWidth, form.texHeight,
                out x0, out y0, out x1, out y1);
        }

        // ── 에디터 전용 ─────────────────────────────────────────────────────────

#if UNITY_EDITOR
        [ContextMenu("Animator에서 스프라이트 수집 (다음 폼에 추가)")]
        private void CollectSpritesFromAnimator()
        {
            var anim = GetComponent<Animator>();
            if (anim == null || anim.runtimeAnimatorController == null)
            {
                Debug.LogWarning("Animator 또는 AnimatorController가 없습니다.", this);
                return;
            }

            var clips = anim.runtimeAnimatorController.animationClips;
            if (clips == null || clips.Length == 0)
            {
                Debug.LogWarning("AnimationClip이 없습니다.", this);
                return;
            }

            var sprites = new HashSet<Sprite>();

            foreach (var clip in clips)
            {
                var bindings = UnityEditor.AnimationUtility.GetObjectReferenceCurveBindings(clip);
                foreach (var binding in bindings)
                {
                    if (binding.type != typeof(SpriteRenderer) ||
                        binding.propertyName != "m_Sprite")
                        continue;

                    var keyframes = UnityEditor.AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    foreach (var kf in keyframes)
                    {
                        if (kf.value is Sprite sprite)
                            sprites.Add(sprite);
                    }
                }
            }

            if (sprites.Count == 0)
            {
                Debug.LogWarning("AnimationClip에서 스프라이트를 찾을 수 없습니다.", this);
                return;
            }

            if (forms == null) forms = System.Array.Empty<PaintForm>();

            var usedTypes = new HashSet<PaintFormType>();
            foreach (var f in forms) usedTypes.Add(f.formType);

            PaintFormType newType = default;
            foreach (PaintFormType t in System.Enum.GetValues(typeof(PaintFormType)))
            {
                if (!usedTypes.Contains(t)) { newType = t; break; }
            }

            int newIndex = forms.Length;
            System.Array.Resize(ref forms, newIndex + 1);
            forms[newIndex] = new PaintForm
            {
                formType = newType,
                sprites = sprites.OrderBy(s => s.name).ToArray()
            };

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[{newType}] 스프라이트 {sprites.Count}개를 수집하여 인덱스 {newIndex}에 추가했습니다.", this);
        }
#endif

        // ── CPU 컴포지팅 ────────────────────────────────────────────────

        private Vector2 GetLightDir()
        {
            if (lightAngle != _cachedLightAngle)
            {
                _cachedLightDir = PaintEmboss.LightDir(lightAngle);
                _cachedLightAngle = lightAngle;
            }
            return _cachedLightDir;
        }

        /// <summary>
        /// CPU에서 originalPixels + maskPixels + edgeNormals을 합성하여
        /// compositedTexture에 업로드한다. UV 변환 없이 pixel 인덱스로 직접 접근.
        /// </summary>
        // ── 엠보스(외곽선 하이라이트/쉐도우) 외부 제어 ──────────────────────────
        // 방 옵션이나 미리보기 UI가 값을 밀어넣을 때 사용. 값 반영 후 즉시 재합성한다.
        public void ApplyEmboss(float highlight, float shadow)
        {
            edgeHighlight = Mathf.Clamp01(highlight);
            edgeShadow = Mathf.Clamp01(shadow);
            if (_activeForm != null) Recomposite();
        }

        // 현재 유효한 RoomOption에서 엠보스 값을 읽어 반영한다.
        // (동기화된 옵션이 없으면 인스펙터 기본값을 유지)
        private void PullEmbossFromRoomOption()
        {
            var option = ActiveRoomOption();
            if (option == null) return;

            edgeHighlight = Mathf.Clamp01(option.edgeHighlight);
            edgeShadow = Mathf.Clamp01(option.edgeShadow);
        }

        // 인게임이면 라운드 옵션(GameRoundManager), 로비면 로비 옵션(LobbyManager)을 우선 사용한다.
        private static RoomOption ActiveRoomOption()
        {
            var round = GameRoundManager.Instance != null
                ? GameRoundManager.Instance.CurrentRoomOption
                : null;
            if (round != null) return round;

            return LobbyManager.Instance != null
                ? LobbyManager.Instance.CurrentRoomOption
                : null;
        }

        private void Recomposite()
        {
            var form = _activeForm;
            if (form == null || form.compositedPixels == null) return;

            var orig = form.originalPixels;           // 현재 프레임의 원본
            var mask = form.maskPixels;               // paint 데이터
            var normals = form.edgeNormals;           // 현재 프레임의 법선
            var output = form.compositedPixels;
            bool showPaint = form.paintVisible;
            Vector2 ld = GetLightDir();
            int count = form.texWidth * form.texHeight;

            for (int i = 0; i < count; i++)
            {
                // 원본 알파 → byte (0~255)
                byte origA = (byte)Mathf.RoundToInt(Mathf.Clamp01(orig[i].a) * 255f);

                if (showPaint && mask[i].a > 0 && origA > 0)
                {
                    // ── paint가 있고, 원본이 불투명, paint 표시 중 ──
                    float pr = mask[i].r / 255f;
                    float pg = mask[i].g / 255f;
                    float pb = mask[i].b / 255f;

                    // 엠보스: 법선 · 빛방향 (프리뷰와 공유하는 PaintEmboss.Shade)
                    PaintEmboss.Shade(ref pr, ref pg, ref pb, normals[i], ld,
                        edgeHighlight, edgeShadow, shadingStrength);

                    output[i] = new Color32(
                        (byte)Mathf.RoundToInt(pr * 255f),
                        (byte)Mathf.RoundToInt(pg * 255f),
                        (byte)Mathf.RoundToInt(pb * 255f),
                        origA);
                }
                else
                {
                    // ── paint 없음 / 투명 / paint 숨김 → 원본 그대로 ──
                    output[i] = new Color32(
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(orig[i].r) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(orig[i].g) * 255f),
                        (byte)Mathf.RoundToInt(Mathf.Clamp01(orig[i].b) * 255f),
                        origA);
                }
            }

            form.compositedTexture.SetPixels32(output);
            form.compositedTexture.Apply();
        }
    }
}
