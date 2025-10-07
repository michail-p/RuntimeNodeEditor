using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XNode;
using TMPro;
using UnityEngine.InputSystem;

[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public class RuntimeNodeEditor : UIBehaviour, IPointerDownHandler, IPointerClickHandler
{
    public Canvas Canvas;

    [Header("Graph")]
    public NodeGraph Graph;
    public Sprite CustomBG;

    [Header("Node")]
    public Color NodeBGColor = new Color32(24, 24, 24, 200);
    public Color NodeColor = new Color32(45, 45, 45, 220);
    public Color SelectedPortColor = new Color32(255, 204, 0, 30);
    [Space]
    public Color InputColumnColor = new Color32(100, 100, 100, 40);
    public Color OutputColumnColor = new Color32(100, 100, 100, 40);
    public Color SelectedOutlineColor = new Color32(255, 204, 0, 30);

    [Header("Ports")]
    public Color PortBGColor = new Color32(80, 80, 80, 200);
    public Color ConnectorColor = new Color32(255, 255, 255, 255);
    [Space]
    public Color DefaultColor = new Color32(200, 200, 200, 255);
    public Color StringColor = new Color32(255, 150, 100, 255);
    public Color IntColor = new Color32(100, 255, 150, 255);
    public Color FloatColor = new Color32(100, 150, 255, 255);
    public Color BoolColor = new Color32(255, 100, 150, 255);
    public Color VectorColor = new Color32(110, 210, 255, 255);
    public Color ColorColor = new Color32(255, 140, 220, 255);
    public Color SlotColor = new Color32(190, 140, 255, 255);
    public Color GameObjectColor = new Color32(255, 225, 120, 255);

    [Header("Links")]
    public Color LinkColor = new Color32(255, 255, 255, 180);
    public float LinkThickness = 3f;
    public float BezierTangentStrength = 80f;
    public int BezierSegments = 20;

    [Space]
    public TMP_FontAsset Font;

    RectTransform _nodeContainer;
    [HideInInspector] public RectTransform LinkContainer;

    RectTransform _context;
    Vector2 _contextPos;

    Dictionary<XNode.Node, RuntimeNode> _nodes = new();
    Dictionary<NodePort, RuntimePort> _ports = new();
    List<RuntimeLink> _links = new();
    Dictionary<(NodePort, NodePort), RuntimeLink> _connections = new();

    RuntimePort _pendingPortSelection;
    RuntimeNode _selectedNode;
    [HideInInspector] public HashSet<RuntimeNode> SelectedNodes = new();

    bool _connectionsDirty;
    Vector2 _nextSpawnPos = new(80f, 80f);

    Vector2 _panOffset = Vector2.zero;
    Vector2 _lastMousePos;
    bool _isPanning;


    protected override void Awake()
    {
        base.Awake();

        CreateContainers();
        CreateContext();
    }

    void Update()
    {
        if (_connectionsDirty)
        {
            CreateConnections();
            _connectionsDirty = false;
        }

        if (_pendingPortSelection != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            ClearPortSelection();

        if (_selectedNode != null && Keyboard.current.deleteKey.wasPressedThisFrame)
            DeleteSelectedNodes();

        Pan();
    }

    public void SetGraph(NodeGraph graph)
    {
        Graph = graph;
        BuildGraph();
    }

    void CreateContainers()
    {
        LinkContainer = CreateContainer("Links", GetComponent<RectTransform>());

        _nodeContainer = CreateContainer("Nodes", GetComponent<RectTransform>());
        _nodeContainer.SetSiblingIndex(LinkContainer.GetSiblingIndex() + 1);
    }

    public Camera GetCanvasCamera() => Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Canvas.worldCamera;

    RectTransform CreateContainer(string name, RectTransform parent)
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

    void CreateContext()
    {
        var menuGO = new GameObject("ContextMenu", typeof(RectTransform));
        menuGO.transform.SetParent(Canvas.transform, false);
        _context = (RectTransform)menuGO.transform;

        _context.pivot = new Vector2(0f, 1f);
        _context.sizeDelta = new Vector2(180f, 0f);

        var bg = menuGO.AddComponent<Image>();
        bg.sprite = CustomBG != null ? CustomBG : null;
        bg.type = Image.Type.Sliced;
        bg.pixelsPerUnitMultiplier = 32;
        bg.color = new Color32(32, 32, 32, 240);
        bg.raycastTarget = true;

        var layout = menuGO.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.spacing = 2f;
        layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childForceExpandHeight = false;
        layout.childControlHeight = false;

        menuGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.MinSize;

        foreach (var node in NodeRegistry.Nodes)
        {
            var btn = Instantiate(UIManager.Instance.ButtonPrefab, menuGO.transform);
            btn.transform.Find("Label").GetComponent<TextMeshProUGUI>().text = node.Name.Replace("Node", "");
            btn.GetComponent<Button>().onClick.AddListener(() =>
            {
                CreateNodeInstance(node, _contextPos);
                HideContext();
            });
        }

        _context.gameObject.SetActive(false);
    }

    void HideContext() => _context.gameObject.SetActive(false);

    void BuildGraph()
    {
        foreach (var link in _links)
            Destroy(link.gameObject);

        _links.Clear();

        foreach (var node in _nodes.Values)
            Destroy(node.gameObject);

        _nodes.Clear();
        _ports.Clear();

        foreach (XNode.Node node in Graph.nodes)
            CreateNode(node);

        SetConnectionsDirty();
    }

    void Pan()
    {
        if (Mouse.current.middleButton.isPressed && !_isPanning)
        {
            _isPanning = true;
            _lastMousePos = Mouse.current.position.ReadValue();
        }
        else if (!Mouse.current.middleButton.isPressed && _isPanning)
            _isPanning = false;

        if (Mouse.current.middleButton.isPressed)
        {
            if (!_isPanning)
            {
                _isPanning = true;
                _lastMousePos = Mouse.current.position.ReadValue();
            }
            else
            {
                var currentMousePos = Mouse.current.position.ReadValue();
                _panOffset += (currentMousePos - _lastMousePos) / (Canvas != null ? Canvas.scaleFactor : 1f);
                _nodeContainer.anchoredPosition = _panOffset;
                _lastMousePos = currentMousePos;
            }
        }
        else if (_isPanning)
            _isPanning = false;
    }

    void CreateConnections()
    {
        // Build set of current connections from graph
        var currentConnections = new HashSet<(NodePort, NodePort)>();

        foreach (XNode.Node node in Graph.nodes)
            foreach (var port in node.Ports)
            {
                if (!port.IsOutput)
                    continue;

                foreach (var targetPort in port.GetConnections())
                    currentConnections.Add((port, targetPort));
            }

        // Remove connections that no longer exist
        var toRemove = new List<(NodePort, NodePort)>();
        foreach (var kvp in _connections)
            if (!currentConnections.Contains(kvp.Key))
            {
                toRemove.Add(kvp.Key);
                _links.Remove(kvp.Value);
                Destroy(kvp.Value.gameObject);
            }

        foreach (var key in toRemove)
            _connections.Remove(key);

        // Add new connections
        foreach (var connection in currentConnections)
        {
            if (_connections.ContainsKey(connection))
                continue;

            if (!_ports.TryGetValue(connection.Item1, out var from))
                continue;

            if (!_ports.TryGetValue(connection.Item2, out var to))
                continue;

            _connections[connection] = CreateConnection(from, to);
        }
    }

    RuntimeLink CreateConnection(RuntimePort from, RuntimePort to)
    {
        var go = new GameObject("Connection", typeof(RectTransform));
        go.transform.SetParent(LinkContainer, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(0f, LinkThickness);

        var image = go.AddComponent<Image>();
        image.color = LinkColor;

        var link = go.AddComponent<RuntimeLink>();
        link.Initialize(this, from, to);
        _links.Add(link);

        return link;
    }

    void CreateNode(XNode.Node node, Vector2? screenPosition = null)
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
            go.transform.SetParent(Canvas.transform, false);

            RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)Canvas.transform, screenPosition.Value, GetCanvasCamera(), out Vector3 worldPoint);
            rect.position = worldPoint;

            // Now reparent to nodeLayer (this will maintain world position)
            go.transform.SetParent(_nodeContainer, true);

            // Store the resulting anchored position back to the node for consistency
            node.position = rect.anchoredPosition;
        }
        else
        {
            go.transform.SetParent(_nodeContainer, false);
            rect.anchoredPosition = node.position;
        }

        var image = go.AddComponent<Image>();
        image.sprite = CustomBG != null ? CustomBG : null;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 32;
        image.color = NodeColor;
        image.raycastTarget = true;

        go.AddComponent<CanvasGroup>().blocksRaycasts = true;

        var nodeUI = go.AddComponent<RuntimeNode>();
        nodeUI.Initialize(this, node);

        _nodes.Add(node, nodeUI);
        foreach (var port in nodeUI._ports)
            _ports[port.Port] = port;

        _nextSpawnPos += new Vector2(48f, -48f);
    }

    public void RegisterPort(NodePort port, RuntimePort portUI)
    {
        _ports[port] = portUI;
        SetConnectionsDirty();
    }

    public void UnregisterPort(NodePort port)
    {
        if (_ports.ContainsKey(port))
        {
            _ports.Remove(port);
            SetConnectionsDirty();
        }
    }

    public void OnNodePositionChanged(XNode.Node node, Vector2 anchoredPosition)
    {
        node.position = anchoredPosition;
        SetConnectionsDirty();
    }

    public void OnPortClick(RuntimePort port, PointerEventData eventData)
    {
        if (_pendingPortSelection == port)
        {
            ClearPortSelection();
            return;
        }

        if (_pendingPortSelection == null)
        {
            _pendingPortSelection = port;
            port.Select(true);
            return;
        }

        var firstPort = _pendingPortSelection.Port;
        var secondPort = port.Port;
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

        if (firstPort.ValueType != secondPort.ValueType)
        {
            UIManager.Instance.Message($"Cannot connect {firstPort.node.name}.{firstPort.fieldName} ({GetReadablePortTypeName(firstPort.ValueType)}) -> {secondPort.node.name}.{secondPort.fieldName} ({GetReadablePortTypeName(secondPort.ValueType)}). Port types must match.");
            ClearPortSelection();
            return;
        }

        if (!firstPort.CanConnectTo(secondPort))
        {
            UIManager.Instance.Message($"Cannot connect {firstPort.node.name}.{firstPort.fieldName} -> {secondPort.node.name}.{secondPort.fieldName}. Check port direction and types.");
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

    public void SetConnectionsDirty()
    {
        foreach (var node in _nodes.Values)
            node.CreateFieldUIs();

        _connectionsDirty = true;
    }

    public void SelectNode(RuntimeNode node, bool multiSelect = false)
    {
        if (multiSelect)
        {
            if (SelectedNodes.Contains(node))
                SelectedNodes.Remove(node);
            else
                SelectedNodes.Add(node);
        }
        else
        {
            SelectedNodes.Clear();
            SelectedNodes.Add(node);
        }

        _selectedNode = node;
    }

    public void ClearNodeSelection()
    {
        SelectedNodes.Clear();
        _selectedNode = null;
    }

    public void DeleteSelectedNodes()
    {
        if (SelectedNodes.Count == 0)
            return;

        var nodesToDelete = new List<RuntimeNode>(SelectedNodes);
        SelectedNodes.Clear();
        _selectedNode = null;

        foreach (var node in nodesToDelete)
            DeleteNode(node);
    }

    public void ClearPortSelection()
    {
        _pendingPortSelection?.Select(false);
        _pendingPortSelection = null;
    }

    public void DeleteNode(RuntimeNode node)
    {
        if (_nodes.ContainsKey(node.Node))
            _nodes.Remove(node.Node);

        foreach (var port in node._ports)
            _ports.Remove(port.Port);

        Graph.RemoveNode(node.Node);
        Destroy(node.gameObject);
        SetConnectionsDirty();
    }

    public XNode.Node CreateNodeInstance(Type type, Vector2? screenPosition = null)
    {
        var disallowMultipleAttribute = type.GetCustomAttribute<XNode.Node.DisallowMultipleNodesAttribute>();
        if (disallowMultipleAttribute != null)
        {
            int count = 0;
            foreach (var n in Graph.nodes)
                if (n != null && n.GetType() == type)
                    count++;

            if (count >= disallowMultipleAttribute.max)
            {
                UIManager.Instance.Message($"Disallowed multiple nodes of type {type.Name}");
                return null;
            }
        }

        var node = Graph.AddNode(type);
        node.position = _nextSpawnPos; // Store default, will be overridden by world position
        node.name = type.ToString().Replace("Node", "");
        node.UpdatePorts();

        CreateNode(node, screenPosition);
        SetConnectionsDirty();

        return node;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.pointerCurrentRaycast.gameObject == gameObject)
        {
            ClearNodeSelection();
            ClearPortSelection();
            HideContext();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.pointerCurrentRaycast.gameObject == gameObject && eventData.button == PointerEventData.InputButton.Right)
        {
            HideContext();

            _contextPos = eventData.position;

            // Position directly at mouse
            RectTransformUtility.ScreenPointToWorldPointInRectangle(
                (RectTransform)Canvas.transform,
                eventData.position,
                Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Canvas.worldCamera,
                out Vector3 worldPoint
            );
            _context.position = worldPoint;
            _context.gameObject.SetActive(true);
            _context.SetAsLastSibling();
        }
    }

    string GetReadablePortTypeName(Type portType)
    {
        if (portType == null)
            return "None";

        return portType.Name;
    }

    public Color GetPortColor(Type portType) => portType switch
    {
        null => DefaultColor,
        Type t when t == typeof(string) => StringColor,
        Type t when t == typeof(int) => IntColor,
        Type t when t == typeof(float) => FloatColor,
        Type t when t == typeof(bool) => BoolColor,
        Type t when t == typeof(Vector2) || t == typeof(Vector3) => VectorColor,
        Type t when t == typeof(Color) => ColorColor,
        Type t when t == typeof(Slot) => SlotColor,
        Type t when t == typeof(GameObject) => GameObjectColor,
        _ => DefaultColor
    };
}

[Serializable]
public class CustomPortType
{
    public string Name;
    [HideInInspector] public Type ResolvedType;
    public Color Color = Color.white;
}