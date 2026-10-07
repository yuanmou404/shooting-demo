using UnityEngine;

namespace PixelArena
{
    /// <summary>
    /// 低分辨率渲染再放大 —— 既得到像素游戏的颗粒感，又能大幅提升安卓帧率。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class Pixelation : MonoBehaviour
    {
        public int PixelScale = 3;
        private RenderTexture rt;
        private int lastW, lastH, lastScale;

        private void OnRenderImage(RenderTexture src, RenderTexture dest)
        {
            int scale = Mathf.Max(1, PixelScale);
            int w = Mathf.Max(64, src.width / scale);
            int h = Mathf.Max(64, src.height / scale);

            if (rt == null || w != lastW || h != lastH || scale != lastScale)
            {
                if (rt != null) rt.Release();
                rt = new RenderTexture(w, h, 16, RenderTextureFormat.Default);
                rt.filterMode = FilterMode.Point;
                rt.wrapMode = TextureWrapMode.Clamp;
                rt.useMipMap = false;
                lastW = w; lastH = h; lastScale = scale;
            }

            Graphics.Blit(src, rt);
            Graphics.Blit(rt, dest);
        }

        private void OnDestroy()
        {
            if (rt != null) rt.Release();
        }
    }
}
