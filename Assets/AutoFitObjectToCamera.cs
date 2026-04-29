using UnityEngine;

public class PlaceSplatInFrontOfCamera : MonoBehaviour
{
    public Transform splat;
    public Camera viewerCamera;

    public float distanceFromCamera = 3.0f;
    public float heightOffset = 0.0f;
    public Vector3 splatScale = new Vector3(0.1f, 0.1f, 0.1f);

    void Start()
    {
        Place();
    }

    [ContextMenu("Place Splat")]
    public void Place()
    {
        if (splat == null || viewerCamera == null)
        {
            Debug.LogWarning("PlaceSplatInFrontOfCamera: splat or viewerCamera is not assigned.");
            return;
        }

        Vector3 forward = viewerCamera.transform.forward;
        forward.y = 0f;

        if (forward.magnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        forward.Normalize();

        splat.position = viewerCamera.transform.position + forward * distanceFromCamera;
        splat.position += Vector3.up * heightOffset;
        splat.localScale = splatScale;
    }
}