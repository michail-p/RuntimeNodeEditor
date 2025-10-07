using UnityEngine;

public class MessageNode : XNode.Node, IRuntimeNodeExecutable
{
    [Input] public string Value;
    [Output] public string Passthrough;

    public override object GetValue(XNode.NodePort port)
    {
        if (port.fieldName == nameof(Passthrough))
            return GetInputValue(nameof(Value), Value);

        return null;
    }

    public void Execute(RuntimeGraphContext context) => UIManager.Instance.Message(context.GetInputValue<string>(nameof(Value)));
}