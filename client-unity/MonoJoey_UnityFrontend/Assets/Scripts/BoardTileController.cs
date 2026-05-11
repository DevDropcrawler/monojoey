using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class BoardTileController : MonoBehaviour
{
    [SerializeField] private string tileId = "tile";
    [SerializeField] private Renderer baseRenderer;
    [SerializeField] private Renderer highlightRenderer;
    [SerializeField] private Renderer ownershipRenderer;
    [SerializeField] private int boardIndex = -1;
    [SerializeField] private Color normalColor = new Color(0.28f, 0.32f, 0.36f, 1f);
    [SerializeField] private Color highlightedColor = new Color(0.95f, 0.78f, 0.24f, 0.65f);
    [SerializeField] private Color unownedColor = new Color(0.22f, 0.24f, 0.27f, 1f);
    [SerializeField] private bool enableDebugLogging;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private MaterialPropertyBlock propertyBlock;
    private string ownerPlayerId = "";
    private bool isHighlighted;

    public string TileId => tileId;
    public string OwnerPlayerId => ownerPlayerId;
    public bool IsHighlighted => isHighlighted;
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
        isHighlighted = highlighted;
        ApplyHighlight();
        LogDebug($"Highlight set to {isHighlighted}.");
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
        Bounds bounds = baseRenderer != null ? baseRenderer.bounds : new Bounds(transform.position, transform.lossyScale);
        return new Vector3(bounds.center.x, bounds.max.y + yOffset, bounds.center.z);
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

        highlightRenderer.enabled = isHighlighted;
        SetRendererColor(highlightRenderer, highlightedColor);
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

    private void LogDebug(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[BoardTileController] {message}", this);
        }
    }
}
