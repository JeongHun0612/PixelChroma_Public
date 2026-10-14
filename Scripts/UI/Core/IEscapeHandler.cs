namespace PixelChroma
{
    /// <summary>
    /// ESC로 닫을 수 있는 게임플레이 상태(관전·페인트 등)를 소비하는 대상.
    /// UIManager가 유일한 ESC 리더로서, 등록된 핸들러에 처리를 위임한다.
    /// </summary>
    public interface IEscapeHandler
    {
        /// <summary>
        /// ESC를 소비했으면 true. 닫을 게 없으면 false를 반환해 상위(씬 설정)로 넘긴다.
        /// </summary>
        bool HandleEscape();
    }
}
