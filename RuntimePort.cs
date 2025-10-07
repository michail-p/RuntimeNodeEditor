using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XNode;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class RuntimePort : UIBehaviour, IPointerClickHandler
{
    public RuntimeNode Node;
    public NodePort Port;

    RuntimeNodeEditor _editor;
    TextMeshProUGUI _label;
    Image _backgroundImage;
    Image _connectorImage;

    public Vector2 WorldConnectorPosition
    {
        get
        {
            if (_connectorImage == null)
                return transform.position;

            return _connectorImage.rectTransform.TransformPoint(_connectorImage.rectTransform.rect.center);
        }
    }


    public void Initialize(RuntimeNode node, NodePort port)
    {
        Node = node;
        Port = port;
        _editor = node.Editor;

        var rectTransform = (RectTransform)transform;
        rectTransform.anchorMin = new Vector2(0f, 0.5f);
        rectTransform.anchorMax = new Vector2(1f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);

        _backgroundImage = gameObject.AddComponent<Image>();
        _backgroundImage.sprite = _editor.CustomBG != null ? _editor.CustomBG : null;
        _backgroundImage.type = Image.Type.Sliced;
        _backgroundImage.pixelsPerUnitMultiplier = 64;
        _backgroundImage.color = _editor.PortBGColor;
        _backgroundImage.raycastTarget = true;

        CreateConnector();
        CreateLabel();
        RefreshLabel();
    }

    void CreateConnector()
    {
        var connectorGO = new GameObject("Connector", typeof(RectTransform));
        connectorGO.transform.SetParent(transform, false);

        var connectorRect = (RectTransform)connectorGO.transform;
        connectorRect.sizeDelta = new Vector2(8, 8);
        connectorRect.anchorMin = Port.IsInput ? new Vector2(0f, 0.5f) : new Vector2(1f, 0.5f);
        connectorRect.anchorMax = connectorRect.anchorMin;
        connectorRect.pivot = new Vector2(0.5f, 0.5f);
        connectorRect.anchoredPosition = Vector2.zero;

        _connectorImage = connectorGO.AddComponent<Image>();
        _connectorImage.sprite = _editor.CustomBG != null ? _editor.CustomBG : null;
        _connectorImage.type = Image.Type.Sliced;
        _connectorImage.raycastTarget = false;

        RefreshColor();
    }

    void CreateLabel()
    {
        _label = Instantiate(UIManager.Instance.LabelPrefab, transform).GetComponent<TextMeshProUGUI>();
        _label.alignment = Port.IsInput ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.MidlineRight;

        var rect = _label.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0, 0);
        rect.anchorMax = new Vector2(1, 1);
        rect.offsetMin = new Vector2(5, 0);
        rect.offsetMax = new Vector2(-5, 0);
    }

    public void RefreshLabel()
    {
        _label.text = Port.fieldName;
        RefreshColor();
    }

    public void RefreshColor()
    {
        if (_connectorImage == null)
            return;

        _connectorImage.color = _editor.GetPortColor(Port.ValueType);
    }

    public void Select(bool selected) => _backgroundImage.color = selected ? _editor.SelectedPortColor : _editor.PortBGColor;

    public void OnPointerClick(PointerEventData eventData) => Node.Editor.OnPortClick(this, eventData);

    public Vector2 GetScreenPosition(Camera camera)
    {
        if (_connectorImage == null)
            return RectTransformUtility.WorldToScreenPoint(camera, transform.position);

        var rect = _connectorImage.rectTransform;
        Vector3 world = rect.TransformPoint(rect.rect.center);

        return RectTransformUtility.WorldToScreenPoint(camera, world);
    }
}