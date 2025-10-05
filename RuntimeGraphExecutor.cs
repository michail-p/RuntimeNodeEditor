using System;
using System.Linq;
using UnityEngine;
using XNode;
using UnityEngine.InputSystem;

// Lightweight runner that evaluates or invokes nodes from an xNode graph at runtime.
public class RuntimeGraphExecutor : MonoBehaviour
{
    [Tooltip("Optional runtime editor that owns the graph. If omitted, assign Graph Asset directly.")]
    public RuntimeNodeEditor editor;

    [Tooltip("Optional fallback graph when no RuntimeNodeEditor is provided.")]
    public NodeGraph graphAsset;

    [Tooltip("Name of the node to execute first. Leave empty to execute every runnable node.")]
    public string entryNodeName;

    [Tooltip("Automatically execute when this component starts.")]
    public bool executeOnStart = true;

    public NodeGraph Graph
    {
        get
        {
            if (editor != null)
            {
                if (editor.HasGraph)
                    return editor.Graph;

                if (editor.graphAsset != null)
                    return editor.graphAsset;
            }

            return graphAsset;
        }
    }


    void Start()
    {
        if (executeOnStart)
            Execute();
    }

    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            Execute();
    }

    // Execute either the configured entry node or every runnable node.
    public void Execute()
    {
        var graph = Graph;
        if (graph == null)
        {
            Debug.LogWarning($"[{nameof(RuntimeGraphExecutor)}] No graph available to execute.", this);
            return;
        }

        Debug.Log($"[{nameof(RuntimeGraphExecutor)}] Executing graph with {graph.nodes.Count} nodes.", this);

        if (!string.IsNullOrEmpty(entryNodeName))
        {
            var node = graph.nodes.FirstOrDefault(n => n != null && string.Equals(n.name, entryNodeName, StringComparison.Ordinal));
            if (node == null)
            {
                Debug.LogWarning($"[{nameof(RuntimeGraphExecutor)}] Entry node '{entryNodeName}' not found.", this);
                return;
            }
            Debug.Log($"[{nameof(RuntimeGraphExecutor)}] Executing entry node: {node.name}", this);
            ExecuteNode(node);
        }
        else
        {
            int executedCount = 0;
            foreach (var node in graph.nodes)
            {
                if (node is IRuntimeNodeExecutable)
                {
                    ExecuteNode(node);
                    executedCount++;
                }
            }
            Debug.Log($"[{nameof(RuntimeGraphExecutor)}] Executed {executedCount} runnable nodes out of {graph.nodes.Count} total.", this);
        }
    }

    public T EvaluateOutput<T>(string nodeName, string outputPortName)
    {
        var graph = Graph;
        if (graph == null)
            return default;

        var node = graph.nodes.FirstOrDefault(n => n != null && string.Equals(n.name, nodeName, StringComparison.Ordinal));
        if (node == null)
        {
            Debug.LogWarning($"[{nameof(RuntimeGraphExecutor)}] Node '{nodeName}' not found.", this);
            return default;
        }

        var port = node.GetOutputPort(outputPortName);
        if (port == null)
        {
            Debug.LogWarning($"[{nameof(RuntimeGraphExecutor)}] Node '{nodeName}' does not have output '{outputPortName}'.", this);
            return default;
        }

        object value = port.GetOutputValue();
        if (value == null)
            return default;

        if (value is T tValue)
            return tValue;

        try
        {
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch (Exception)
        {
            Debug.LogWarning($"[{nameof(RuntimeGraphExecutor)}] Unable to cast value from node '{nodeName}' port '{outputPortName}' to {typeof(T).Name}.", this);
            return default;
        }
    }

    void ExecuteNode(XNode.Node node)
    {
        if (node == null)
            return;

        if (node is IRuntimeNodeExecutable runnable)
        {
            Debug.Log($"[{nameof(RuntimeGraphExecutor)}] Executing: {node.name} ({node.GetType().Name})", this);
            runnable.Execute(new RuntimeGraphContext(this, node));
        }
    }

    internal T GetInputValue<T>(XNode.Node node, string portName) => node != null ? node.GetInputValue(portName, default(T)) : default;
}

// Optional interface for nodes that wish to participate in graph execution.
public interface IRuntimeNodeExecutable
{
    void Execute(RuntimeGraphContext context);
}

// Execution-time helper passed to runnable nodes.
public readonly struct RuntimeGraphContext
{
    public RuntimeGraphExecutor Executor { get; }
    public NodeGraph Graph => Executor.Graph;
    public XNode.Node Node { get; }

    public RuntimeGraphContext(RuntimeGraphExecutor executor, XNode.Node node)
    {
        Executor = executor;
        Node = node;
    }

    public T GetInputValue<T>(string portName) => Executor.GetInputValue<T>(Node, portName);
    public T EvaluateOutput<T>(string nodeName, string outputPortName) => Executor.EvaluateOutput<T>(nodeName, outputPortName);
}
