using Mirror;
using PixelChroma;
using PixelChroma.UI;
using UnityEngine;

public class LobbyPlayer : PlayerBase
{
    [Header("Lobby Ready UI")]
    [Tooltip("레디 시 켜질 체크마크 이미지 오브젝트를 연결해주세요.")]
    [SerializeField] private GameObject readyCheckObject;

    [Header("Rank Icons")]
    [SerializeField] private GameObject goldMedalObject;    // 1등 금메달
    [SerializeField] private GameObject silverMedalObject;  // 2등 은메달
    [SerializeField] private GameObject bronzeMedalObject;  // 3등 동메달
    [SerializeField] private GameObject poopObject;         // 꼴지 💩

    private LobbyPlayerState _lobbyState;

    protected override void Awake()
    {
        base.Awake();
        // 같은 프리팹에 있는 상태 관리 스크립트를 찾아옵니다.
        _lobbyState = GetComponent<LobbyPlayerState>();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        LobbyPlayerState.OnReadyChanged += UpdateReadyImage;
        RankClient.OnRanksChanged += ApplyRankIcons;

        UpdateReadyImage();
        ApplyRankIcons();   // 순위가 이미 도착했어도 즉시 반영
    }

    public override void OnStopClient()
    {
        LobbyPlayerState.OnReadyChanged -= UpdateReadyImage;
        RankClient.OnRanksChanged -= ApplyRankIcons;

        base.OnStopClient();
    }

    protected override void OnLocalPlayerStarted()
    {
        var lobby = UIManager.Instance.GetPanel<UILobby>();
        PaintController.BindHUD(lobby);
    }

    protected override void OnLocalPlayerStopped()
    {
        PaintController.UnbindHUD();
    }

    private void UpdateReadyImage()
    {
        if (readyCheckObject != null && _lobbyState != null)
        {
            readyCheckObject.SetActive(_lobbyState.isReady);
        }
    }

    // ── 순위 아이콘 ──────────────────────────────────────────────

    private void ApplyRankIcons()
    {
        HideAllRankIcons();

        // 아직 netId가 없으면(스폰 전) 0끼리 매칭돼 모든 아이콘이 켜지므로 방어합니다.
        if (netId == 0) return;

        if (netId == RankClient.FirstNetId && goldMedalObject != null)
            goldMedalObject.SetActive(true);
        else if (netId == RankClient.SecondNetId && silverMedalObject != null)
            silverMedalObject.SetActive(true);
        else if (netId == RankClient.ThirdNetId && bronzeMedalObject != null)
            bronzeMedalObject.SetActive(true);

        // 꼴지는 1~3등과 겹치지 않으므로 별도 if
        if (netId == RankClient.LastNetId && poopObject != null)
            poopObject.SetActive(true);
    }

    private void HideAllRankIcons()
    {
        if (goldMedalObject != null) goldMedalObject.SetActive(false);
        if (silverMedalObject != null) silverMedalObject.SetActive(false);
        if (bronzeMedalObject != null) bronzeMedalObject.SetActive(false);
        if (poopObject != null) poopObject.SetActive(false);
    }
}