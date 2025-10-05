using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XNode;
using TMPro;
using UnityEngine.InputSystem;

// Runtime UI for inspecting and editing NodeGraph assets.
// Attach this component to a RectTransform that lives under a Canvas.
// The editor will build its own minimal UI if none is provided.
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public class RuntimeNodeEditor : UIBehaviour, IPointerDownHandler, IPointerClickHandler
{
    [Header("Graph")]
    public NodeGraph graphAsset;
    public bool cloneGraphAsset = true;

    [Header("Layout")]
    public RectTransform viewport;
    public RectTransform nodeLayer;
    public RectTransform connectionLayer;

    [Header("Appearance")]
    public Color backgroundColor = new Color32(24, 24, 24, 200);
    public Color nodeColor = new Color32(45, 45, 45, 220);
    public Color selectedPortColor = new Color32(255, 204, 0, 255);
    public Color connectionColor = new Color32(255, 255, 255, 180);
    public float connectionThickness = 3f;
    public float bezierTangentStrength = 80f;

    [Header("Port Colors by Type")]
    public Color floatPortColor = new Color32(100, 150, 255, 255);
    public Color intPortColor = new Color32(100, 255, 150, 255);
    public Color stringPortColor = new Color32(255, 150, 100, 255);
    public Color boolPortColor = new Color32(255, 100, 150, 255);
    public Color defaultPortColor = new Color32(200, 200, 200, 255);
    [Tooltip("Custom mappings that override default port colors. Use fully qualified type names (e.g. Namespace.TypeName) for best results.")]
    public List<CustomPortColor> customPortColors = new();

    Dictionary<XNode.Node, RuntimeNodeView> nodeViews = new();
    Dictionary<NodePort, RuntimePortView> portViews = new();
    List<RuntimeConnectionView> connectionViews = new();
    List<NodeTypeInfo> nodeTypes = new();
    readonly List<ResolvedCustomPortColor> resolvedCustomPortColors = new();
    readonly Dictionary<string, Type> customPortTypeCache = new(StringComparer.Ordinal);
    readonly HashSet<string> unresolvedCustomPortTypes = new(StringComparer.Ordinal);

    [HideInInspector] public RectTransform contextMenu;
    Vector2 contextMenuScreenPosition;

    [HideInInspector] public RectTransform cachedRectTransform;
    [HideInInspector] public Canvas rootCanvas;
    [HideInInspector] public Image backgroundImage;

    TMP_FontAsset defaultFont;

    [HideInInspector] public NodeGraph runtimeGraph;
    [HideInInspector] public RuntimePortView pendingPortSelection;
    [HideInInspector] public RuntimeNodeView selectedNode;
    [HideInInspector] public bool connectionsDirty;
    [HideInInspector] public Vector2 nextSpawnPosition = new(80f, 80f);

    Vector2 panOffset = Vector2.zero;
    Vector2 lastMousePosition;
    bool isPanning;

    public struct NodeTypeInfo
    {
        public string DisplayName;
        public Type Type;
        public int Order;
    }

    [Serializable]
    public class CustomPortColor
    {
        [Tooltip("Type name to match. Supports assembly qualified names, full names, or simple names.")]
        public string typeName;
        [Tooltip("Apply this color to types derived from the specified type.")]
        public bool includeSubtypes = true;
        public Color color = Color.white;
    }

    struct ResolvedCustomPortColor
    {
        public Type Type;
        public bool IncludeSubtypes;
        public Color Color;
    }

    public NodeGraph Graph => runtimeGraph;
    public bool HasGraph => runtimeGraph != null;
    internal Canvas RootCanvas => rootCanvas;
    internal Color SelectedPortColor => selectedPortColor;
    internal float ConnectionThickness => connectionThickness;
    internal Color ConnectionColor => connectionColor;
    internal Color NodeColor => nodeColor;
    internal RectTransform ConnectionLayer => connectionLayer;
    internal bool UseBezierConnections => true;
    internal float BezierTangentStrength => bezierTangentStrength;


    protected override void Awake()
    {
        base.Awake();

        cachedRectTransform = (RectTransform)transform;
        rootCanvas = GetComponentInParent<Canvas>();
        defaultFont = RuntimeFontUtility.GetDefaultFont();
        EnsureUIHierarchy();
        RebuildCustomPortColorCache();
        BuildNodeTypeCache();
        InitializeGraph();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        RebuildCustomPortColorCache();
    }
#endif

    protected override void OnEnable()
    {
        base.OnEnable();
        RebuildGraphViews();
    }

    public void SetGraph(NodeGraph graph, bool clone)
    {
        graphAsset = graph;
        cloneGraphAsset = clone;
        InitializeGraph();
        RebuildGraphViews();
    }

    void EnsureUIHierarchy()
    {
        backgroundImage = GetComponent<Image>();
        if (backgroundImage == null)
            backgroundImage = gameObject.AddComponent<Image>();

        backgroundImage.color = backgroundColor;
        backgroundImage.raycastTarget = true;

        if (viewport == null) viewport = cachedRectTransform;
        if (connectionLayer == null)
            connectionLayer = CreateLayer("Connections", viewport);

        if (nodeLayer == null)
        {
            nodeLayer = CreateLayer("Nodes", viewport);
            nodeLayer.SetSiblingIndex(connectionLayer.GetSiblingIndex() + 1);
        }
    }

    RectTransform CreateLayer(string name, RectTransform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);

        return rect;
    }

    void CreateContextMenu(Vector2 screenPosition)
    {
        if (contextMenu != null)
            Destroy(contextMenu.gameObject);

        var menuGO = new GameObject("ContextMenu", typeof(RectTransform));
        menuGO.transform.SetParent(rootCanvas.transform, false);
        contextMenu = (RectTransform)menuGO.transform;

        contextMenu.pivot = new Vector2(0f, 1f);
        contextMenu.sizeDelta = new Vector2(180f, 0f);

        // Position directly at mouse
        RectTransformUtility.ScreenPointToWorldPointInRectangle(
            (RectTransform)rootCanvas.transform,
            screenPosition,
            rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera,
            out Vector3 worldPoint
        );
        contextMenu.position = worldPoint;

        var bg = menuGO.AddComponent<Image>();
        bg.color = new Color32(32, 32, 32, 240);
        bg.raycastTarget = true;

        var layout = menuGO.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.spacing = 2f;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        menuGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Add menu item for each node type
        foreach (var nodeType in nodeTypes)
            CreateContextMenuItem(contextMenu, nodeType.DisplayName, () =>
            {
                CreateNodeInstance(nodeType.Type, contextMenuScreenPosition);
                HideContextMenu();
            });
    }

    void CreateContextMenuItem(RectTransform parent, string label, UnityEngine.Events.UnityAction action)
    {
        var itemGO = new GameObject(label, typeof(RectTransform));
        itemGO.transform.SetParent(parent, false);

        var image = itemGO.AddComponent<Image>();
        image.color = new Color32(50, 50, 50, 255);
        image.raycastTarget = true;

        var button = itemGO.AddComponent<Button>();
        button.onClick.AddListener(action);

        var colors = button.colors;
        colors.normalColor = new Color32(50, 50, 50, 255);
        colors.highlightedColor = new Color32(70, 70, 70, 255);
        colors.pressedColor = new Color32(40, 40, 40, 255);
        button.colors = colors;

        var textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(itemGO.transform, false);

        var rect = (RectTransform)textGO.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 0f);
        rect.offsetMax = new Vector2(-8f, 0f);

        var txt = textGO.AddComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.font = defaultFont;
        txt.fontSize = 14;
        txt.color = Color.white;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        txt.textWrappingMode = TextWrappingModes.NoWrap;

        var layout = itemGO.AddComponent<LayoutElement>();
        layout.minHeight = 28f;
    }

    void HideContextMenu()
    {
        if (contextMenu != null)
        {
            Destroy(contextMenu.gameObject);
            contextMenu = null;
        }
    }

    void BuildNodeTypeCache()
    {
        nodeTypes.Clear();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic)
                continue;

            Type[] types = Array.Empty<Type>();
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                types = e.Types.Where(t => t != null).ToArray();
            }

            foreach (var type in types)
            {
                if (type == null || type.IsAbstract)
                    continue;

                if (!typeof(XNode.Node).IsAssignableFrom(type))
                    continue;

                var menu = type.GetCustomAttribute<XNode.Node.CreateNodeMenuAttribute>();
                if (menu != null && string.IsNullOrEmpty(menu.menuName))
                    continue;

                nodeTypes.Add(new NodeTypeInfo
                {
                    DisplayName = menu?.menuName ?? SplitTypeName(type.Name),
                    Order = menu?.order ?? 0,
                    Type = type
                });
            }
        }

        nodeTypes.Sort((a, b) =>
        {
            int orderCompare = a.Order.CompareTo(b.Order);
            if (orderCompare != 0)
                return orderCompare;

            return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
        });
    }

    static string SplitTypeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        var parts = new List<char>(name.Length + 8);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (i > 0 && char.IsUpper(c) && char.IsLower(name[i - 1]))
                parts.Add(' ');

            parts.Add(c);
        }

        return new string(parts.ToArray());
    }

    void InitializeGraph()
    {
        runtimeGraph = null;
        if (graphAsset == null)
            return;

        runtimeGraph = cloneGraphAsset ? graphAsset.Copy() : graphAsset;
        if (runtimeGraph != null)
            runtimeGraph.name = cloneGraphAsset ? graphAsset.name + " (Runtime)" : runtimeGraph.name;
    }

    void RebuildGraphViews()
    {
        foreach (var view in connectionViews)
            if (view != null)
                Destroy(view.gameObject);

        connectionViews.Clear();

        foreach (var view in nodeViews.Values)
            if (view != null)
                Destroy(view.gameObject);

        nodeViews.Clear();
        portViews.Clear();

        if (!HasGraph)
            return;

        foreach (XNode.Node node in runtimeGraph.nodes)
        {
            if (node == null)
                continue;

            CreateNodeView(node);
        }

        SetConnectionsDirty();
    }

    void Update()
    {
        if (connectionsDirty)
        {
            RebuildConnections();
            connectionsDirty = false;
        }

        if (pendingPortSelection != null && WasCancelPressed())
            ClearPortSelection();

        if (selectedNode != null && WasDeletePressed())
            DeleteSelectedNode();

        HandlePanning();
    }

    void HandlePanning()
    {
        if (Keyboard.current == null)
            return;

        bool middleMouseDown = Mouse.current != null && Mouse.current.middleButton.isPressed;

        if (middleMouseDown && !isPanning)
        {
            isPanning = true;
            lastMousePosition = Mouse.current.position.ReadValue();
        }
        else if (!middleMouseDown && isPanning)
            isPanning = false;

        if (isPanning && Mouse.current != null)
        {
            Vector2 currentMousePosition = Mouse.current.position.ReadValue();
            Vector2 delta = currentMousePosition - lastMousePosition;
            lastMousePosition = currentMousePosition;

            panOffset += delta / (rootCanvas != null ? rootCanvas.scaleFactor : 1f);
            if (nodeLayer != null)
                nodeLayer.anchoredPosition = panOffset;
        }
    }

    void RebuildConnections()
    {
        foreach (var view in connectionViews)
            if (view != null) Destroy(view.gameObject);

        connectionViews.Clear();

        foreach (XNode.Node node in runtimeGraph.nodes)
        {
            if (node == null)
                continue;

            foreach (var port in node.Ports)
            {
                if (!port.IsOutput)
                    continue;

                if (!portViews.TryGetValue(port, out var fromView))
                    continue;

                foreach (var targetPort in port.GetConnections())
                {
                    if (targetPort == null)
                        continue;

                    if (!portViews.TryGetValue(targetPort, out var toView))
                        continue;

                    CreateConnection(fromView, toView);
                }
            }
        }
    }

    void CreateConnection(RuntimePortView from, RuntimePortView to)
    {
        var go = new GameObject("Connection", typeof(RectTransform));
        go.transform.SetParent(connectionLayer, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(0f, connectionThickness);

        go.AddComponent<Image>().color = connectionColor;

        var connectionView = go.AddComponent<RuntimeConnectionView>();
        connectionView.Initialize(this, from, to);
        connectionViews.Add(connectionView);
    }

    void CreateNodeView(XNode.Node node, Vector2? screenPosition = null)
    {
        var go = new GameObject(node.name, typeof(RectTransform));

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(260f, 140f);

        // Position node using screen position if provided, otherwise use node.position
        if (screenPosition.HasValue)
        {
            // Temporarily parent to canvas to set world position correctly
            go.transform.SetParent(rootCanvas.transform, false);

            Camera cam = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera;
            RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)rootCanvas.transform, screenPosition.Value, cam, out Vector3 worldPoint);
            rect.position = worldPoint;

            // Now reparent to nodeLayer (this will maintain world position)
            go.transform.SetParent(nodeLayer, true);

            // Store the resulting anchored position back to the node for consistency
            node.position = rect.anchoredPosition;
        }
        else
        {
            go.transform.SetParent(nodeLayer, false);
            rect.anchoredPosition = node.position != default ? node.position : nextSpawnPosition;
        }

        var image = go.AddComponent<Image>();
        image.color = nodeColor;
        image.raycastTarget = true;

        go.AddComponent<CanvasGroup>().blocksRaycasts = true;

        var nodeView = go.AddComponent<RuntimeNodeView>();
        nodeView.Initialize(this, node);

        nodeViews.Add(node, nodeView);
        foreach (var portView in nodeView.PortViews)
            portViews[portView.Port] = portView;

        nextSpawnPosition += new Vector2(48f, -48f);
    }

    internal void RegisterPortView(NodePort port, RuntimePortView view)
    {
        portViews[port] = view;
        SetConnectionsDirty();
    }

    internal void UnregisterPortView(NodePort port)
    {
        if (portViews.ContainsKey(port))
        {
            portViews.Remove(port);
            SetConnectionsDirty();
        }
    }

    internal void NotifyNodePositionChanged(XNode.Node node, Vector2 anchoredPosition)
    {
        node.position = anchoredPosition;
        SetConnectionsDirty();
    }

    internal void HandlePortClicked(RuntimePortView view, PointerEventData eventData)
    {
        if (pendingPortSelection == view)
        {
            ClearPortSelection();
            return;
        }

        if (pendingPortSelection == null)
        {
            pendingPortSelection = view;
            view.SetSelected(true);
            return;
        }

        var firstPort = pendingPortSelection.Port;
        var secondPort = view.Port;
        if (firstPort == null || secondPort == null)
        {
            ClearPortSelection();
            return;
        }

        if (firstPort == secondPort)
        {
            ClearPortSelection();
            return;
        }

        if (!firstPort.CanConnectTo(secondPort))
        {
            Debug.LogWarning($"RuntimeNodeEditor: Cannot connect {firstPort.node.name}.{firstPort.fieldName} -> {secondPort.node.name}.{secondPort.fieldName}. Check port direction and types.");
            ClearPortSelection();
            return;
        }

        // Determine output -> input orientation
        NodePort output = firstPort.IsOutput ? firstPort : secondPort;
        NodePort input = firstPort.IsInput ? firstPort : secondPort;

        if (output.IsConnectedTo(input))
            output.Disconnect(input);
        else
            output.Connect(input);

        ClearPortSelection();
        SetConnectionsDirty();
    }

    internal void SetConnectionsDirty()
    {
        foreach (var nodeView in nodeViews.Values)
            if (nodeView != null)
                nodeView.RefreshFieldViews();

        connectionsDirty = true;
    }

    internal void SetSelectedNode(RuntimeNodeView nodeView) => selectedNode = nodeView;

    public void ClearPortSelection()
    {
        if (pendingPortSelection != null)
        {
            pendingPortSelection.SetSelected(false);
            pendingPortSelection = null;
        }
    }

    public void RemoveNode(RuntimeNodeView view)
    {
        if (!HasGraph)
            return;

        if (view == null || view.Node == null)
            return;

        if (nodeViews.ContainsKey(view.Node))
            nodeViews.Remove(view.Node);

        foreach (var portView in view.PortViews)
        {
            if (portView == null)
                continue;

            portViews.Remove(portView.Port);
        }

        runtimeGraph.RemoveNode(view.Node);
        Destroy(view.gameObject);
        SetConnectionsDirty();
    }

    public XNode.Node CreateNodeInstance(Type type, Vector2? screenPosition = null)
    {
        if (type == null || !typeof(XNode.Node).IsAssignableFrom(type))
            return null;

        if (!HasGraph)
            return null;

        var attribute = type.GetCustomAttribute<XNode.Node.DisallowMultipleNodesAttribute>();
        if (attribute != null)
            if (runtimeGraph.nodes.Count(n => n != null && n.GetType() == type) >= attribute.max)
            {
                Debug.LogWarning($"Disallowed multiple nodes of type {type.Name}");
                return null;
            }

        var node = runtimeGraph.AddNode(type);
        if (node == null)
            return null;

        node.name = GenerateUniqueNodeName(type.Name);
        node.position = nextSpawnPosition; // Store default, will be overridden by world position
        node.UpdatePorts();

        CreateNodeView(node, screenPosition);
        SetConnectionsDirty();

        return node;
    }

    string GenerateUniqueNodeName(string baseName)
    {
        string candidate = baseName;
        int suffix = 1;
        while (runtimeGraph.nodes.Any(n => n != null && n.name == candidate))
        {
            candidate = $"{baseName} {suffix}";
            suffix++;
        }

        return candidate;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData == null)
            return;

        if (eventData.pointerCurrentRaycast.gameObject == gameObject)
        {
            selectedNode = null;
            ClearPortSelection();
            HideContextMenu();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData == null || eventData.button != PointerEventData.InputButton.Right)
            return;

        if (eventData.pointerCurrentRaycast.gameObject == gameObject)
        {
            HideContextMenu();

            // Store screen position for node spawning
            contextMenuScreenPosition = eventData.position;

            CreateContextMenu(eventData.position);
        }
    }

    bool WasCancelPressed() => Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;

    bool WasDeletePressed() => Keyboard.current != null && Keyboard.current.deleteKey.wasPressedThisFrame;

    void DeleteSelectedNode()
    {
        if (selectedNode == null || selectedNode.Node == null)
            return;

        var node = selectedNode.Node;

        // Clear selection
        selectedNode = null;

        // Remove from graph
        if (runtimeGraph != null)
            runtimeGraph.RemoveNode(node);

        // Remove view
        if (nodeViews.ContainsKey(node))
        {
            var view = nodeViews[node];
            nodeViews.Remove(node);

            // Unregister all port views
            foreach (var portView in view.PortViews)
                if (portViews.ContainsKey(portView.Port))
                    portViews.Remove(portView.Port);

            Destroy(view.gameObject);
        }

        SetConnectionsDirty();
    }

    public Color GetPortColor(Type portType)
    {
        if (TryGetCustomPortColor(portType, out var customColor))
            return customColor;

        if (portType == null)
            return defaultPortColor;

        if (portType == typeof(float) || portType == typeof(double))
            return floatPortColor;
        if (portType == typeof(int) || portType == typeof(long) || portType == typeof(short) || portType == typeof(byte))
            return intPortColor;
        if (portType == typeof(string))
            return stringPortColor;
        if (portType == typeof(bool))
            return boolPortColor;

        return defaultPortColor;
    }

    void RebuildCustomPortColorCache()
    {
        resolvedCustomPortColors.Clear();
        customPortTypeCache.Clear();
        unresolvedCustomPortTypes.Clear();

        if (customPortColors == null)
            return;

        foreach (var entry in customPortColors)
        {
            if (entry == null)
                continue;

            var type = ResolveCustomPortType(entry.typeName);
            if (type == null)
            {
                if (!string.IsNullOrWhiteSpace(entry.typeName) && unresolvedCustomPortTypes.Add(entry.typeName))
                    Debug.LogWarning($"RuntimeNodeEditor: Unable to resolve type '{entry.typeName}' for custom port color.", this);
                continue;
            }

            resolvedCustomPortColors.Add(new ResolvedCustomPortColor
            {
                Type = type,
                IncludeSubtypes = entry.includeSubtypes,
                Color = entry.color
            });
        }

        RefreshAllPortViewColors();
    }

    Type ResolveCustomPortType(string typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
            return null;

        typeName = typeName.Trim();
        if (customPortTypeCache.TryGetValue(typeName, out var cached))
            return cached;

        Type resolved = Type.GetType(typeName, throwOnError: false, ignoreCase: false);

        if (resolved == null)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic)
                    continue;

                try
                {
                    resolved = assembly.GetType(typeName, false, false);
                    if (resolved != null)
                        break;
                }
                catch (ReflectionTypeLoadException e)
                {
                    foreach (var candidate in e.Types)
                    {
                        if (candidate == null)
                            continue;

                        if (string.Equals(candidate.FullName, typeName, StringComparison.Ordinal) ||
                            string.Equals(candidate.Name, typeName, StringComparison.Ordinal))
                        {
                            resolved = candidate;
                            break;
                        }
                    }
                }

                if (resolved != null)
                    break;

                Type[] types = Array.Empty<Type>();
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException e)
                {
                    types = e.Types.Where(t => t != null).ToArray();
                }

                resolved = types.FirstOrDefault(t => string.Equals(t.FullName, typeName, StringComparison.Ordinal));
                if (resolved != null)
                    break;

                resolved = types.FirstOrDefault(t => string.Equals(t.Name, typeName, StringComparison.Ordinal));
                if (resolved != null)
                    break;
            }
        }

        customPortTypeCache[typeName] = resolved;
        return resolved;
    }

    bool TryGetCustomPortColor(Type portType, out Color color)
    {
        if (portType == null)
        {
            color = default;
            return false;
        }

        foreach (var entry in resolvedCustomPortColors)
        {
            if (entry.Type == null)
                continue;

            if (portType == entry.Type || (entry.IncludeSubtypes && entry.Type.IsAssignableFrom(portType)))
            {
                color = entry.Color;
                return true;
            }
        }

        if (portType.IsGenericType)
        {
            var definition = portType.GetGenericTypeDefinition();
            foreach (var entry in resolvedCustomPortColors)
            {
                if (entry.Type == null)
                    continue;

                if (definition == entry.Type || (entry.IncludeSubtypes && entry.Type.IsAssignableFrom(definition)))
                {
                    color = entry.Color;
                    return true;
                }
            }
        }

        color = default;
        return false;
    }

    void RefreshAllPortViewColors()
    {
        foreach (var view in portViews.Values)
        {
            if (view == null)
                continue;

            view.RefreshColor();
        }
    }

    public void RefreshCustomPortColors() => RebuildCustomPortColorCache();
}