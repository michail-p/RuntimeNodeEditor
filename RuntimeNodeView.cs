using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XNode;
using TMPro;

// Visual representation of a single Node at runtime.
[RequireComponent(typeof(RectTransform))]
public class RuntimeNodeView : UIBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public XNode.Node Node { get; set; }
    public RuntimeNodeEditor Editor { get; set; }
    public IReadOnlyList<RuntimePortView> PortViews => portViewList;

    Dictionary<NodePort, RuntimePortView> portViewLookup = new();
    List<RuntimePortView> portViewList = new();
    List<RuntimeFieldView> fieldViews = new();

    RectTransform rectTransform;
    RectTransform inputsContainer;
    RectTransform outputsContainer;
    RectTransform fieldsContainer;
    TextMeshProUGUI titleText;
    Button deleteButton;
    LayoutElement layoutElement;

    static readonly Color InputColumnColor = new Color32(100, 100, 100, 40);
    static readonly Color OutputColumnColor = new Color32(100, 100, 100, 40);


    internal void Initialize(RuntimeNodeEditor editor, XNode.Node node)
    {
        Editor = editor;
        Node = node;

        rectTransform = (RectTransform)transform;

        layoutElement = gameObject.GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = gameObject.AddComponent<LayoutElement>();

        layoutElement.preferredWidth = 260f;
        layoutElement.minWidth = 220f;

        BuildUI();
        RefreshPorts();
        ApplyNodePosition();
    }

    void BuildUI()
    {
        if (!gameObject.TryGetComponent<VerticalLayoutGroup>(out var layoutGroup))
            layoutGroup = gameObject.AddComponent<VerticalLayoutGroup>();

        layoutGroup.childAlignment = TextAnchor.UpperLeft;
        layoutGroup.padding = new RectOffset(12, 12, 12, 12);
        layoutGroup.spacing = 8f;
        layoutGroup.childForceExpandHeight = false;
        layoutGroup.childControlHeight = true;
        layoutGroup.childControlWidth = true;

        if (!gameObject.TryGetComponent<ContentSizeFitter>(out var fitter))
            fitter = gameObject.AddComponent<ContentSizeFitter>();

        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        CreateHeader();
        CreatePortColumns();
        CreateFieldsSection();
    }

    void CreateHeader()
    {
        var headerGO = new GameObject("Header", typeof(RectTransform));
        headerGO.transform.SetParent(transform, false);

        var headerLayout = headerGO.AddComponent<HorizontalLayoutGroup>();
        headerLayout.childAlignment = TextAnchor.MiddleLeft;
        headerLayout.spacing = 8f;
        headerLayout.childForceExpandWidth = true;
        headerLayout.childControlWidth = true;
        headerLayout.childForceExpandHeight = false;

        var titleGO = new GameObject("Title", typeof(RectTransform));
        titleGO.transform.SetParent(headerGO.transform, false);
        titleText = titleGO.AddComponent<TextMeshProUGUI>();
        titleText.font = RuntimeFontUtility.GetDefaultFont();
        titleText.fontSize = 18;
        titleText.color = Color.white;
        titleText.alignment = TextAlignmentOptions.MidlineLeft;
        titleText.textWrappingMode = TextWrappingModes.NoWrap;
        titleText.text = Node != null ? Node.name : "Node";

        var titleLayout = titleGO.AddComponent<LayoutElement>();
        titleLayout.flexibleWidth = 1f;
        titleLayout.minWidth = 120f;

        var deleteGO = new GameObject("Delete", typeof(RectTransform));
        deleteGO.transform.SetParent(headerGO.transform, false);

        var deleteImage = deleteGO.AddComponent<Image>();
        deleteImage.color = new Color32(150, 60, 60, 255);
        deleteButton = deleteGO.AddComponent<Button>();
        deleteButton.onClick.AddListener(() => Editor.RemoveNode(this));

        var deleteTextGO = new GameObject("Label", typeof(RectTransform));
        deleteTextGO.transform.SetParent(deleteGO.transform, false);

        var deleteRect = (RectTransform)deleteTextGO.transform;
        deleteRect.anchorMin = Vector2.zero;
        deleteRect.anchorMax = Vector2.one;
        deleteRect.offsetMin = Vector2.zero;
        deleteRect.offsetMax = Vector2.zero;

        var deleteLabel = deleteTextGO.AddComponent<TextMeshProUGUI>();
        deleteLabel.font = RuntimeFontUtility.GetDefaultFont();
        deleteLabel.text = "x";
        deleteLabel.fontSize = 18;
        deleteLabel.alignment = TextAlignmentOptions.Midline;
        deleteLabel.color = Color.white;
        deleteLabel.textWrappingMode = TextWrappingModes.NoWrap;

        var deleteLayout = deleteGO.AddComponent<LayoutElement>();
        deleteLayout.preferredWidth = 32f;
        deleteLayout.minWidth = 32f;
        deleteLayout.minHeight = 28f;
    }

    void CreatePortColumns()
    {
        var bodyGO = new GameObject("Body", typeof(RectTransform));
        bodyGO.transform.SetParent(transform, false);

        var bodyLayout = bodyGO.AddComponent<HorizontalLayoutGroup>();
        bodyLayout.childAlignment = TextAnchor.UpperLeft;
        bodyLayout.spacing = 8f;
        bodyLayout.childControlHeight = true;
        bodyLayout.childControlWidth = true;
        bodyLayout.childForceExpandWidth = true;
        bodyLayout.childForceExpandHeight = false;

        inputsContainer = CreatePortColumn(bodyGO.transform, "Inputs", TextAnchor.UpperLeft, InputColumnColor);
        outputsContainer = CreatePortColumn(bodyGO.transform, "Outputs", TextAnchor.UpperRight, OutputColumnColor);
    }

    RectTransform CreatePortColumn(Transform parent, string label, TextAnchor alignment, Color backgroundColor)
    {
        var columnGO = new GameObject(label, typeof(RectTransform));
        columnGO.transform.SetParent(parent, false);

        columnGO.AddComponent<Image>().color = backgroundColor;

        var verticalLayout = columnGO.AddComponent<VerticalLayoutGroup>();
        verticalLayout.childAlignment = alignment;
        verticalLayout.spacing = 4f;
        verticalLayout.padding = new RectOffset(6, 6, 6, 6);
        verticalLayout.childControlHeight = true;
        verticalLayout.childForceExpandHeight = false;
        verticalLayout.childForceExpandWidth = true;

        var labelGO = new GameObject("Label", typeof(RectTransform));
        labelGO.transform.SetParent(columnGO.transform, false);

        var labelText = labelGO.AddComponent<TextMeshProUGUI>();
        labelText.font = RuntimeFontUtility.GetDefaultFont();
        labelText.fontSize = 14;
        labelText.color = new Color32(210, 210, 210, 255);
        labelText.alignment = alignment == TextAnchor.UpperLeft ? TextAlignmentOptions.TopLeft : TextAlignmentOptions.TopRight;
        labelText.textWrappingMode = TextWrappingModes.NoWrap;
        labelText.text = label;

        labelGO.AddComponent<LayoutElement>().preferredHeight = 18f;

        return (RectTransform)columnGO.transform;
    }

    void CreateFieldsSection()
    {
        var fieldsGO = new GameObject("Fields", typeof(RectTransform));
        fieldsGO.transform.SetParent(transform, false);
        fieldsContainer = (RectTransform)fieldsGO.transform;
        fieldsContainer.anchorMin = new Vector2(0f, 1f);
        fieldsContainer.anchorMax = new Vector2(1f, 1f);
        fieldsContainer.pivot = new Vector2(0.5f, 1f);
        fieldsContainer.sizeDelta = Vector2.zero;

        fieldsGO.AddComponent<Image>().color = new Color32(50, 50, 50, 160);

        var layout = fieldsGO.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.spacing = 4f;
        layout.padding = new RectOffset(6, 6, 6, 6);
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;

        var fitter = fieldsGO.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        fieldsGO.AddComponent<LayoutElement>().flexibleWidth = 1f;

        fieldsGO.SetActive(false);
    }

    void ApplyNodePosition()
    {
        if (Node != null)
            rectTransform.anchoredPosition = Node.position;
    }

    public void RefreshPorts()
    {
        if (Node == null)
            return;

        Node.UpdatePorts();

        var ports = Node.Ports.ToList();
        var currentPorts = new HashSet<NodePort>(ports);
        foreach (var port in portViewLookup.Keys.Where(port => !currentPorts.Contains(port)).ToList())
        {
            if (!portViewLookup.TryGetValue(port, out var view))
                continue;

            portViewList.Remove(view);
            Editor.UnregisterPortView(port);

            Destroy(view.gameObject);
            portViewLookup.Remove(port);
        }

        foreach (var port in ports)
        {
            if (portViewLookup.TryGetValue(port, out var portView))
            {
                portView.RefreshLabel();
                continue;
            }

            var view = CreatePortView(port);
            portViewLookup.Add(port, view);
            portViewList.Add(view);
            Editor.RegisterPortView(port, view);
        }

        RefreshFields();
    }

    RuntimePortView CreatePortView(NodePort port)
    {
        var go = new GameObject(port.fieldName, typeof(RectTransform));
        go.transform.SetParent(port.IsInput ? inputsContainer : outputsContainer, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(0f, 26f);

        var portView = go.AddComponent<RuntimePortView>();
        portView.Initialize(this, port);

        return portView;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        transform.SetAsLastSibling();
        Editor?.SetSelectedNode(this);
    }

    public void OnBeginDrag(PointerEventData eventData) => transform.SetAsLastSibling();

    public void OnDrag(PointerEventData eventData)
    {
        if (Editor == null || Editor.RootCanvas == null)
            return;

        rectTransform.anchoredPosition += eventData.delta / Editor.RootCanvas.scaleFactor;
        Editor.NotifyNodePositionChanged(Node, rectTransform.anchoredPosition);
    }

    public void OnEndDrag(PointerEventData eventData) => Editor.NotifyNodePositionChanged(Node, rectTransform.anchoredPosition);

    protected override void OnDestroy()
    {
        base.OnDestroy();

        foreach (var pair in portViewLookup)
            Editor?.UnregisterPortView(pair.Key);

        portViewLookup.Clear();
        portViewList.Clear();

        foreach (var fieldView in fieldViews)
            if (fieldView != null)
                Destroy(fieldView.gameObject);

        fieldViews.Clear();
    }

    void LateUpdate()
    {
        if (Node != null && titleText != null)
            titleText.text = Node.name;
    }

    void RefreshFields()
    {
        if (fieldsContainer == null)
            return;

        foreach (var fieldView in fieldViews)
            if (fieldView != null)
                Destroy(fieldView.gameObject);

        fieldViews.Clear();

        if (Node == null)
        {
            fieldsContainer.gameObject.SetActive(false);
            return;
        }

        foreach (var descriptor in RuntimeNodeReflection.GetSerializableFields(Node.GetType()))
        {
            var viewGO = new GameObject(descriptor.Field.Name, typeof(RectTransform));
            viewGO.transform.SetParent(fieldsContainer, false);

            var fieldView = viewGO.AddComponent<RuntimeFieldView>();
            if (fieldView.Initialize(this, descriptor))
                fieldViews.Add(fieldView);
            else
                Destroy(viewGO);
        }

        RefreshFieldViews();
    }

    internal void RefreshFieldViews()
    {
        bool anyVisible = false;
        foreach (var fieldView in fieldViews)
        {
            if (fieldView == null)
                continue;

            fieldView.Refresh();

            if (!anyVisible && fieldView.gameObject.activeSelf)
                anyVisible = true;
        }

        if (fieldsContainer != null)
            fieldsContainer.gameObject.SetActive(anyVisible);
    }

    internal void OnFieldValueCommitted()
    {
        Editor?.SetConnectionsDirty();
        RefreshFieldViews();
    }
}