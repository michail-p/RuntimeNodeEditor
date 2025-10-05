using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XNode;
using TMPro;

// Runtime UI for a single NodePort.
[RequireComponent(typeof(RectTransform))]
public class RuntimePortView : UIBehaviour, IPointerClickHandler
{
    static Color DefaultBackground = new Color32(80, 80, 80, 200);
    static Color ConnectorColor = new Color32(255, 255, 255, 255);

    public RuntimeNodeView NodeView { get; set; }
    public NodePort Port { get; set; }

    RectTransform rectTransform;
    TextMeshProUGUI label;
    Image backgroundImage;
    Image connectorImage;
    LayoutElement layoutElement;

    public Vector2 WorldConnectorPosition
    {
        get
        {
            if (connectorImage == null)
                return transform.position;

            return connectorImage.rectTransform.TransformPoint(connectorImage.rectTransform.rect.center);
        }
    }


    internal void Initialize(RuntimeNodeView nodeView, NodePort port)
    {
        NodeView = nodeView;
        Port = port ?? throw new ArgumentNullException(nameof(port));

        rectTransform = (RectTransform)transform;
        rectTransform.anchorMin = new Vector2(0f, 0.5f);
        rectTransform.anchorMax = new Vector2(1f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        backgroundImage = gameObject.GetComponent<Image>();
        if (backgroundImage == null)
            backgroundImage = gameObject.AddComponent<Image>();

        backgroundImage.color = DefaultBackground;
        backgroundImage.raycastTarget = true;

        layoutElement = gameObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = gameObject.AddComponent<LayoutElement>();

        layoutElement.minHeight = 24f;
        layoutElement.preferredHeight = 24f;

        BuildConnector();
        BuildLabel();
        RefreshLabel();
    }

    void BuildConnector()
    {
        var connectorGO = new GameObject("Connector", typeof(RectTransform));
        connectorGO.transform.SetParent(transform, false);

        var connectorRect = (RectTransform)connectorGO.transform;
        connectorRect.sizeDelta = new Vector2(8, 8);
        connectorRect.anchorMin = Port.IsInput ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
        connectorRect.anchorMax = connectorRect.anchorMin;
        connectorRect.pivot = new Vector2(0.5f, 0.5f);
        connectorRect.anchoredPosition = Vector2.zero;

        connectorImage = connectorGO.AddComponent<Image>();
        RefreshColor();
        connectorImage.raycastTarget = false;
    }

    void BuildLabel()
    {
        var textGO = new GameObject("Label", typeof(RectTransform));
        textGO.transform.SetParent(transform, false);

        var textRect = (RectTransform)textGO.transform;
        textRect.anchorMin = new Vector2(0f, 0f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.offsetMin = new Vector2(Port.IsInput ? 12f : 4f, 2f);
        textRect.offsetMax = new Vector2(Port.IsOutput ? -12f : -4f, -2f);
        textRect.pivot = new Vector2(0.5f, 0.5f);

        label = textGO.AddComponent<TextMeshProUGUI>();
        label.font = RuntimeFontUtility.GetDefaultFont();
        label.fontSize = 14;
        label.color = Color.white;
        label.alignment = Port.IsInput ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;
        label.textWrappingMode = TextWrappingModes.NoWrap;
    }

    public void RefreshLabel()
    {
        if (label == null || Port == null)
            return;

        label.text = $"{Port.fieldName} ({(Port.ValueType != null ? Port.ValueType.Name : "Value")})";
        RefreshColor();
    }

    public void RefreshColor()
    {
        if (connectorImage == null)
            return;

        var editor = NodeView != null ? NodeView.Editor : null;
        connectorImage.color = editor != null ? editor.GetPortColor(Port?.ValueType) : ConnectorColor;
    }

    public void SetSelected(bool selected)
    {
        if (backgroundImage == null)
            return;

        backgroundImage.color = selected ? NodeView.Editor.SelectedPortColor : DefaultBackground;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (NodeView == null)
            return;

        NodeView.Editor.HandlePortClicked(this, eventData);
    }

    public Vector2 GetScreenPosition(Camera camera)
    {
        if (connectorImage == null)
            return RectTransformUtility.WorldToScreenPoint(camera, transform.position);

        var rect = connectorImage.rectTransform;
        Vector3 world = rect.TransformPoint(rect.rect.center);

        return RectTransformUtility.WorldToScreenPoint(camera, world);
    }
}