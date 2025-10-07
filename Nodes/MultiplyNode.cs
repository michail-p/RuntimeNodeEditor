public class MultiplyNode : XNode.Node
{
    [Input] public float A;
    [Input] public float B = 1f;
    [Output] public float Result;

    public override object GetValue(XNode.NodePort port)
    {
        if (port.fieldName != nameof(Result))
            return null;

        return GetInputValue(nameof(A), A) * GetInputValue(nameof(B), B);
    }
}
