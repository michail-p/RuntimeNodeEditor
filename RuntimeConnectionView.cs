using UnityEngine;
using UnityEngine.UI;

// Visual line connecting two RuntimePortView instances inside the runtime editor.
// Supports both straight lines and bezier curves.
[RequireComponent(typeof(RectTransform))]
public class RuntimeConnectionView : MonoBehaviour
{
    RuntimeNodeEditor editor;
    RuntimePortView from;
    RuntimePortView to;
    RectTransform rectTransform;
    Image straightLine;
    GameObject[] bezierSegments;
    const int BezierSegmentCount = 20;

    Camera CanvasCamera
    {
        get
        {
            if (editor == null || editor.RootCanvas == null)
                return null;

            if (editor.RootCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            return editor.RootCanvas.worldCamera;
        }
    }


    public void Initialize(RuntimeNodeEditor editor, RuntimePortView from, RuntimePortView to)
    {
        this.editor = editor;
        this.from = from;
        this.to = to;
        rectTransform = (RectTransform)transform;

        if (editor.UseBezierConnections)
        {
            bezierSegments = new GameObject[BezierSegmentCount];
            for (int i = 0; i < BezierSegmentCount; i++)
            {
                var segmentGO = new GameObject($"BezierSegment_{i}", typeof(RectTransform), typeof(Image));
                segmentGO.transform.SetParent(transform, false);

                var segmentRect = (RectTransform)segmentGO.transform;
                segmentRect.anchorMin = new Vector2(0.5f, 0.5f);
                segmentRect.anchorMax = new Vector2(0.5f, 0.5f);
                segmentRect.pivot = new Vector2(0f, 0.5f);
                segmentRect.sizeDelta = new Vector2(0f, editor.ConnectionThickness);

                var segmentImage = segmentGO.GetComponent<Image>();
                segmentImage.color = editor.ConnectionColor;
                segmentImage.raycastTarget = false;

                bezierSegments[i] = segmentGO;
            }
        }
        else
        {
            var lineGO = new GameObject("StraightLine", typeof(RectTransform), typeof(Image));
            lineGO.transform.SetParent(transform, false);

            var lineRect = (RectTransform)lineGO.transform;
            lineRect.anchorMin = new Vector2(0.5f, 0.5f);
            lineRect.anchorMax = new Vector2(0.5f, 0.5f);
            lineRect.pivot = new Vector2(0f, 0.5f);
            lineRect.sizeDelta = new Vector2(0f, editor.ConnectionThickness);

            straightLine = lineGO.GetComponent<Image>();
            straightLine.color = editor.ConnectionColor;
            straightLine.raycastTarget = false;
        }

        rectTransform.pivot = new Vector2(0f, 0.5f);
    }

    void LateUpdate()
    {
        if (editor == null || from == null || to == null || editor.RootCanvas == null)
            return;

        var targetRect = editor.ConnectionLayer != null ? editor.ConnectionLayer : (RectTransform)editor.transform;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, from.GetScreenPosition(CanvasCamera), CanvasCamera, out var startLocalRoot);
        RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRect, to.GetScreenPosition(CanvasCamera), CanvasCamera, out var endLocalRoot);

        Vector2 direction = endLocalRoot - startLocalRoot;
        float distance = direction.magnitude;
        if (distance < 0.1f)
        {
            gameObject.SetActive(false);
            return;
        }
        gameObject.SetActive(true);

        if (editor.UseBezierConnections)
            DrawBezier(startLocalRoot, endLocalRoot);
        else
            DrawStraightLine(startLocalRoot, endLocalRoot, direction, distance);
    }

    void DrawStraightLine(Vector2 start, Vector2 end, Vector2 direction, float distance)
    {
        if (straightLine == null)
            return;

        var lineRect = (RectTransform)straightLine.transform;
        lineRect.anchoredPosition = start;
        lineRect.sizeDelta = new Vector2(distance, editor.ConnectionThickness);
        lineRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    void DrawBezier(Vector2 start, Vector2 end)
    {
        if (bezierSegments == null || bezierSegments.Length == 0)
            return;

        Vector2 direction = end - start;
        float baseSign = Mathf.Approximately(direction.x, 0f) ? 0f : Mathf.Sign(direction.x);
        if (Mathf.Approximately(baseSign, 0f))
            baseSign = 1f;

        float startOrientation = from != null && from.Port != null && from.Port.IsInput ? -1f : 1f;
        float endOrientation = to != null && to.Port != null && to.Port.IsInput ? -1f : 1f;

        float startSign = Mathf.Approximately(direction.x, 0f) ? startOrientation : baseSign;
        float endSign = Mathf.Approximately(direction.x, 0f) ? endOrientation : -baseSign;

        float distance = direction.magnitude;
        float strength = Mathf.Min(editor.BezierTangentStrength, Mathf.Max(20f, distance * 0.5f));

        Vector2 startControl = start + new Vector2(startSign * strength, 0f);
        Vector2 endControl = end + new Vector2(endSign * strength, 0f);

        // Calculate tangents based on port directions
        Vector2 prevPoint = start;
        for (int i = 0; i < BezierSegmentCount; i++)
        {
            Vector2 currentPoint = CalculateBezierPoint((i + 1f) / BezierSegmentCount, start, startControl, endControl, end);
            Vector2 segmentDirection = currentPoint - prevPoint;

            if (bezierSegments[i] != null)
            {
                var segmentRect = (RectTransform)bezierSegments[i].transform;
                segmentRect.anchoredPosition = prevPoint;
                segmentRect.sizeDelta = new Vector2(segmentDirection.magnitude, editor.ConnectionThickness);
                segmentRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(segmentDirection.y, segmentDirection.x) * Mathf.Rad2Deg);
            }

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
        if (bezierSegments != null)
            foreach (var segment in bezierSegments)
                if (segment != null)
                    Destroy(segment);

        if (straightLine != null)
            Destroy(straightLine.gameObject);
    }
}