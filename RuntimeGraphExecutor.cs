using UnityEngine;
using UnityEngine.InputSystem;

public class RuntimeGraphExecutor : MonoBehaviour
{
    public RuntimeNodeEditor Editor;
    public string EntryNodeName;
    public bool ExecuteOnStart = true;


    void Start()
    {
        if (ExecuteOnStart)
            Execute();
    }

    void Update()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            Execute();
    }

    // Execute either the configured entry node or every runnable node.
    public void Execute()
    {
        foreach (var node in Editor.Graph.nodes)
            if ((string.IsNullOrEmpty(EntryNodeName) || node.name == EntryNodeName) && node is IRuntimeNodeExecutable runnable)
                runnable.Execute(new RuntimeGraphContext(this, node));
    }
}

// Optional interface for nodes that wish to participate in graph execution.
public interface IRuntimeNodeExecutable
{
    void Execute(RuntimeGraphContext context);
}

// Execution-time helper passed to runnable nodes.
public class RuntimeGraphContext
{
    public RuntimeGraphExecutor Executor;
    public XNode.Node Node;

    public RuntimeGraphContext(RuntimeGraphExecutor executor, XNode.Node node)
    {
        Executor = executor;
        Node = node;
    }

    public T GetInputValue<T>(string portName) => Node.GetInputValue(portName, default(T));
}
