using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class TokenAnimator : MonoBehaviour
{
    public enum MovementKind
    {
        Normal,
        Fast,
        Minimal
    }

    [SerializeField] private PlayerTokenController tokenController;
    [SerializeField] private float secondsPerTile = 0.35f;
    [SerializeField] private float hopHeight = 0.35f;
    [SerializeField] private AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private AnimationCurve hopCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);
    [SerializeField] private bool enableDebugLogging;

    private Coroutine activeCoroutine;
    private Vector3 finalPosition;
    private int finalTileIndex;
    private MovementKind currentMovementKind = MovementKind.Normal;

    public bool IsAnimating => activeCoroutine != null;
    public string CurrentMovementKind => currentMovementKind.ToString();

    private void Awake()
    {
        if (tokenController == null)
        {
            tokenController = GetComponent<PlayerTokenController>();
        }
    }

    public Coroutine AnimateAlongPath(
        IReadOnlyList<string> pathTileIds,
        IReadOnlyDictionary<string, BoardTileController> tilesById,
        int stepCount,
        MovementKind movementKind)
    {
        StopMovement(false);
        currentMovementKind = movementKind;
        activeCoroutine = StartCoroutine(AnimatePathRoutine(pathTileIds, tilesById, stepCount, movementKind));
        return activeCoroutine;
    }

    public void StopMovement(bool snapToFinalTile)
    {
        if (activeCoroutine != null)
        {
            StopCoroutine(activeCoroutine);
            activeCoroutine = null;
        }

        if (snapToFinalTile)
        {
            transform.position = finalPosition;
            if (tokenController != null)
            {
                tokenController.SetCurrentTileIndex(finalTileIndex);
            }
        }
    }

    private IEnumerator AnimatePathRoutine(
        IReadOnlyList<string> pathTileIds,
        IReadOnlyDictionary<string, BoardTileController> tilesById,
        int stepCount,
        MovementKind movementKind)
    {
        if (pathTileIds == null || tilesById == null || pathTileIds.Count == 0 || stepCount <= 0)
        {
            LogDebug("Animation skipped: no path or steps.");
            activeCoroutine = null;
            yield break;
        }

        int maxIndex = Mathf.Min(stepCount, pathTileIds.Count - 1);
        if (maxIndex <= 0)
        {
            SnapToTile(pathTileIds[0], tilesById, 0);
            activeCoroutine = null;
            yield break;
        }

        LogDebug($"Animation start: path={string.Join(" -> ", pathTileIds)}, stepCount={stepCount}, movementKind={movementKind}.");

        for (int i = 1; i <= maxIndex; i++)
        {
            string tileId = pathTileIds[i];
            if (!tilesById.TryGetValue(tileId, out BoardTileController tile) || tile == null)
            {
                LogDebug($"Animation skipped missing tileId={tileId}.");
                continue;
            }

            Vector3 start = transform.position;
            Vector3 end = tile.GetTokenAnchorPosition(0.35f);
            finalPosition = end;
            finalTileIndex = i;

            float duration = DurationForMovement(movementKind);
            if (duration <= 0.01f)
            {
                transform.position = end;
            }
            else
            {
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    float curveT = movementCurve == null ? t : movementCurve.Evaluate(t);
                    Vector3 position = Vector3.Lerp(start, end, curveT);
                    float hopT = hopCurve == null ? Mathf.Sin(t * Mathf.PI) : hopCurve.Evaluate(t);
                    position.y += Mathf.Max(0f, hopT) * hopHeight;
                    transform.position = position;
                    yield return null;
                }

                transform.position = end;
            }

            if (tokenController != null)
            {
                tokenController.SetCurrentTileIndex(i);
            }

            LogDebug($"Step complete: tileId={tileId}, tileIndex={i}, position={transform.position}.");
        }

        LogDebug($"Animation end: finalTileIndex={finalTileIndex}, movementKind={movementKind}.");
        activeCoroutine = null;
    }

    private void SnapToTile(string tileId, IReadOnlyDictionary<string, BoardTileController> tilesById, int tileIndex)
    {
        if (tilesById.TryGetValue(tileId, out BoardTileController tile) && tile != null)
        {
            finalPosition = tile.GetTokenAnchorPosition(0.35f);
            finalTileIndex = tileIndex;
            transform.position = finalPosition;

            if (tokenController != null)
            {
                tokenController.SetCurrentTileIndex(tileIndex);
            }
        }
    }

    private float DurationForMovement(MovementKind movementKind)
    {
        return movementKind switch
        {
            MovementKind.Fast => Mathf.Max(0.01f, secondsPerTile * 0.5f),
            MovementKind.Minimal => Mathf.Min(0.05f, secondsPerTile * 0.15f),
            _ => Mathf.Max(0.01f, secondsPerTile),
        };
    }

    private void LogDebug(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[TokenAnimator] {message}", this);
        }
    }
}
