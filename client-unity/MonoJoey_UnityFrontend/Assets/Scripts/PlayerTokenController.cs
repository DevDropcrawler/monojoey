using UnityEngine;

[RequireComponent(typeof(Renderer))]
public sealed class PlayerTokenController : MonoBehaviour
{
    [SerializeField] private string playerId = "player-1";
    [SerializeField] private Color tokenColor = new Color(0.20f, 0.55f, 0.85f, 1f);
    [SerializeField] private int currentTileIndex;
    [SerializeField] private bool enableDebugLogging;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private Renderer tokenRenderer;
    private MaterialPropertyBlock propertyBlock;

    public string PlayerId => playerId;
    public Color TokenColor => tokenColor;
    public int CurrentTileIndex => currentTileIndex;

    private void Awake()
    {
        tokenRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
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
        LogDebug($"Player set to {playerId} with color {tokenColor}.");
    }

    public void SetCurrentTileIndex(int tileIndex)
    {
        currentTileIndex = tileIndex;
        LogDebug($"Tile index set to {currentTileIndex}.");
    }

    public void MoveToTilePosition(Vector3 worldPosition)
    {
        transform.position = worldPosition;
        LogDebug($"Moved to tile {currentTileIndex} at {worldPosition}.");
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
    }

    private void LogDebug(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[PlayerTokenController] {message}", this);
        }
    }
}
