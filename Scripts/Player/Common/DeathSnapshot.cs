using PixelChroma;
using UnityEngine;

public class DeathSnapshot
{
    public Vector3 Position { get; private set; }
    public PlayerPaintCanvas.PaintFormType FormType { get; private set; }
    public int FrameIndex { get; private set; }
    public bool FlipX { get; private set; }
    public void Capture(Transform transform, PlayerPaintCanvas paintCanvas)
    {
        Position = transform.position;

        if (paintCanvas != null)
        {
            FormType = paintCanvas._activeFormType;
            FrameIndex = paintCanvas.GetCurrentFrameIndex();
            FlipX = paintCanvas.CurrentFlipX;
        }
    }
}