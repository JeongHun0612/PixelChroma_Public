namespace PixelChroma.UI
{
    public abstract class UIScene : UIPanel
    {
        /// <summary>
        /// 팝업이 하나도 없고, 게임플레이 핸들러도 소비하지 않았을 때 ESC로 호출된다. 기본은 무시.
        /// </summary>
        public virtual void OnCancel() { }
    }
}
