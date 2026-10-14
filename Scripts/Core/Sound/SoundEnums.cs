namespace PixelChroma
{
    // BGM 트랙 식별자. 씬 단위로 하나만 재생된다(Title / Lobby / InGame).
    // 새 트랙을 추가하려면 여기에 값을 넣고 SoundLibrary 에셋에 클립을 매핑한다.
    public enum BgmType
    {
        None,
        Title,
        Lobby,
        InGame,
    }

    // 효과음 식별자. UI·인게임 이벤트에서 호출한다.
    // 새 효과음을 추가하려면 여기에 값을 넣고 SoundLibrary 에셋에 클립을 매핑한다.
    public enum SfxType
    {
        ButtonClick,
        Warning,
        PaintSplat,
        Hit,
        Clock,
        GunShoot,
        Expose,
        TimeStateChange,
        ScoreRefresh,
        ResultAppear,
    }

    // 볼륨/음소거를 제어하는 믹서 채널. AudioMixer의 exposed 파라미터와 1:1 대응한다.
    public enum AudioChannel
    {
        Master,
        Bgm,
        Sfx,
    }
}
