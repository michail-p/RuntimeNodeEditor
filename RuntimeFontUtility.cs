using TMPro;
using UnityEngine;

internal static class RuntimeFontUtility
{
    static TMP_FontAsset cachedFont;

    internal static TMP_FontAsset GetDefaultFont()
    {
        if (cachedFont != null)
            return cachedFont;

        cachedFont = TMP_Settings.defaultFontAsset;
        if (cachedFont == null)
            cachedFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        if (cachedFont == null)
            Debug.LogWarning("RuntimeNodeEditor: Unable to locate a default TMP font asset. Please assign TMP Settings default font or provide one manually.");

        return cachedFont;
    }
}
