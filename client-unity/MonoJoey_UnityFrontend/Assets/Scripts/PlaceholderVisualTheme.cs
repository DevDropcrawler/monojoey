using UnityEngine;

public static class PlaceholderVisualTheme
{
    public static readonly Color BoardSurface = new Color(0.11f, 0.13f, 0.14f, 1f);
    public static readonly Color BoardRail = new Color(0.18f, 0.21f, 0.22f, 1f);
    public static readonly Color PanelBackground = new Color(0.07f, 0.09f, 0.11f, 0.94f);
    public static readonly Color PanelAccent = new Color(0.95f, 0.72f, 0.22f, 1f);
    public static readonly Color SecondaryText = new Color(0.76f, 0.84f, 0.88f, 1f);
    public static readonly Color DisabledText = new Color(0.54f, 0.60f, 0.64f, 1f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    public static Color TileColor(string tileType)
    {
        string normalized = string.IsNullOrWhiteSpace(tileType) ? "" : tileType.Trim().ToLowerInvariant();
        if (normalized.Contains("start"))
        {
            return new Color(0.22f, 0.48f, 0.33f, 1f);
        }

        if (normalized.Contains("chance"))
        {
            return new Color(0.55f, 0.34f, 0.70f, 1f);
        }

        if (normalized.Contains("table"))
        {
            return new Color(0.35f, 0.44f, 0.72f, 1f);
        }

        if (normalized.Contains("utility"))
        {
            return new Color(0.24f, 0.55f, 0.62f, 1f);
        }

        if (normalized.Contains("transport"))
        {
            return new Color(0.34f, 0.36f, 0.40f, 1f);
        }

        if (normalized.Contains("tax") || normalized.Contains("fine"))
        {
            return new Color(0.63f, 0.28f, 0.24f, 1f);
        }

        if (normalized.Contains("lockup") || normalized.Contains("jail"))
        {
            return new Color(0.30f, 0.31f, 0.36f, 1f);
        }

        return new Color(0.42f, 0.49f, 0.44f, 1f);
    }

    public static Color HighlightColor(BoardTileController.HighlightKind kind)
    {
        switch (kind)
        {
            case BoardTileController.HighlightKind.ActiveAuction:
                return new Color(1.00f, 0.34f, 0.18f, 0.84f);
            case BoardTileController.HighlightKind.CurrentPlayer:
                return new Color(0.20f, 0.74f, 1.00f, 0.78f);
            case BoardTileController.HighlightKind.Selected:
                return new Color(1.00f, 0.78f, 0.20f, 0.72f);
            default:
                return Color.clear;
        }
    }

    public static void ApplyRendererColor(Renderer targetRenderer, Color color)
    {
        if (targetRenderer == null)
        {
            return;
        }

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        block.SetColor(ColorId, color);
        targetRenderer.SetPropertyBlock(block);
    }

    public static GameObject CreateRuntimeCubeChild(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Color color)
    {
        GameObject child = GameObject.CreatePrimitive(PrimitiveType.Cube);
        child.name = name;
        child.hideFlags = HideFlags.DontSave;
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = localScale;

        Collider collider = child.GetComponent<Collider>();
        if (collider != null)
        {
            Object.Destroy(collider);
        }

        ApplyRendererColor(child.GetComponent<Renderer>(), color);
        return child;
    }

    public static TextMesh CreateRuntimeTextChild(Transform parent, string name, Vector3 localPosition, float characterSize, int fontSize, Color color)
    {
        GameObject child = new GameObject(name, typeof(TextMesh));
        child.hideFlags = HideFlags.DontSave;
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        child.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        child.transform.localScale = Vector3.one;

        TextMesh text = child.GetComponent<TextMesh>();
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = characterSize;
        text.fontSize = fontSize;
        text.color = color;
        return text;
    }

    public static string CompactLabel(string value, string fallback, int maxLength)
    {
        string label = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        if (label.Length <= maxLength)
        {
            return label;
        }

        return label.Substring(0, Mathf.Max(1, maxLength - 1)) + ".";
    }
}
