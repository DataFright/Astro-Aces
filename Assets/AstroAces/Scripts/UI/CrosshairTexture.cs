using UnityEngine;

namespace AstroAces.UI
{
    /// <summary>
    /// Static factory building the reticle Texture2D in code -- DESIGN.md Sec 7.1: "circle
    /// with a cross and a hollow centre, slightly translucent." No art asset; generated once
    /// at runtime and reused by both the gunnery reticle and the smaller aim marker
    /// (CrosshairController draws the aim marker at a smaller on-screen size, not a
    /// different texture).
    /// </summary>
    public static class CrosshairTexture
    {
        public static Texture2D Create(int size = 64, float alpha = 0.75f)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color clear = new Color(1f, 1f, 1f, 0f);
            Color draw = new Color(1f, 1f, 1f, alpha);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            Vector2 center = new Vector2(size / 2f, size / 2f);
            float ringRadius = size * 0.42f;
            float ringHalfThickness = size * 0.02f;
            float crossInner = size * 0.14f;    // hollow centre -- gap around the exact middle
            float crossOuter = size * 0.40f;
            float armHalfThickness = size * 0.015f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                    float distFromCenter = Vector2.Distance(p, center);
                    bool onRing = Mathf.Abs(distFromCenter - ringRadius) <= ringHalfThickness;

                    float dx = Mathf.Abs(p.x - center.x);
                    float dy = Mathf.Abs(p.y - center.y);
                    bool onHorizontalArm = dy <= armHalfThickness && dx >= crossInner && dx <= crossOuter;
                    bool onVerticalArm = dx <= armHalfThickness && dy >= crossInner && dy <= crossOuter;

                    if (onRing || onHorizontalArm || onVerticalArm)
                        pixels[y * size + x] = draw;
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
