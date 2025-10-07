using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class RuntimeLink : MonoBehaviour
{
    RuntimeNodeEditor _editor;
    RuntimePort _from;
    RuntimePort _to;
    GameObject[] _bezierSegments;


    public void Initialize(RuntimeNodeEditor editor, RuntimePort from, RuntimePort to)
    {
        _editor = editor;
        _from = from;
        _to = to;

        _bezierSegments = new GameObject[_editor.BezierSegments];
        for (int i = 0; i < _editor.BezierSegments; i++)
        {
            var segmentGO = new GameObject($"BezierSegment_{i}", typeof(RectTransform), typeof(Image));
            segmentGO.transform.SetParent(transform, false);

            var segmentRect = (RectTransform)segmentGO.transform;
            segmentRect.anchorMin = new Vector2(0.5f, 0.5f);
            segmentRect.anchorMax = new Vector2(0.5f, 0.5f);
            segmentRect.pivot = new Vector2(0f, 0.5f);
            segmentRect.sizeDelta = new Vector2(0f, editor.LinkThickness);

            var segmentImage = segmentGO.GetComponent<Image>();
            segmentImage.color = editor.LinkColor;
            segmentImage.raycastTarget = false;

            _bezierSegments[i] = segmentGO;
        }

        GetComponent<RectTransform>().pivot = new Vector2(0f, 0.5f);
    }

    void LateUpdate()
    {
        if (_from == null || _to == null)
        {
            gameObject.SetActive(false);
            return;
        }

        var targetRect = _editor.LinkContainer != null ? _editor.LinkContainer : (RectTransform)_editor.transform;
        var canvasCamera = _editor.GetCanvasCamera();

        RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, _from.GetScreenPosition(canvasCamera), canvasCamera, out var startLocalRoot);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, _to.GetScreenPosition(canvasCamera), canvasCamera, out var endLocalRoot);

        if ((endLocalRoot - startLocalRoot).magnitude < 0.1f)
        {
            gameObject.SetActive(false);
            return;
        }
        gameObject.SetActive(true);

        DrawBezier(startLocalRoot, endLocalRoot);
    }

    void DrawBezier(Vector2 start, Vector2 end)
    {
        Vector2 direction = end - start;
        float baseSign = Mathf.Approximately(direction.x, 0f) ? 0f : Mathf.Sign(direction.x);
        if (Mathf.Approximately(baseSign, 0f))
            baseSign = 1f;

        float startOrientation = _from != null && _from.Port != null && _from.Port.IsInput ? -1f : 1f;
        float endOrientation = _to != null && _to.Port != null && _to.Port.IsInput ? -1f : 1f;

        float startSign = Mathf.Approximately(direction.x, 0f) ? startOrientation : baseSign;
        float endSign = Mathf.Approximately(direction.x, 0f) ? endOrientation : -baseSign;

        float distance = direction.magnitude;
        float strength = Mathf.Min(_editor.BezierTangentStrength, Mathf.Max(20f, distance * 0.5f));

        Vector2 startControl = start + new Vector2(startSign * strength, 0f);
        Vector2 endControl = end + new Vector2(endSign * strength, 0f);

        // Calculate tangents based on port directions
        Vector2 prevPoint = start;
        for (int i = 0; i < _editor.BezierSegments; i++)
        {
            Vector2 currentPoint = CalculateBezierPoint((i + 1f) / _editor.BezierSegments, start, startControl, endControl, end);
            Vector2 segmentDirection = currentPoint - prevPoint;

            var segmentRect = (RectTransform)_bezierSegments[i].transform;
            segmentRect.anchoredPosition = prevPoint;
            segmentRect.sizeDelta = new Vector2(segmentDirection.magnitude, _editor.LinkThickness);
            segmentRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(segmentDirection.y, segmentDirection.x) * Mathf.Rad2Deg);

            prevPoint = currentPoint;
        }
    }

    Vector2 CalculateBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
    {
        float u = 1f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;

        Vector2 point = uuu * p0;
        point += 3f * uu * t * p1;
        point += 3f * u * tt * p2;
        point += ttt * p3;

        return point;
    }

    void OnDestroy()
    {
        foreach (var segment in _bezierSegments)
            Destroy(segment);
    }
}