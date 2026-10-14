namespace PixelChroma.UI
{
    public interface IPaintHUD
    {
        UIColorPicker ColorPicker { get; }
        void SetPaintMode(bool active);
    }
}
