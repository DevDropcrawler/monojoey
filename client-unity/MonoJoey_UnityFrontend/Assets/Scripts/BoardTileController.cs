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
    [SerializeField] private string displayName = "";
    [SerializeField] private string tileType = "";
    [SerializeField] private int price;
    [SerializeField] private bool isPurchasable;
    [SerializeField] private bool isAuctionable;
    [SerializeField] private Renderer baseRenderer;
    [SerializeField] private Renderer highlightRenderer;
    [SerializeField] private Renderer ownershipRenderer;
    [SerializeField] private int boardIndex = -1;
    [SerializeField] private Color normalColor = new Color(0.28f, 0.32f, 0.36f, 1f);
    [SerializeField] private Color highlightedColor = new Color(0.95f, 0.78f, 0.24f, 0.65f);
    [SerializeField] private Color currentPlayerHighlightColor = new Color(0.24f, 0.70f, 0.95f, 0.68f);
    [SerializeField] private Color activeAuctionHighlightColor = new Color(0.95f, 0.30f, 0.18f, 0.75f);
    [SerializeField] private Color unownedColor = new Color(0.22f, 0.24f, 0.27f, 1f);
    [SerializeField] private bool enablePlaceholderVisualPolish = true;
    [SerializeField] private bool enableDebugLogging;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private MaterialPropertyBlock propertyBlock;
    private string ownerPlayerId = "";
    private HighlightKind highlightKind = HighlightKind.None;
    private Transform placeholderVisualRoot;
    private Renderer tileTypeBandRenderer;
    private Renderer placeholderBorderRenderer;
    private TextMesh titleLabel;
    private TextMesh detailLabel;

    public string TileId => tileId;
    public string DisplayName => displayName;
    public string TileType => tileType;
    public int Price => price;
    public bool IsPurchasable => isPurchasable;
    public bool IsAuctionable => isAuctionable;
    public string OwnerPlayerId => ownerPlayerId;
    public bool IsHighlighted => highlightKind != HighlightKind.None;
    public HighlightKind CurrentHighlightKind => highlightKind;
    public int BoardIndex => boardIndex;
    public bool HasPlaceholderVisualPolish => placeholderVisualRoot != null && titleLabel != null && tileTypeBandRenderer != null;
    public string TitleLabelText => titleLabel == null ? "" : titleLabel.text;
    public string DetailLabelText => detailLabel == null ? "" : detailLabel.text;

    private void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();
        ResolveRenderers();
        EnsurePlaceholderVisuals();
        ApplyBaseColor();
        ApplyHighlight();
        ApplyOwnershipColor();
        RefreshPlaceholderVisuals();
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
        RefreshPlaceholderVisuals();
        LogDebug($"Bound tileId={tileId}, owner={DisplayOwner()}.");
    }

    public void BindTileSnapshot(MonoJoeyBoardTileSnapshot snapshotTile, Color ownerColor)
    {
        if (snapshotTile == null)
        {
            return;
        }

        tileId = string.IsNullOrWhiteSpace(snapshotTile.tileId) ? tileId : snapshotTile.tileId;
        displayName = string.IsNullOrWhiteSpace(snapshotTile.displayName) ? tileId : snapshotTile.displayName;
        tileType = string.IsNullOrWhiteSpace(snapshotTile.tileType) ? "property" : snapshotTile.tileType;
        price = snapshotTile.price;
        isPurchasable = snapshotTile.isPurchasable;
        isAuctionable = snapshotTile.isAuctionable;
        normalColor = PlaceholderVisualTheme.TileColor(tileType);
        SetOwnership(snapshotTile.ownerPlayerId, ownerColor);
        ApplyBaseColor();
        RefreshPlaceholderVisuals();
        LogDebug($"Bound tile snapshot tileId={tileId}, type={tileType}, owner={DisplayOwner()}, price={price}.");
    }

    public void SetBoardIndex(int newBoardIndex)
    {
        boardIndex = newBoardIndex;
        RefreshPlaceholderVisuals();
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

        if (tileTypeBandRenderer != null)
        {
            SetRendererColor(tileTypeBandRenderer, PlaceholderVisualTheme.TileColor(tileType));
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
        if (placeholderBorderRenderer != null)
        {
            SetRendererColor(placeholderBorderRenderer, highlightKind == HighlightKind.None
                ? new Color(0.06f, 0.07f, 0.08f, 1f)
                : ColorForHighlight(highlightKind));
        }
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
                return PlaceholderVisualTheme.HighlightColor(kind).a > 0f ? PlaceholderVisualTheme.HighlightColor(kind) : currentPlayerHighlightColor;
            case HighlightKind.ActiveAuction:
                return PlaceholderVisualTheme.HighlightColor(kind).a > 0f ? PlaceholderVisualTheme.HighlightColor(kind) : activeAuctionHighlightColor;
            case HighlightKind.Selected:
                return PlaceholderVisualTheme.HighlightColor(kind).a > 0f ? PlaceholderVisualTheme.HighlightColor(kind) : highlightedColor;
            default:
                return highlightedColor;
        }
    }

    private void EnsurePlaceholderVisuals()
    {
        if (!enablePlaceholderVisualPolish || !Application.isPlaying || placeholderVisualRoot != null)
        {
            return;
        }

        placeholderVisualRoot = new GameObject("PlaceholderTileVisuals_Runtime").transform;
        placeholderVisualRoot.gameObject.hideFlags = HideFlags.DontSave;
        placeholderVisualRoot.SetParent(transform, false);
        placeholderVisualRoot.localPosition = Vector3.zero;
        placeholderVisualRoot.localRotation = Quaternion.identity;
        placeholderVisualRoot.localScale = Vector3.one;

        GameObject border = PlaceholderVisualTheme.CreateRuntimeCubeChild(
            placeholderVisualRoot,
            "TileBorder_Runtime",
            new Vector3(0f, 0.72f, 0f),
            new Vector3(1.12f, 0.04f, 1.12f),
            new Color(0.06f, 0.07f, 0.08f, 1f));
        placeholderBorderRenderer = border.GetComponent<Renderer>();

        GameObject typeBand = PlaceholderVisualTheme.CreateRuntimeCubeChild(
            placeholderVisualRoot,
            "TileTypeBand_Runtime",
            new Vector3(0f, 0.78f, 0.38f),
            new Vector3(0.92f, 0.05f, 0.16f),
            PlaceholderVisualTheme.TileColor(tileType));
        tileTypeBandRenderer = typeBand.GetComponent<Renderer>();

        titleLabel = PlaceholderVisualTheme.CreateRuntimeTextChild(
            placeholderVisualRoot,
            "TileTitleLabel_Runtime",
            new Vector3(0f, 0.86f, -0.12f),
            0.075f,
            42,
            Color.white);
        detailLabel = PlaceholderVisualTheme.CreateRuntimeTextChild(
            placeholderVisualRoot,
            "TileDetailLabel_Runtime",
            new Vector3(0f, 0.87f, 0.24f),
            0.055f,
            32,
            PlaceholderVisualTheme.SecondaryText);
    }

    private void RefreshPlaceholderVisuals()
    {
        if (!enablePlaceholderVisualPolish)
        {
            return;
        }

        EnsurePlaceholderVisuals();
        if (tileTypeBandRenderer != null)
        {
            PlaceholderVisualTheme.ApplyRendererColor(tileTypeBandRenderer, PlaceholderVisualTheme.TileColor(tileType));
        }

        if (titleLabel != null)
        {
            titleLabel.text = PlaceholderVisualTheme.CompactLabel(displayName, tileId, 16);
            titleLabel.color = Color.white;
        }

        if (detailLabel != null)
        {
            string typeLabel = PlaceholderVisualTheme.CompactLabel(tileType, "tile", 12);
            string priceLabel = price > 0 ? $"${price}" : (isPurchasable ? "$?" : "safe");
            string auctionLabel = isAuctionable ? "auction" : "";
            detailLabel.text = string.IsNullOrWhiteSpace(auctionLabel)
                ? $"{boardIndex:00}  {typeLabel}  {priceLabel}"
                : $"{boardIndex:00}  {typeLabel}  {priceLabel}  {auctionLabel}";
        }

        ApplyHighlight();
    }

    private void LogDebug(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[BoardTileController] {message}", this);
        }
    }
}
