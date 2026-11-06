using System;
using System.Collections.Generic;
using Aib.Data;
using UnityEngine;

namespace Aib.Lesson
{
    /// <summary>
    /// Checks a hand-drawn letter against the letter's shape (a 64x64 mask from Tools/gen_glyphs.py).
    /// Points are in 0..1 pad coordinates, y down. A drawing passes when most of what was drawn lies
    /// near the letter (precision) and most of the letter was drawn over (recall).
    /// Thresholds were tuned on simulated wobbly drawings of all 78 shapes.
    /// </summary>
    public static class DrawingCheck
    {
        public const int N = 64;
        const int Tolerance = 3;          // cells (~5% of the pad)
        public const float TracePass = 0.75f;
        public const float MemoryPass = 0.7f;
        const int MinCells = 6;           // a dot or a tiny tick is not a letter

        static readonly List<(int dx, int dy)> Disk = BuildDisk();

        static List<(int, int)> BuildDisk()
        {
            var d = new List<(int, int)>();
            for (int y = -Tolerance; y <= Tolerance; y++)
            for (int x = -Tolerance; x <= Tolerance; x++)
                if (x * x + y * y <= Tolerance * Tolerance) d.Add((x, y));
            return d;
        }

        /// <summary>Mask from its hex form: rows top to bottom, 4 cells per digit, high bit first.</summary>
        public static bool[] ParseBits(string hex)
        {
            var cells = new bool[N * N];
            for (int i = 0; i < hex.Length && i * 4 < cells.Length; i++)
            {
                int v = Convert.ToInt32(hex[i].ToString(), 16);
                for (int b = 0; b < 4; b++) cells[i * 4 + b] = (v & (8 >> b)) != 0;
            }
            return cells;
        }

        /// <summary>
        /// Tracing compares in place. From memory the drawing may be anywhere and any size, so it is
        /// first moved and scaled (keeping its proportions) onto the letter's box.
        /// </summary>
        public static bool Passes(IReadOnlyList<List<Vector2>> strokes, bool[] letter, bool fromMemory)
        {
            var (precision, recall) = Score(strokes, letter, fromMemory);
            float need = fromMemory ? MemoryPass : TracePass;
            return precision >= need && recall >= need;
        }

        public static (float precision, float recall) Score(IReadOnlyList<List<Vector2>> strokes, bool[] letter, bool fit)
        {
            var points = Sample(strokes);
            if (points.Count == 0) return (0, 0);
            if (fit) Fit(points, letter);

            var drawn = new bool[N * N];
            foreach (var p in points)
            {
                int x = Mathf.Clamp((int)(p.x * N), 0, N - 1), y = Mathf.Clamp((int)(p.y * N), 0, N - 1);
                drawn[y * N + x] = true;
            }
            int drawnCount = 0, inkCount = 0, drawnNear = 0, inkCovered = 0;
            var nearInk = Dilate(letter);
            var nearDrawn = Dilate(drawn);
            for (int i = 0; i < drawn.Length; i++)
            {
                if (drawn[i]) { drawnCount++; if (nearInk[i]) drawnNear++; }
                if (letter[i]) { inkCount++; if (nearDrawn[i]) inkCovered++; }
            }
            if (drawnCount < MinCells || inkCount == 0) return (0, 0);
            return ((float)drawnNear / drawnCount, (float)inkCovered / inkCount);
        }

        /// <summary>Points along every stroke, close enough together to leave no gaps between cells.</summary>
        static List<Vector2> Sample(IReadOnlyList<List<Vector2>> strokes)
        {
            const float step = 0.25f / N;
            var result = new List<Vector2>();
            foreach (var stroke in strokes)
            {
                if (stroke.Count == 0) continue;
                result.Add(stroke[0]);
                for (int i = 1; i < stroke.Count; i++)
                {
                    var a = stroke[i - 1];
                    var b = stroke[i];
                    int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b) / step));
                    for (int k = 1; k <= n; k++) result.Add(Vector2.Lerp(a, b, (float)k / n));
                }
            }
            return result;
        }

        static void Fit(List<Vector2> points, bool[] letter)
        {
            Vector2 min = points[0], max = points[0];
            foreach (var p in points) { min = Vector2.Min(min, p); max = Vector2.Max(max, p); }
            int x0 = N, y0 = N, x1 = -1, y1 = -1;
            for (int i = 0; i < letter.Length; i++)
            {
                if (!letter[i]) continue;
                x0 = Math.Min(x0, i % N); x1 = Math.Max(x1, i % N);
                y0 = Math.Min(y0, i / N); y1 = Math.Max(y1, i / N);
            }
            if (x1 < 0) return;
            // Cell centres, matching where drawn points land when rasterised.
            var lmin = new Vector2(x0 + 0.5f, y0 + 0.5f) / N;
            var lmax = new Vector2(x1 + 0.5f, y1 + 0.5f) / N;
            var size = max - min;
            var lsize = lmax - lmin;
            float scale = Mathf.Max(lsize.x, lsize.y) / Mathf.Max(Mathf.Max(size.x, size.y), 1e-3f);
            Vector2 center = (min + max) / 2, lcenter = (lmin + lmax) / 2;
            for (int i = 0; i < points.Count; i++)
                points[i] = (points[i] - center) * scale + lcenter;
        }

        static bool[] Dilate(bool[] cells)
        {
            var result = new bool[cells.Length];
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                if (!cells[y * N + x]) continue;
                foreach (var (dx, dy) in Disk)
                {
                    int u = x + dx, v = y + dy;
                    if (u >= 0 && u < N && v >= 0 && v < N) result[v * N + u] = true;
                }
            }
            return result;
        }
    }

    /// <summary>Letter shapes for drawing: the faint guide image and the mask drawings are checked against.</summary>
    public static class GlyphStore
    {
        [Serializable] class RefList { public List<Ref> items; }
        [Serializable] class Ref { public string key; public string bits; }

        static Dictionary<string, bool[]> _masks;
        static readonly Dictionary<string, Texture2D> Guides = new Dictionary<string, Texture2D>();

        /// <summary>"a_l" for ա, "a_u" for Ա.</summary>
        public static string Key(LetterData letter, string glyph) =>
            letter.id + (glyph == letter.upper && glyph != letter.lower ? "_u" : "_l");

        public static bool[] Mask(string key)
        {
            if (_masks == null)
            {
                _masks = new Dictionary<string, bool[]>();
                var asset = Resources.Load<TextAsset>("Glyphs/refs");
                if (asset != null)
                    foreach (var r in JsonUtility.FromJson<RefList>(asset.text).items)
                        _masks[r.key] = DrawingCheck.ParseBits(r.bits);
            }
            return _masks.TryGetValue(key, out var m) ? m : null;
        }

        public static Texture2D Guide(string key)
        {
            if (Guides.TryGetValue(key, out var tex)) return tex;
            var asset = Resources.Load<TextAsset>("Glyphs/" + key);
            if (asset == null) return null;
            tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            tex.LoadImage(asset.bytes);
            Guides[key] = tex;
            return tex;
        }
    }
}
