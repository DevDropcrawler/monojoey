using UnityEngine;

[RequireComponent(typeof(Renderer))]
public sealed class PlayerTokenController : MonoBehaviour
{
    [SerializeField] private string playerId = "player-1";
    [SerializeField] private Color tokenColor = new Color(0.20f, 0.55f, 0.85f, 1f);
    [SerializeField] private string currentTileId = "";
    [SerializeField] private int currentTileIndex;
    [SerializeField] private bool enablePlaceholderVisualPolish = true;
    [SerializeField] private bool enableDebugLogging;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Renderer tokenRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Transform placeholderVisualRoot;
    private Renderer pedestalRenderer;
    private Renderer ringRenderer;
    private TextMesh nameplateText;

    public string PlayerId => playerId;
    public Color TokenColor => tokenColor;
    public string CurrentTileId => currentTileId;
    public int CurrentTileIndex => currentTileIndex;
    public bool HasPlaceholderVisualPolish => placeholderVisualRoot != null && ringRenderer != null && nameplateText != null;
    public string NameplateText => nameplateText == null ? "" : nameplateText.text;

    private void Awake()
    {
        tokenRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
        EnsurePlaceholderVisuals();
        ApplyTokenColor();
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            tokenRenderer = GetComponent<Renderer>();
            propertyBlock ??= new MaterialPropertyBlock();
            ApplyTokenColor();
        }
    }

    public void SetPlayer(string newPlayerId, Color newTokenColor)
    {
        playerId = newPlayerId;
        tokenColor = newTokenColor;
        ApplyTokenColor();
        RefreshPlaceholderVisuals();
        LogDebug($"Player set to {playerId} with color {tokenColor}.");
    }

    public void SetCurrentTileIndex(int tileIndex)
    {
        currentTileIndex = tileIndex;
        LogDebug($"Tile index set to {currentTileIndex}.");
    }

    public void SetCurrentTile(string tileId, int tileIndex)
    {
        currentTileId = string.IsNullOrWhiteSpace(tileId) ? "" : tileId;
        currentTileIndex = tileIndex;
        LogDebug($"Current tile set to {DisplayTile()} index={currentTileIndex}.");
    }

    public void MoveToTilePosition(Vector3 worldPosition)
    {
        transform.position = worldPosition;
        LogDebug($"Moved to tile {DisplayTile()} index={currentTileIndex} at {worldPosition}.");
    }

    private void ApplyTokenColor()
    {
        if (tokenRenderer == null)
        {
            return;
        }

        propertyBlock ??= new MaterialPropertyBlock();
        tokenRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(BaseColorId, tokenColor);
        propertyBlock.SetColor(ColorId, tokenColor);
        tokenRenderer.SetPropertyBlock(propertyBlock);
        RefreshPlaceholderVisuals();
    }

    private void EnsurePlaceholderVisuals()
    {
        if (!enablePlaceholderVisualPolish || !Application.isPlaying || placeholderVisualRoot != null)
        {
            return;
        }

        placeholderVisualRoot = new GameObject("PlaceholderTokenVisuals_Runtime").transform;
        placeholderVisualRoot.gameObject.hideFlags = HideFlags.DontSave;
        placeholderVisualRoot.SetParent(transform, false);
        placeholderVisualRoot.localPosition = Vector3.zero;
        placeholderVisualRoot.localRotation = Quaternion.identity;
        placeholderVisualRoot.localScale = Vector3.one;

        GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pedestal.name = "TokenPedestal_Runtime";
        pedestal.hideFlags = HideFlags.DontSave;
        pedestal.transform.SetParent(placeholderVisualRoot, false);
        pedestal.transform.localPosition = new Vector3(0f, -0.46f, 0f);
        pedestal.transform.localRotation = Quaternion.identity;
        pedestal.transform.localScale = new Vector3(1.05f, 0.08f, 1.05f);
        Collider pedestalCollider = pedestal.GetComponent<Collider>();
        if (pedestalCollider != null)
        {
            Destroy(pedestalCollider);
        }

        pedestalRenderer = pedestal.GetComponent<Renderer>();

        GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        ring.name = "TokenColorRing_Runtime";
        ring.hideFlags = HideFlags.DontSave;
        ring.transform.SetParent(placeholderVisualRoot, false);
        ring.transform.localPosition = new Vector3(0f, -0.34f, 0f);
        ring.transform.localRotation = Quaternion.identity;
        ring.transform.localScale = new Vector3(1.18f, 0.035f, 1.18f);
        Collider ringCollider = ring.GetComponent<Collider>();
        if (ringCollider != null)
        {
            Destroy(ringCollider);
        }

        ringRenderer = ring.GetComponent<Renderer>();
        nameplateText = PlaceholderVisualTheme.CreateRuntimeTextChild(
            placeholderVisualRoot,
            "TokenNameplate_Runtime",
            new Vector3(0f, 0.84f, 0f),
            0.085f,
            38,
            Color.white);
        nameplateText.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
        RefreshPlaceholderVisuals();
    }

    private void RefreshPlaceholderVisuals()
    {
        if (!enablePlaceholderVisualPolish)
        {
            return;
        }

        EnsurePlaceholderVisuals();
        if (pedestalRenderer != null)
        {
            PlaceholderVisualTheme.ApplyRendererColor(pedestalRenderer, new Color(0.05f, 0.06f, 0.07f, 1f));
        }

        if (ringRenderer != null)
        {
            PlaceholderVisualTheme.ApplyRendererColor(ringRenderer, tokenColor);
        }

        if (nameplateText != null)
        {
            nameplateText.text = PlaceholderVisualTheme.CompactLabel(playerId, "player", 10);
            nameplateText.color = Color.white;
        }
    }

    private void LogDebug(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[PlayerTokenController] {message}", this);
        }
    }

    private string DisplayTile()
    {
        return string.IsNullOrWhiteSpace(currentTileId) ? "--" : currentTileId;
    }
}
