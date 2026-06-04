using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renderiza uma payline dentro do canvas UI.
/// Coloque este componente num RectTransform full-stretch sobre o slot grid.
/// </summary>
[RequireComponent(typeof(CanvasRenderer))]
public class UIPaylineRenderer : Graphic
{
    [SerializeField] private float _lineWidth = 6f;
    [SerializeField] private Color _litColor  = Color.white;
    [SerializeField] private Color _dimColor  = new Color(1f, 1f, 1f, 0.2f);

    private readonly List<Vector2> _points   = new();
    private int                    _litCount = 0;

    // ── API pública ───────────────────────────────────────────────────────

    /// <summary>
    /// Define os pontos em coordenadas LOCAIS deste RectTransform.
    /// litCount = quantos pontos iniciais ficam acesos; o restante fica apagado.
    /// </summary>
    public void SetLine(IList<Vector2> localPoints, int litCount)
    {
        _points.Clear();
        foreach (var p in localPoints) _points.Add(p);
        _litCount = litCount;
        SetVerticesDirty();
        enabled = true;
    }

    public void Clear()
    {
        _points.Clear();
        _litCount = 0;
        SetVerticesDirty();
    }

    // ── Mesh ──────────────────────────────────────────────────────────────

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (_points.Count < 2) return;

        for (int i = 0; i < _points.Count - 1; i++)
        {
            // segmento i→i+1 é "lit" se i+1 <= litCount (inclusive o ponto de corte)
            Color c = (i < _litCount - 1) ? _litColor : _dimColor;
            AddSegment(vh, _points[i], _points[i + 1], c);
        }
    }

    private void AddSegment(VertexHelper vh, Vector2 a, Vector2 b, Color c)
    {
        Vector2 dir  = (b - a).normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x) * (_lineWidth * 0.5f);

        int idx = vh.currentVertCount;
        vh.AddVert(a - perp, c, Vector2.zero);
        vh.AddVert(a + perp, c, Vector2.zero);
        vh.AddVert(b + perp, c, Vector2.zero);
        vh.AddVert(b - perp, c, Vector2.zero);
        vh.AddTriangle(idx,     idx + 1, idx + 2);
        vh.AddTriangle(idx,     idx + 2, idx + 3);
    }
}
