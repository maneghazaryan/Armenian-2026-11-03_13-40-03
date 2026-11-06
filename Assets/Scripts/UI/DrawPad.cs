using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Aib.UI
{
    /// <summary>
    /// A square to write a letter in with a finger or the mouse. An optional faint sample of the
    /// letter lies underneath. Strokes are kept in 0..1 pad coordinates (y down), the same space
    /// as the letter masks.
    /// </summary>
    public class DrawPad : VisualElement
    {
        static readonly Color Ink = new Color32(0x2B, 0x2A, 0x33, 0xFF);
        const float InkWidth = 0.05f;      // of the pad size
        const float MinStep = 0.004f;      // ignore jitter smaller than this

        readonly VisualElement _guide;
        readonly VisualElement _ink;   // above the guide, so the sample never tints the strokes
        readonly List<List<Vector2>> _strokes = new List<List<Vector2>>();
        int _pointer = -1;

        public IReadOnlyList<List<Vector2>> Strokes => _strokes;
        public bool IsEmpty => _strokes.Count == 0;
        /// <summary>Raised when a stroke is finished.</summary>
        public event Action StrokeEnded;

        public DrawPad(Texture2D guide)
        {
            AddToClassList("draw-pad");
            _guide = new VisualElement { pickingMode = PickingMode.Ignore };
            _guide.AddToClassList("draw-guide");
            if (guide != null) _guide.style.backgroundImage = new StyleBackground(guide);
            Add(_guide);
            _ink = new VisualElement { pickingMode = PickingMode.Ignore };
            _ink.AddToClassList("draw-ink");
            _ink.generateVisualContent += Paint;
            Add(_ink);

            RegisterCallback<PointerDownEvent>(OnDown);
            RegisterCallback<PointerMoveEvent>(OnMove);
            RegisterCallback<PointerUpEvent>(e => End(e.pointerId));
            RegisterCallback<PointerCaptureOutEvent>(e => End(_pointer));
        }

        public bool GuideVisible
        {
            get => _guide.visible;
            set => _guide.visible = value;
        }

        public void Erase()
        {
            _strokes.Clear();
            _ink.MarkDirtyRepaint();
        }

        Vector2 ToPad(Vector3 local) => new Vector2(
            local.x / Mathf.Max(1, contentRect.width),
            local.y / Mathf.Max(1, contentRect.height));

        void OnDown(PointerDownEvent e)
        {
            if (_pointer >= 0 || e.button > 0) return;
            _pointer = e.pointerId;
            this.CapturePointer(e.pointerId);
            _strokes.Add(new List<Vector2> { ToPad(e.localPosition) });
            _ink.MarkDirtyRepaint();
            e.StopPropagation();
        }

        void OnMove(PointerMoveEvent e)
        {
            if (e.pointerId != _pointer || _strokes.Count == 0) return;
            var stroke = _strokes[_strokes.Count - 1];
            var p = ToPad(e.localPosition);
            if (Vector2.Distance(stroke[stroke.Count - 1], p) < MinStep) return;
            stroke.Add(p);
            _ink.MarkDirtyRepaint();
            e.StopPropagation();
        }

        void End(int pointerId)
        {
            if (pointerId != _pointer || _pointer < 0) return;
            if (this.HasPointerCapture(_pointer)) this.ReleasePointer(_pointer);
            _pointer = -1;
            StrokeEnded?.Invoke();
        }

        void Paint(MeshGenerationContext ctx)
        {
            var size = _ink.contentRect.size;
            var p = ctx.painter2D;
            float width = InkWidth * Mathf.Min(size.x, size.y);
            p.lineWidth = width;
            p.strokeColor = Ink;
            p.fillColor = Ink;
            p.lineCap = LineCap.Round;
            p.lineJoin = LineJoin.Round;
            foreach (var stroke in _strokes)
            {
                var first = Vector2.Scale(stroke[0], size);
                if (stroke.Count == 1)
                {
                    p.BeginPath();
                    p.Arc(first, width / 2, Angle.Degrees(0), Angle.Degrees(360));
                    p.Fill();
                    continue;
                }
                p.BeginPath();
                p.MoveTo(first);
                for (int i = 1; i < stroke.Count; i++) p.LineTo(Vector2.Scale(stroke[i], size));
                p.Stroke();
            }
        }
    }
}
