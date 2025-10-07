using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using XNode;
using TMPro;
using System.Linq;

[RequireComponent(typeof(RectTransform))]
public class RuntimeNode : UIBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public XNode.Node Node;
    public RuntimeNodeEditor Editor;

    Dictionary<NodePort, RuntimePort> _portLookup = new();
    [HideInInspector] public List<RuntimePort> _ports = new();
    List<RuntimeField> _fields = new();

    RectTransform _rectTransform;
    RectTransform _inputsContainer;
    RectTransform _outputsContainer;
    RectTransform _fieldsContainer;


    public void Initialize(RuntimeNodeEditor editor, XNode.Node node)
    {
        Editor = editor;
        Node = node;

        _rectTransform = (RectTransform)transform;

        CreateUI();
        CreatePorts();
        SetNodePosition();
    }

    void CreateUI()
    {
        var layoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();
        layoutGroup.padding = new RectOffset(12, 12, 12, 12);
        layoutGroup.spacing = 8f;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childControlHeight = false;

        gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.MinSize;

        var title = Instantiate(UIManager.Instance.LabelPrefab, transform).GetComponent<TextMeshProUGUI>();
        title.alignment = TextAlignmentOptions.MidlineLeft;
        title.text = Node.name.Replace("Node", "");

        CreatePortColumns();
        CreateFieldSection();
    }

    void CreatePortColumns()
    {
        var bodyGO = new GameObject("Body", typeof(RectTransform));
        bodyGO.transform.SetParent(transform, false);

        var bodyLayout = bodyGO.AddComponent<HorizontalLayoutGroup>();
        bodyLayout.spacing = 8f;

        bodyGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.MinSize;

        _inputsContainer = CreatePortColumn(bodyGO.transform, "Inputs", TextAnchor.UpperLeft, Editor.InputColumnColor);
        _outputsContainer = CreatePortColumn(bodyGO.transform, "Outputs", TextAnchor.UpperRight, Editor.OutputColumnColor);
    }

    RectTransform CreatePortColumn(Transform parent, string label, TextAnchor alignment, Color backgroundColor)
    {
        var columnGO = new GameObject(label, typeof(RectTransform));
        columnGO.transform.SetParent(parent, false);

        var image = columnGO.AddComponent<Image>();
        image.sprite = Editor.CustomBG != null ? Editor.CustomBG : null;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 64;
        image.color = backgroundColor;

        var verticalLayout = columnGO.AddComponent<VerticalLayoutGroup>();
        verticalLayout.childAlignment = alignment;
        verticalLayout.spacing = 4f;
        verticalLayout.padding = new RectOffset(6, 6, 6, 6);
        verticalLayout.childForceExpandHeight = false;
        verticalLayout.childControlHeight = false;

        Instantiate(UIManager.Instance.LabelPrefab, columnGO.transform).GetComponent<TextMeshProUGUI>().text = label;

        return (RectTransform)columnGO.transform;
    }

    void CreateFieldSection()
    {
        var fieldsGO = new GameObject("Fields", typeof(RectTransform));
        fieldsGO.transform.SetParent(transform, false);
        _fieldsContainer = (RectTransform)fieldsGO.transform;
        _fieldsContainer.anchorMin = new Vector2(0f, 1f);
        _fieldsContainer.anchorMax = new Vector2(1f, 1f);
        _fieldsContainer.pivot = new Vector2(0.5f, 1f);
        _fieldsContainer.sizeDelta = Vector2.zero;

        var image = fieldsGO.AddComponent<Image>();
        image.sprite = Editor.CustomBG != null ? Editor.CustomBG : null;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 64;
        image.color = new Color32(50, 50, 50, 160);

        var layout = fieldsGO.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 4f;
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.childForceExpandHeight = false;
        layout.childControlHeight = false;

        fieldsGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.MinSize;

        fieldsGO.SetActive(false);
    }

    void SetNodePosition() => _rectTransform.anchoredPosition = Node.position;

    public void CreatePorts()
    {
        Node.UpdatePorts();

        var ports = Node.Ports.ToList();
        var portsToRemove = new List<NodePort>();
        foreach (var port in _portLookup.Keys)
            if (!new HashSet<NodePort>(ports).Contains(port))
                portsToRemove.Add(port);

        foreach (var port in portsToRemove)
            if (_portLookup.TryGetValue(port, out var portUI))
            {
                _ports.Remove(portUI);
                Editor.UnregisterPort(port);

                Destroy(portUI.gameObject);
                _portLookup.Remove(port);
            }

        foreach (var port in ports)
        {
            if (_portLookup.TryGetValue(port, out var portUI))
            {
                portUI.RefreshLabel();
                continue;
            }

            portUI = CreatePort(port);
            _portLookup.Add(port, portUI);
            _ports.Add(portUI);
            Editor.RegisterPort(port, portUI);
        }

        CreateFields();
    }

    RuntimePort CreatePort(NodePort port)
    {
        var go = new GameObject(port.fieldName, typeof(RectTransform));
        go.transform.SetParent(port.IsInput ? _inputsContainer : _outputsContainer, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(0f, 26f);

        var portUI = go.AddComponent<RuntimePort>();
        portUI.Initialize(this, port);

        return portUI;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        transform.SetAsLastSibling();

        // Check for multi-select (Ctrl/Cmd key)
        Editor.SelectNode(this, Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed || Keyboard.current.leftCommandKey.isPressed || Keyboard.current.rightCommandKey.isPressed);
    }

    public void OnBeginDrag(PointerEventData eventData) => transform.SetAsLastSibling();

    public void OnDrag(PointerEventData eventData)
    {
        Vector2 delta = eventData.delta / Editor.Canvas.scaleFactor;

        // If this node is part of multi-selection, move all selected nodes
        if (Editor.SelectedNodes.Contains(this) && Editor.SelectedNodes.Count > 1)
        {
            foreach (var selectedNode in Editor.SelectedNodes)
            {
                selectedNode._rectTransform.anchoredPosition += delta;
                Editor.OnNodePositionChanged(selectedNode.Node, selectedNode._rectTransform.anchoredPosition);
            }
        }
        else
        {
            _rectTransform.anchoredPosition += delta;
            Editor.OnNodePositionChanged(Node, _rectTransform.anchoredPosition);
        }
    }

    public void OnEndDrag(PointerEventData eventData) => Editor.OnNodePositionChanged(Node, _rectTransform.anchoredPosition);

    protected override void OnDestroy()
    {
        base.OnDestroy();

        foreach (var pair in _portLookup)
            Editor.UnregisterPort(pair.Key);

        _portLookup.Clear();
        _ports.Clear();

        foreach (var fieldUI in _fields)
            Destroy(fieldUI.gameObject);

        _fields.Clear();
    }

    void LateUpdate() => UpdateSelectionOutline();

    void UpdateSelectionOutline()
    {
        Outline outline = gameObject.GetComponent<Outline>();
        if (Editor.SelectedNodes.Contains(this) && !outline)
        {
            outline = gameObject.AddComponent<Outline>();
            outline.effectColor = Editor.SelectedOutlineColor;
            outline.effectDistance = new Vector2(2, -2);
        }
        else if (!Editor.SelectedNodes.Contains(this) && outline)
            Destroy(outline);
    }

    void CreateFields()
    {
        foreach (var fieldUI in _fields)
            Destroy(fieldUI.gameObject);

        _fields.Clear();

        foreach (var descriptor in RuntimeNodeReflection.GetFieldList(Node.GetType()))
        {
            var fieldUI = new GameObject(descriptor.Field.Name, typeof(RectTransform));
            fieldUI.transform.SetParent(_fieldsContainer, false);

            var field = fieldUI.AddComponent<RuntimeField>();
            field.Initialize(this, descriptor);
            _fields.Add(field);
        }

        CreateFieldUIs();
    }

    public void CreateFieldUIs()
    {
        bool anyVisible = false;
        foreach (var fieldUI in _fields)
        {
            fieldUI.ShouldShow();

            if (!anyVisible && fieldUI.gameObject.activeSelf)
                anyVisible = true;
        }

        _fieldsContainer.gameObject.SetActive(anyVisible);
    }

    public void OnFieldValueChanged()
    {
        Editor.SetConnectionsDirty();
        CreateFieldUIs();
    }
}