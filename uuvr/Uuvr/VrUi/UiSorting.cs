using UnityEngine;

namespace Uuvr.VrUi;

// SBGVR: Unity sorts transparent objects by sorting layer and order before render queue, so a game effect
// with a higher sorting layer or order (particles often have one) was drawn over the VR UI quad, whatever its
// render queue. Renderers given this sorting come after everything else that's transparent; their render
// queues then order them among themselves.
public static class UiSorting
{
    public static void DrawLast(Renderer renderer)
    {
        var layers = SortingLayer.layers;
        if (layers.Length > 0) renderer.sortingLayerID = layers[layers.Length - 1].id;
        renderer.sortingOrder = short.MaxValue;
    }
}
