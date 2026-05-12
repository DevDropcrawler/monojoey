using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class BoardTileController : MonoBehaviour
{
    public enum HighlightKind
    {
        None,
        Selected,
        CurrentPlayer,
        ActiveAuction
    }

    [SerializeField] private string tileId = "tile";
    [SerializeField] private Renderer baseRenderer;
    [SerializeField] private Renderer highlightRenderer;
    [SerializeField] private Renderer ownershipRenderer;
    [SerializeField] private int boardIndex = -1;
    [SerializeField] private Color normalColor = new Color(0.28f, 0.32f, 0.36f, 1f);
    [SerializeField] private Color highlightedColor = new Color(0.95f, 0.78f, 0.24f, 0.65f);
    [SerializeField] private Color currentPlayerHighlightColor = new Color(0.24f, 0.70f, 0.95f, 0.68f);
    [SerializeField] private Color activeAuctionHighlightColor = new Color(0.95f, 0.30f, 0.18f, 0.75f);
    [SerializeField] private Color unownedColor = new Color(0.22f, 0.24f, 0.27f, 1f);
    [SerializeField] private bool enableDebugLogging;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private MaterialPropertyBlock propertyBlock;
    private string ownerPlayerId = "";
    private HighlightKind highlightKind = HighlightKind.None;

    public string TileId => tileId;
    public string OwnerPlayerId => ownerPlayerId;
    public bool IsHighlighted => highlightKind != HighlightKind.None;
    public HighlightKind CurrentHighlightKind => highlightKind;
    public int BoardIndex => boardIndex;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        ResolveRenderers();
        ApplyBaseColor();
        ApplyHighlight();
        ApplyOwnershipColor();
    }

    private void OnValidate()
    {
        ResolveRenderers();
        propertyBlock ??= new MaterialPropertyBlock();
        ApplyBaseColor();
        ApplyHighlight();
        ApplyOwnershipColor();
    }

    public void BindTile(string newTileId, string newOwnerPlayerId, Color ownerColor)
    {
        tileId = string.IsNullOrWhiteSpace(newTileId) ? tileId : newTileId;
        SetOwnership(newOwnerPlayerId, ownerColor);
        ApplyBaseColor();
        LogDebug($"Bound tileId={tileId}, owner={DisplayOwner()}.");
    }

    public void SetBoardIndex(int newBoardIndex)
    {
        boardIndex = newBoardIndex;
        LogDebug($"Board index set to {boardIndex}.");
    }

    public void SetHighlighted(bool highlighted)
    {
        SetHighlightKind(highlighted ? HighlightKind.Selected : HighlightKind.None);
    }

    public void SetHighlightKind(HighlightKind newHighlightKind)
    {
        highlightKind = newHighlightKind;
        ApplyHighlight();
        LogDebug($"Highlight set to {highlightKind}.");
    }

    public void SetOwnership(string newOwnerPlayerId, Color ownerColor)
    {
        ownerPlayerId = string.IsNullOrWhiteSpace(newOwnerPlayerId) ? "" : newOwnerPlayerId;

        if (ownershipRenderer != null)
        {
            ownershipRenderer.enabled = !string.IsNullOrWhiteSpace(ownerPlayerId);
            SetRendererColor(ownershipRenderer, string.IsNullOrWhiteSpace(ownerPlayerId) ? unownedColor : ownerColor);
        }

        LogDebug($"Ownership set to {DisplayOwner()}.");
    }

    public Vector3 GetTokenAnchorPosition(float yOffset)
    {
        return GetTokenAnchorPosition(yOffset, 0, 1);
    }

    public Vector3 GetTokenAnchorPosition(float yOffset, int slotIndex, int slotCount)
    {
        Bounds bounds = baseRenderer != null ? baseRenderer.bounds : new Bounds(transform.position, transform.lossyScale);
        Vector3 center = new Vector3(bounds.center.x, bounds.max.y + yOffset, bounds.center.z);
        if (slotCount <= 1)
        {
            return center;
        }

        int columns = Mathf.CeilToInt(Mathf.Sqrt(slotCount));
        int rows = Mathf.CeilToInt(slotCount / (float)columns);
        int clampedSlot = Mathf.Clamp(slotIndex, 0, Mathf.Max(0, slotCount - 1));
        int row = clampedSlot / columns;
        int column = clampedSlot % columns;
        float spacing = Mathf.Max(0.18f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.42f);
        float xOffset = (column - ((columns - 1) * 0.5f)) * spacing;
        float zOffset = (((rows - 1) * 0.5f) - row) * spacing;
        return center + (transform.right * xOffset) + (transform.forward * zOffset);
    }

    private void ResolveRenderers()
    {
        if (baseRenderer == null)
        {
            baseRenderer = GetComponent<Renderer>();
        }
    }

    private void ApplyBaseColor()
    {
        if (baseRenderer != null)
        {
            SetRendererColor(baseRenderer, normalColor);
        }
    }

    private void ApplyHighlight()
    {
        if (highlightRenderer == null)
        {
            return;
        }

        highlightRenderer.enabled = highlightKind != HighlightKind.None;
        SetRendererColor(highlightRenderer, ColorForHighlight(highlightKind));
    }

    private void ApplyOwnershipColor()
    {
        if (ownershipRenderer != null)
        {
            ownershipRenderer.enabled = !string.IsNullOrWhiteSpace(ownerPlayerId);
        }
    }

    private void SetRendererColor(Renderer targetRenderer, Color color)
    {
        if (targetRenderer == null)
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, color);
        propertyBlock.SetColor(ColorId, color);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    private string DisplayOwner()
    {
        return string.IsNullOrWhiteSpace(ownerPlayerId) ? "unowned" : ownerPlayerId;
    }

    private Color ColorForHighlight(HighlightKind kind)
    {
        switch (kind)
        {
            case HighlightKind.CurrentPlayer:
                return currentPlayerHighlightColor;
            case HighlightKind.ActiveAuction:
                return activeAuctionHighlightColor;
            case HighlightKind.Selected:
                return highlightedColor;
            default:
                return highlightedColor;
        }
    }

    private void LogDebug(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[BoardTileController] {message}", this);
        }
    }
}
