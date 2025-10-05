using UnityEngine;

[CreateNodeMenu("Runtime Samples/Log Value")]
public class LogValueNode : XNode.Node, IRuntimeNodeExecutable
{
    [Input] public float Value;
    [Input(backingValue = ShowBackingValue.Always)] public string Label = "Value";
    [Output] public float Passthrough;

    public override object GetValue(XNode.NodePort port)
    {
        if (port.fieldName == nameof(Passthrough))
            return GetInputValue(nameof(Value), Value);

        return null;
    }

    public void Execute(RuntimeGraphContext context)
    {
        float amount = context.GetInputValue<float>(nameof(Value));
        string label = context.GetInputValue<string>(nameof(Label));

        if (string.IsNullOrEmpty(label))
            label = name;

        Debug.Log($"{label}: {amount}", this);
    }
}