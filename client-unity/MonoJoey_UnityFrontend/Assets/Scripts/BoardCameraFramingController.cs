using UnityEngine;

public sealed class BoardCameraFramingController : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private float framingMargin = 1.35f;
    [SerializeField] private float cameraHeight = 13f;
    [SerializeField] private float cameraDepthOffset = -10f;
    [SerializeField] private Vector3 cameraEulerAngles = new Vector3(55f, 0f, 0f);
    [SerializeField] private bool enableDebugLogging;

    public Camera TargetCamera => targetCamera;
    public float LastOrthographicSize { get; private set; }
    public Bounds LastFramedBounds { get; private set; }

    public void Configure(Camera cameraToFrame)
    {
        if (cameraToFrame != null)
        {
            targetCamera = cameraToFrame;
        }
    }

    public Camera EnsureCamera()
    {
        if (targetCamera != null)
        {
            return targetCamera;
        }

        targetCamera = Camera.main;
        if (targetCamera != null)
        {
            return targetCamera;
        }

        GameObject cameraObject = new GameObject("BoardCamera_Runtime", typeof(Camera))
        {
            hideFlags = HideFlags.DontSave
        };
        targetCamera = cameraObject.GetComponent<Camera>();
        targetCamera.tag = "MainCamera";
        return targetCamera;
    }

    public void FrameBounds(Bounds bounds)
    {
        Camera cameraToFrame = EnsureCamera();
        if (cameraToFrame == null)
        {
            return;
        }

        LastFramedBounds = bounds;
        cameraToFrame.orthographic = true;
        cameraToFrame.transform.position = new Vector3(bounds.center.x, bounds.center.y + cameraHeight, bounds.center.z + cameraDepthOffset);
        cameraToFrame.transform.rotation = Quaternion.Euler(cameraEulerAngles);

        float verticalExtent = Mathf.Max(bounds.extents.z, bounds.extents.x * 0.72f);
        LastOrthographicSize = Mathf.Max(4f, verticalExtent * framingMargin);
        cameraToFrame.orthographicSize = LastOrthographicSize;
        LogDebug($"Camera framed bounds={bounds}, size={LastOrthographicSize}, position={cameraToFrame.transform.position}.");
    }

    private void LogDebug(string message)
    {
        if (enableDebugLogging)
        {
            Debug.Log($"[BoardCameraFramingController] {message}", this);
        }
    }
}
