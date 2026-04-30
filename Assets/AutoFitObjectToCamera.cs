using System.Text.RegularExpressions;
using GaussianSplatting.Runtime;
using UnityEngine;

public class PlaceSplatInFrontOfCamera : MonoBehaviour
{
    public Transform splat;
    public GameObject splatObject;
    public GaussianSplatRenderer splatRenderer;
    public GameObject splatPrefab;
    public Camera viewerCamera;

    public bool autoFindSplat = true;
    public bool autoFindCamera = true;
    public bool instantiatePrefabIfNeeded = true;
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
        ResolveReferences();

        if (splat == null || viewerCamera == null)
        {
            Debug.LogWarning("PlaceSplatInFrontOfCamera: splat and/or viewerCamera could not be found.");
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

    void ResolveReferences()
    {
        if (autoFindCamera && viewerCamera == null)
        {
            viewerCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
        }

        if (splat != null)
        {
            splatObject = splat.gameObject;
            splatRenderer = splatRenderer != null ? splatRenderer : splat.GetComponent<GaussianSplatRenderer>();
            return;
        }

        if (splatRenderer == null)
        {
            splatRenderer = GetComponent<GaussianSplatRenderer>();
        }

        if (splatRenderer != null)
        {
            splat = splatRenderer.transform;
            splatObject = splat.gameObject;
            return;
        }

        if (splatObject != null)
        {
            splat = splatObject.transform;
            splatRenderer = splatObject.GetComponent<GaussianSplatRenderer>();
            if (splatRenderer == null)
            {
                splatRenderer = splatObject.GetComponentInChildren<GaussianSplatRenderer>();
            }
            return;
        }

        if (instantiatePrefabIfNeeded && splatPrefab != null)
        {
            splatObject = Instantiate(splatPrefab);
            splatObject.name = splatPrefab.name;
            splatRenderer = splatObject.GetComponent<GaussianSplatRenderer>();
            if (splatRenderer == null)
            {
                splatRenderer = splatObject.GetComponentInChildren<GaussianSplatRenderer>();
            }
            splat = splatRenderer != null ? splatRenderer.transform : splatObject.transform;
            return;
        }

        if (autoFindSplat)
        {
            splatRenderer = FindLatestGaussianSplatRenderer();
            if (splatRenderer != null)
            {
                splat = splatRenderer.transform;
                splatObject = splat.gameObject;
            }
        }
    }

    static GaussianSplatRenderer FindLatestGaussianSplatRenderer()
    {
        GaussianSplatRenderer[] renderers = FindObjectsByType<GaussianSplatRenderer>(FindObjectsSortMode.None);
        GaussianSplatRenderer best = null;
        int bestIndex = -1;

        foreach (GaussianSplatRenderer renderer in renderers)
        {
            int index = ExtractInputIndex(renderer);
            if (best == null || index > bestIndex)
            {
                best = renderer;
                bestIndex = index;
            }
        }

        return best;
    }

    static int ExtractInputIndex(GaussianSplatRenderer renderer)
    {
        if (renderer == null)
        {
            return -1;
        }

        string assetName = renderer.asset != null ? renderer.asset.name : string.Empty;
        string searchText = $"{assetName} {renderer.gameObject.name}";
        Match match = Regex.Match(searchText, @"input[_-](\d+)");
        return match.Success ? int.Parse(match.Groups[1].Value) : -1;
    }
}
