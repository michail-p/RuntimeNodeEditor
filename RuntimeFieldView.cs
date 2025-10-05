using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using XNode;
using TMPro;
using Debug = UnityEngine.Debug;

// UI widget that binds a node field to a runtime-editable control.
[RequireComponent(typeof(RectTransform))]
internal class RuntimeFieldView : UIBehaviour
{
    RuntimeNodeView nodeView;
    RuntimeNodeReflection.FieldDescriptor descriptor;
    NodePort associatedPort;

    TextMeshProUGUI label;
    TMP_InputField inputField;
    Toggle toggle;

    bool suppressEvents;


    internal bool Initialize(RuntimeNodeView host, RuntimeNodeReflection.FieldDescriptor descriptor)
    {
        nodeView = host;
        this.descriptor = descriptor;
        associatedPort = host.Node != null ? host.Node.GetPort(descriptor.Field.Name) : null;

        BuildLayout();
        return SetupControl();
    }

    void BuildLayout()
    {
        var rect = (RectTransform)transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, 28f);

        var layout = gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.spacing = 8f;
        layout.padding = new RectOffset(4, 4, 2, 2);
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        var background = gameObject.AddComponent<Image>();
        background.color = new Color32(55, 55, 55, 200);

        label = CreateLabel("Label", descriptor.NicifiedName + ":");
        var labelLayout = label.GetComponent<LayoutElement>();
        labelLayout.minWidth = 90f;
        labelLayout.preferredWidth = 110f;
    }

    bool SetupControl()
    {
        Type fieldType = descriptor.Field.FieldType;

        if (fieldType == typeof(bool))
        {
            toggle = CreateToggle();
            Refresh();
            return true;
        }

        if (fieldType == typeof(string) ||
            fieldType == typeof(int) ||
            fieldType == typeof(float) ||
            fieldType == typeof(double) ||
            fieldType == typeof(long) ||
            fieldType == typeof(uint) ||
            fieldType == typeof(short) ||
            fieldType == typeof(ushort) ||
            fieldType == typeof(byte) ||
            fieldType == typeof(sbyte) ||
            fieldType.IsEnum)
        {
            inputField = CreateInputField(fieldType);
            Refresh();
            return true;
        }

        // Unsupported type – just show a message and disable the row.
        label.text += " (Unsupported)";
        gameObject.SetActive(false);

        return false;
    }

    TextMeshProUGUI CreateLabel(string name, string text)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);

        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.font = RuntimeFontUtility.GetDefaultFont();
        txt.fontSize = 14;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        txt.color = Color.white;
        txt.text = text;

        go.AddComponent<LayoutElement>();
        return txt;
    }

    Toggle CreateToggle()
    {
        var toggleGO = new GameObject("Toggle", typeof(RectTransform));
        toggleGO.transform.SetParent(transform, false);

        var toggleRect = (RectTransform)toggleGO.transform;
        toggleRect.sizeDelta = new Vector2(24f, 24f);
        toggleRect.anchorMin = new Vector2(0f, 0.5f);
        toggleRect.anchorMax = new Vector2(0f, 0.5f);
        toggleRect.pivot = new Vector2(0f, 0.5f);

        var backgroundGO = new GameObject("Background", typeof(RectTransform));
        backgroundGO.transform.SetParent(toggleGO.transform, false);

        var backgroundRect = (RectTransform)backgroundGO.transform;
        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        var backgroundImage = backgroundGO.AddComponent<Image>();
        backgroundImage.color = new Color32(30, 30, 30, 255);

        var checkmarkGO = new GameObject("Checkmark", typeof(RectTransform));
        checkmarkGO.transform.SetParent(backgroundGO.transform, false);

        var checkmarkRect = (RectTransform)checkmarkGO.transform;
        checkmarkRect.anchorMin = new Vector2(0.2f, 0.2f);
        checkmarkRect.anchorMax = new Vector2(0.8f, 0.8f);
        checkmarkRect.offsetMin = Vector2.zero;
        checkmarkRect.offsetMax = Vector2.zero;

        var checkmarkImage = checkmarkGO.AddComponent<Image>();
        checkmarkImage.color = Color.white;

        var toggleComponent = toggleGO.AddComponent<Toggle>();
        toggleComponent.targetGraphic = backgroundImage;
        toggleComponent.graphic = checkmarkImage;
        toggleComponent.onValueChanged.AddListener(OnToggleValueChanged);

        var layout = toggleGO.AddComponent<LayoutElement>();
        layout.preferredWidth = 28f;
        layout.minWidth = 28f;

        return toggleComponent;
    }

    TMP_InputField CreateInputField(Type fieldType)
    {
        var inputGO = new GameObject("Input", typeof(RectTransform));
        inputGO.transform.SetParent(transform, false);

        var rect = (RectTransform)inputGO.transform;
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(0f, 24f);

        var background = inputGO.AddComponent<Image>();
        background.color = new Color32(30, 30, 30, 255);

        var input = inputGO.AddComponent<TMP_InputField>();
        input.transition = Selectable.Transition.ColorTint;
        input.textComponent = CreateInputText(input.transform, "Text", TextAlignmentOptions.MidlineLeft);
        input.placeholder = CreateInputText(input.transform, "Placeholder", TextAlignmentOptions.MidlineLeft, new Color32(150, 150, 150, 255), "(value)");
        input.onEndEdit.AddListener(OnInputFieldEndEdit);

        if (fieldType == typeof(int) ||
            fieldType == typeof(long) ||
            fieldType == typeof(short) ||
            fieldType == typeof(byte) ||
            fieldType == typeof(uint) ||
            fieldType == typeof(ushort) ||
            fieldType == typeof(sbyte)
        )
            input.contentType = TMP_InputField.ContentType.IntegerNumber;
        else if (fieldType == typeof(float) || fieldType == typeof(double))
            input.contentType = TMP_InputField.ContentType.DecimalNumber;
        else
            input.contentType = TMP_InputField.ContentType.Standard;

        var layout = inputGO.AddComponent<LayoutElement>();
        layout.flexibleWidth = 1f;
        layout.minWidth = 120f;

        return input;
    }

    TextMeshProUGUI CreateInputText(Transform parent, string name, TextAlignmentOptions alignment, Color? color = null, string text = "")
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(6f, 1f);
        rect.offsetMax = new Vector2(-6f, -1f);

        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.font = RuntimeFontUtility.GetDefaultFont();
        txt.fontSize = 14;
        txt.alignment = alignment;
        txt.textWrappingMode = TextWrappingModes.NoWrap;
        txt.color = color ?? Color.white;
        txt.text = text;

        return txt;
    }

    internal void Refresh()
    {
        if (nodeView == null || nodeView.Node == null)
            return;

        bool shouldShow = ShouldDisplay();
        if (!shouldShow)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        var value = descriptor.Field.GetValue(nodeView.Node);

        suppressEvents = true;
        try
        {
            if (toggle != null)
            {
                toggle.isOn = value is bool b && b;
                toggle.interactable = IsEditable();
            }
            else if (inputField != null)
            {
                inputField.interactable = IsEditable();
                inputField.text = FormatValue(value);
            }
        }
        finally
        {
            suppressEvents = false;
        }
    }

    bool ShouldDisplay()
    {
        XNode.Node.InputAttribute inputAttr = descriptor.Input;
        XNode.Node.OutputAttribute outputAttr = descriptor.Output;

        if (inputAttr != null)
            switch (inputAttr.backingValue)
            {
                case XNode.Node.ShowBackingValue.Never:
                    return false;
                case XNode.Node.ShowBackingValue.Unconnected:
                    if (associatedPort != null && associatedPort.IsConnected) return false;
                    break;
            }

        if (outputAttr != null && outputAttr.backingValue == XNode.Node.ShowBackingValue.Never)
            return false;

        return true;
    }

    bool IsEditable()
    {
        // Output-only fields are editable if they have ShowBackingValue.Always
        if (descriptor.Output != null && descriptor.Input == null)
            return descriptor.Output.backingValue == XNode.Node.ShowBackingValue.Always;

        if (associatedPort != null && descriptor.Input != null && associatedPort.IsConnected)
            return false;

        return true;
    }

    void OnInputFieldEndEdit(string text)
    {
        if (suppressEvents || nodeView == null || nodeView.Node == null)
            return;

        if (!TryParse(descriptor.Field.FieldType, text, out object newValue))
        {
            Debug.LogWarning($"RuntimeFieldView.OnInputFieldEndEdit: Failed to parse '{text}' as {descriptor.Field.FieldType}");
            Refresh();
            return;
        }

        descriptor.Field.SetValue(nodeView.Node, newValue);
        nodeView.OnFieldValueCommitted();
        Refresh();
    }

    void OnToggleValueChanged(bool value)
    {
        if (suppressEvents || nodeView == null || nodeView.Node == null)
            return;

        descriptor.Field.SetValue(nodeView.Node, value);
        nodeView.OnFieldValueCommitted();
    }

    static bool TryParse(Type fieldType, string value, out object result)
    {
        value ??= string.Empty;

        if (fieldType == typeof(string))
        {
            result = value;
            return true;
        }

        if (fieldType == typeof(int) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt))
        {
            result = parsedInt;
            return true;
        }
        else if (fieldType == typeof(float) && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedFloat))
        {
            result = parsedFloat;
            return true;
        }
        else if (fieldType == typeof(double) && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedDouble))
        {
            result = parsedDouble;
            return true;
        }
        else if (fieldType == typeof(long) && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedLong))
        {
            result = parsedLong;
            return true;
        }
        else if (fieldType == typeof(uint) && uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedUInt))
        {
            result = parsedUInt;
            return true;
        }
        else if (fieldType == typeof(short) && short.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedShort))
        {
            result = parsedShort;
            return true;
        }
        else if (fieldType == typeof(ushort) && ushort.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedUShort))
        {
            result = parsedUShort;
            return true;
        }
        else if (fieldType == typeof(byte) && byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedByte))
        {
            result = parsedByte;
            return true;
        }
        else if (fieldType == typeof(sbyte) && sbyte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSByte))
        {
            result = parsedSByte;
            return true;
        }
        else if (fieldType.IsEnum && Enum.TryParse(fieldType, value, true, out var parsedEnum))
        {
            result = parsedEnum;
            return true;
        }

        result = null;
        return false;
    }

    static string FormatValue(object value)
    {
        if (value == null)
            return string.Empty;

        return value switch
        {
            float f => f.ToString("0.###", CultureInfo.InvariantCulture),
            double d => d.ToString("0.###", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture),
        };
    }
}