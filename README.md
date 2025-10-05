# Runtime Node Editor

This folder contains a light-weight runtime UI for inspecting and editing [xNode](https://github.com/Siccity/xNode) graphs in play mode.

## Components

- **`RuntimeNodeEditor`** – top-level controller that builds a canvas-based workspace at runtime, renders nodes and connections, and exposes tools to add/remove/connect nodes without relying on the editor only tooling.
- **`RuntimeNodeView`** – internal view responsible for drawing a single node card, including input/output columns and drag handling.
- **`RuntimePortView`** – interactive element for each port. Clicking two compatible ports toggles a connection between them.
- **`RuntimeConnectionView`** – draws straight-line connections between ports and keeps them updated while nodes move.
- **`RuntimeGraphExecutor`** – optional helper for running graphs in play mode. It can call any node implementing `IRuntimeNodeExecutable` and expose evaluated outputs for scripting.
- **Sample nodes** (`SampleNodes/`) – a few ready-made xNode nodes (`ConstantFloat`, `Multiply`, `LogValue`) that demonstrate field editing, value propagation, and `IRuntimeNodeExecutable` integration.

## Quick start

1. Add a **Canvas** (Screen Space Overlay works best) to your scene if one does not already exist.
2. Create an empty `RectTransform` child under the canvas and attach the `RuntimeNodeEditor` component.
3. Assign the `Node Graph` asset you want to edit to the component. Enable **Clone Graph Asset** if you want a temporary copy that won't mutate the source asset while playing.
4. (Optional) Configure **Port Colors by Type** and add entries to **Custom Port Colors** if you want specific types to share branding at runtime.
4. Enter Play Mode.

### Runtime controls

- **Drag nodes** by grabbing their card headers.
- **Add nodes** by right-clicking the empty canvas to open the context menu. Every loaded `XNode.Node` type appears in the list, honouring their `CreateNodeMenu` attributes.
- **Connect ports** by clicking two compatible ports in sequence. Clicking an already connected pair removes the link.
- **Remove nodes** with the ✕ button in the node header.
- **Cancel a pending connection** with the Escape key.
- **Edit node fields** directly inside the node card. Basic scalar, string, enum, and boolean fields respect xNode backing-value rules (inputs hide when connected).

All UI elements are generated at runtime, so no prefabs or additional setup are required. The editor also works in builds, enabling basic graph authoring directly in-game.

### Port colors

- Built-in colors exist for `float`, `int`, `string`, `bool`, and a default fallback.
- Use the **Custom Port Colors** list on `RuntimeNodeEditor` to map any other type (including generics and user-defined classes/structs) to a color. Entries can optionally apply to derived types, and changes propagate instantly to existing port widgets.

## Extending

The API intentionally exposes a few entry points:

- Call `RuntimeNodeEditor.SetGraph(graph, clone)` at runtime to swap the active graph.
- Use `RuntimeNodeEditor.CreateNodeInstance(type)` to spawn nodes programmatically.
- Call `RuntimeNodeEditor.RefreshCustomPortColors()` after modifying the custom-color list via script to reapply colors.
- Each `RuntimeNodeView` keeps the underlying graph node’s `position` in sync for serialization.

Feel free to tailor the visuals or behaviours by overriding colors, replacing the runtime-generated layout, or layering additional UI systems on top of the provided components.

## Executing graphs

Add the `RuntimeGraphExecutor` component to a GameObject and either reference an existing `RuntimeNodeEditor` or assign a `NodeGraph` directly. Nodes that should participate in execution can implement:

```csharp
public class MyNode : XNode.Node, IRuntimeNodeExecutable {
	public override object GetValue(NodePort port) { /* calculate output */ }

	public void Execute(RuntimeGraphContext context) {
		var input = context.GetInputValue<float>("Input");
		var result = input * 2f;
		Debug.Log($"Result: {result}");
	}
}
```

Call `RuntimeGraphExecutor.Execute()` (manually or via the *Execute On Start* toggle) to run either the named entry node or all nodes that implement `IRuntimeNodeExecutable`. Use `EvaluateOutput<T>(nodeName, outputPort)` when you simply need a value from the graph without defining the interface.

### Persistence helpers

Runtime changes live only in memory unless you save them back to disk. A typical pattern inside the Unity Editor is:

```csharp
#if UNITY_EDITOR
using UnityEditor;

void SaveGraph(NodeGraph graph) {
	EditorUtility.SetDirty(graph);
	foreach (var node in graph.nodes) {
		if (node != null) EditorUtility.SetDirty(node);
	}
	AssetDatabase.SaveAssets();
}
#endif
```

In builds (or outside the editor) you can persist graphs by serialising them to JSON/binary yourself, or by storing the necessary data in your own save system. These helpers are optional—add them only if you need authoring changes to survive after play mode.
