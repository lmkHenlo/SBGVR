using UnityEngine;

namespace Uuvr.VrUi;

// SBGVR: Unity sorts transparent objects by sorting layer and order before render queue, so a game effect
// with a higher sorting layer or order (particles often have one) was drawn over the VR UI quad, whatever its
// render queue. Renderers given this sorting come after everything else that's transparent; their sorting
// orders, then their render queues, order them among themselves.
public static class UiSorting
{
    // The floating screen's sorting order. A couple of orders are kept free above it for things that must
    // be drawn after it whatever their render queue: URP draws no transparent object above queue 5000, the
    // floating screen's own queue, so queues alone can't put anything after it.
    public const int ScreenOrder = short.MaxValue - 2;

    /// <param name="ordersAfterScreen">0 sorts with the floating screen (the render queue decides which comes
    /// first); 1 or 2 sorts after it.</param>
    public static void DrawLast(Renderer renderer, int ordersAfterScreen = 0)
    {
        var layers = SortingLayer.layers;
        if (layers.Length > 0) renderer.sortingLayerID = layers[layers.Length - 1].id;
        renderer.sortingOrder = ScreenOrder + Mathf.Clamp(ordersAfterScreen, 0, short.MaxValue - ScreenOrder);
    }
}
