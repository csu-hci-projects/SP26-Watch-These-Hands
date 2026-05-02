using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

/// <summary>
/// Runtime-created XRI scale gizmo. Three colored arrow handles (X/Y/Z) let the user
/// drag along each local axis to scale the target. Drag math projects the interactor ray
/// onto the axis line; scale factor = |currentProjection| / |startProjection|.
/// </summary>
public class VRScaleGizmo : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [SerializeField] float handleLength = 0.45f;
    [SerializeField] float shaftThickness = 0.012f;
    [SerializeField] float capSize = 0.055f;
    [SerializeField] float selectedScale = 1.6f;
    [SerializeField] float boundsPadding = 1.35f;
    [SerializeField] float minimumVisibleRadius = 0.25f;
    [SerializeField] float maximumVisibleRadius = 100f;
    [SerializeField] float minimumScale = 0.05f;
    [SerializeField] float maximumScale = 20f;
    [SerializeField] bool debugLogging = true;
    [SerializeField] float debugLogInterval = 0.25f;
    [SerializeField] bool verboseLogging = false;
    [SerializeField] float jitterThreshold = 0.08f;

    Transform target;
    VRGizmoScaleHandle activeHandle;
    IXRSelectInteractor activeInteractor;
    float startDragProjection;
    Vector3 startScale;
    Vector3 dragAxisWorld;
    Axis dragAxisLocal;
    bool isDragging;
    float nextDebugLogTime;
    float prevProjection;
    int dragFrame;

    public bool IsDragging => isDragging;

    public void SetTarget(Transform newTarget)
    {
        if (target != newTarget)
            DebugLog($"SetTarget {(newTarget != null ? newTarget.name : "null")}");

        target = newTarget;
        gameObject.SetActive(target != null);

        if (target != null)
        {
            UpdateGizmoScale();
            SyncToTarget();
        }
        else
        {
            EndDrag(activeHandle, activeInteractor);
        }
    }

    void Awake()
    {
        CreateHandle(Axis.X, new Color(0.95f, 0.18f, 0.18f, 0.85f));
        CreateHandle(Axis.Y, new Color(0.22f, 0.85f, 0.24f, 0.85f));
        CreateHandle(Axis.Z, new Color(0.22f, 0.42f, 1f, 0.85f));
        gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        UpdateGizmoScale();
        SyncToTarget();

        if (!isDragging || activeHandle == null || activeInteractor == null)
            return;

        // If the trigger was released without XRI firing selectExited (can happen after ContinueDrag),
        // end the drag ourselves.
        if (!activeInteractor.isSelectActive)
        {
            DebugLog($"LateUpdate: interactor no longer active, ending drag axis={activeHandle.Axis}");
            EndDrag(activeHandle, activeInteractor);
            return;
        }

        if (!TryGetInteractorRay(activeInteractor, out var ray, out _))
            return;

        if (!TryProjectRayToAxisLine(ray, dragAxisWorld, out float currentProjection))
            return;

        // BeginDrag runs inside XRInteractionManager:Update(); by LateUpdate the interactor
        // may have shifted slightly. Re-anchor on frame 0 so that offset doesn't show as a jump.
        if (dragFrame == 0)
        {
            if (Mathf.Abs(currentProjection) >= 0.001f)
                startDragProjection = currentProjection;
            prevProjection = currentProjection;
            dragFrame++;
            return;
        }

        if (Mathf.Abs(startDragProjection) < 0.001f)
            return;

        float scaleFactor = Mathf.Abs(currentProjection) / Mathf.Abs(startDragProjection);
        scaleFactor = Mathf.Clamp(scaleFactor, 0.01f, 100f);

        var newScale = startScale;
        switch (dragAxisLocal)
        {
            case Axis.X:
                newScale.x = Mathf.Clamp(startScale.x * scaleFactor, minimumScale, maximumScale);
                break;
            case Axis.Y:
                newScale.y = Mathf.Clamp(startScale.y * scaleFactor, minimumScale, maximumScale);
                break;
            case Axis.Z:
                newScale.z = Mathf.Clamp(startScale.z * scaleFactor, minimumScale, maximumScale);
                break;
        }

        target.localScale = newScale;
        UpdateGizmoScale();
        SyncToTarget();

        var attachPoint = target.position + dragAxisWorld * currentProjection;
        activeHandle.Interactable.SetAttachPoint(attachPoint);

        float projectionDelta = currentProjection - prevProjection;
        bool jitter = dragFrame > 0 && Mathf.Abs(projectionDelta) > jitterThreshold;

        if (jitter)
            Debug.LogWarning($"[VRScaleGizmo] JITTER frame={dragFrame} axis={dragAxisLocal} projDelta={projectionDelta:F4} proj={currentProjection:F4} prevProj={prevProjection:F4} factor={scaleFactor:F4} ray=({ray.origin:F3},{ray.direction:F3}) axisWorld={dragAxisWorld:F3}");
        else if (verboseLogging)
            Debug.Log($"[VRScaleGizmo] frame={dragFrame} axis={dragAxisLocal} proj={currentProjection:F4} start={startDragProjection:F4} factor={scaleFactor:F4} scale={newScale:F3} ray=({ray.origin:F3},{ray.direction:F3})");

        prevProjection = currentProjection;
        dragFrame++;
    }

    void SyncToTarget()
    {
        transform.SetPositionAndRotation(target.position, target.rotation);
    }

    void UpdateGizmoScale()
    {
        float desiredRadius = Mathf.Clamp(EstimateTargetRadius() * boundsPadding, minimumVisibleRadius, maximumVisibleRadius);
        float scale = desiredRadius / Mathf.Max(0.0001f, handleLength);
        transform.localScale = Vector3.one * scale;
    }

    float EstimateTargetRadius()
    {
        if (target == null)
            return minimumVisibleRadius;

        bool hasBounds = false;
        var bounds = new Bounds(target.position, Vector3.zero);

        var renderers = target.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
        {
            if (r == null || !r.enabled) continue;
            if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
            else bounds.Encapsulate(r.bounds);
        }

        if (!hasBounds)
        {
            var colliders = target.GetComponentsInChildren<Collider>();
            foreach (var c in colliders)
            {
                if (c == null || !c.enabled) continue;
                if (!hasBounds) { bounds = c.bounds; hasBounds = true; }
                else bounds.Encapsulate(c.bounds);
            }
        }

        return hasBounds ? bounds.extents.magnitude : minimumVisibleRadius;
    }

    public void BeginDrag(VRGizmoScaleHandle handle, IXRSelectInteractor interactor)
    {
        if (target == null || handle == null || interactor == null)
        {
            DebugLog($"BeginDrag rejected: target={target}, handle={handle}, interactor={interactor}");
            return;
        }

        dragAxisLocal = handle.Axis;
        dragAxisWorld = GetAxisWorld(handle.Axis);

        if (!TryGetInteractorRay(interactor, out var ray, out _) ||
            !TryProjectRayToAxisLine(ray, dragAxisWorld, out startDragProjection))
        {
            startDragProjection = Vector3.Dot(handle.transform.position - target.position, dragAxisWorld);
        }

        if (Mathf.Abs(startDragProjection) < 0.001f)
            startDragProjection = handleLength * transform.lossyScale.x;

        activeHandle = handle;
        activeInteractor = interactor;
        startScale = target.localScale;
        isDragging = true;
        prevProjection = startDragProjection;
        dragFrame = 0;
        handle.SetSelected(true);

        DebugLog($"BeginDrag axis={handle.Axis} startProjection={startDragProjection:F3} startScale={startScale} interactor={interactor.GetType().Name}");
    }

    public void EndDrag(VRGizmoScaleHandle handle, IXRSelectInteractor interactor)
    {
        if (!isDragging)
            return;

        if (handle != activeHandle && !ReferenceEquals(interactor, activeInteractor))
            return;

        if (activeHandle != null)
            activeHandle.SetSelected(false);

        activeHandle = null;
        activeInteractor = null;
        dragAxisWorld = Vector3.zero;
        isDragging = false;

        DebugLog($"EndDrag axis={(handle != null ? handle.Axis.ToString() : "null")}");

        if (target != null)
            SyncToTarget();
    }

    public void ContinueDrag(VRGizmoScaleHandle handle, IXRSelectInteractor interactor)
    {
        if (!isDragging || handle == null || interactor == null)
            return;

        activeInteractor = interactor;
        activeHandle = handle;
        DebugLog($"ContinueDrag (Near-Far handoff) axis={handle.Axis} interactor={interactor.GetType().Name}");
    }

    bool TryProjectRayToAxisLine(Ray ray, Vector3 axisWorld, out float signedDistance)
    {
        var r = ray.origin - target.position;
        var d = ray.direction;
        var a = axisWorld;

        float ada = Vector3.Dot(a, d);
        float denom = 1f - ada * ada;

        if (Mathf.Abs(denom) < 0.0001f)
        {
            signedDistance = Vector3.Dot(r, a);
            return true;
        }

        float ra = Vector3.Dot(r, a);
        float rd = Vector3.Dot(r, d);
        signedDistance = (ra - ada * rd) / denom;
        return true;
    }

    bool TryGetInteractorRay(IXRSelectInteractor interactor, out Ray ray, out float maxDistance)
    {
        maxDistance = 30f;

        if (interactor is XRRayInteractor rayInteractor)
        {
            maxDistance = rayInteractor.maxRaycastDistance;
            var origin = rayInteractor.rayOriginTransform != null ? rayInteractor.rayOriginTransform : rayInteractor.transform;
            if (origin == null)
            {
                ray = default;
                return false;
            }

            ray = new Ray(origin.position, origin.forward);
            return true;
        }

        if (interactor is ICurveInteractionDataProvider curveProvider)
        {
            var origin = curveProvider.curveOrigin;
            if (origin == null)
            {
                ray = default;
                return false;
            }

            ray = new Ray(origin.position, origin.forward);
            return true;
        }

        var attach = interactor.transform;
        if (attach == null)
        {
            ray = default;
            return false;
        }

        ray = new Ray(attach.position, attach.forward);
        return true;
    }

    Vector3 GetAxisWorld(Axis axis)
    {
        return axis switch
        {
            Axis.X => transform.right,
            Axis.Y => transform.up,
            _ => transform.forward,
        };
    }

    void CreateHandle(Axis axis, Color color)
    {
        var go = new GameObject($"Scale {axis}");
        go.transform.SetParent(transform, false);

        go.transform.localRotation = axis switch
        {
            Axis.X => Quaternion.Euler(0f, 90f, 0f),
            Axis.Y => Quaternion.Euler(-90f, 0f, 0f),
            _ => Quaternion.identity,
        };

        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = CreateBallMesh(handleLength, shaftThickness, capSize);

        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CreateMaterial(color);

        var collider = go.AddComponent<SphereCollider>();
        collider.center = new Vector3(0f, 0f, handleLength);
        collider.radius = capSize * 0.9f;

        var interactable = go.AddComponent<VRGizmoScaleInteractable>();
        interactable.colliders.Clear();
        interactable.colliders.Add(collider);
        interactable.selectMode = InteractableSelectMode.Single;
        interactable.distanceCalculationMode = XRBaseInteractable.DistanceCalculationMode.ColliderPosition;

        var handle = go.AddComponent<VRGizmoScaleHandle>();
        handle.Initialize(this, axis, interactable, collider, renderer, color, selectedScale);
    }

    Material CreateMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        var mat = new Material(shader);
        mat.color = color;
        return mat;
    }

    static Mesh CreateBallMesh(float length, float shaftThickness, float ballRadius)
    {
        float hs = shaftThickness * 0.5f;
        float shaftEnd = length - ballRadius * 0.4f;

        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        AppendBox(vertices, triangles,
            new Vector3(-hs, -hs, 0.02f),
            new Vector3( hs,  hs, shaftEnd));

        AppendSphere(vertices, triangles, new Vector3(0f, 0f, length), ballRadius, 10, 7);

        var mesh = new Mesh { name = "VR Scale Ball" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AppendSphere(List<Vector3> verts, List<int> tris, Vector3 center, float radius, int lonSegs, int latSegs)
    {
        int b = verts.Count;

        for (int lat = 0; lat <= latSegs; lat++)
        {
            float theta = lat * Mathf.PI / latSegs;
            float sinTheta = Mathf.Sin(theta);
            float cosTheta = Mathf.Cos(theta);
            for (int lon = 0; lon <= lonSegs; lon++)
            {
                float phi = lon * 2f * Mathf.PI / lonSegs;
                verts.Add(center + new Vector3(
                    radius * sinTheta * Mathf.Cos(phi),
                    radius * sinTheta * Mathf.Sin(phi),
                    radius * cosTheta));
            }
        }

        for (int lat = 0; lat < latSegs; lat++)
        {
            for (int lon = 0; lon < lonSegs; lon++)
            {
                int a = b + lat * (lonSegs + 1) + lon;
                int c = a + lonSegs + 1;
                tris.Add(a);     tris.Add(c);     tris.Add(a + 1);
                tris.Add(c);     tris.Add(c + 1); tris.Add(a + 1);
            }
        }
    }

    static void AppendBox(List<Vector3> verts, List<int> tris, Vector3 min, Vector3 max)
    {
        int b = verts.Count;
        verts.Add(new Vector3(min.x, min.y, min.z));
        verts.Add(new Vector3(max.x, min.y, min.z));
        verts.Add(new Vector3(max.x, max.y, min.z));
        verts.Add(new Vector3(min.x, max.y, min.z));
        verts.Add(new Vector3(min.x, min.y, max.z));
        verts.Add(new Vector3(max.x, min.y, max.z));
        verts.Add(new Vector3(max.x, max.y, max.z));
        verts.Add(new Vector3(min.x, max.y, max.z));

        tris.Add(b+0); tris.Add(b+2); tris.Add(b+1);
        tris.Add(b+0); tris.Add(b+3); tris.Add(b+2);
        tris.Add(b+4); tris.Add(b+5); tris.Add(b+6);
        tris.Add(b+4); tris.Add(b+6); tris.Add(b+7);
        tris.Add(b+0); tris.Add(b+4); tris.Add(b+7);
        tris.Add(b+0); tris.Add(b+7); tris.Add(b+3);
        tris.Add(b+1); tris.Add(b+2); tris.Add(b+6);
        tris.Add(b+1); tris.Add(b+6); tris.Add(b+5);
        tris.Add(b+0); tris.Add(b+1); tris.Add(b+5);
        tris.Add(b+0); tris.Add(b+5); tris.Add(b+4);
        tris.Add(b+3); tris.Add(b+6); tris.Add(b+2);
        tris.Add(b+3); tris.Add(b+7); tris.Add(b+6);
    }

    public void DebugLog(string message)
    {
        if (!debugLogging) return;
        Debug.Log($"[VRScaleGizmo] {message}", this);
    }

    void DebugLogThrottled(string message)
    {
        if (!debugLogging || Time.time < nextDebugLogTime) return;
        nextDebugLogTime = Time.time + Mathf.Max(0.01f, debugLogInterval);
        Debug.Log($"[VRScaleGizmo] {message}", this);
    }
}

public class VRGizmoScaleHandle : MonoBehaviour
{
    VRScaleGizmo owner;
    MeshRenderer meshRenderer;
    Color baseColor;
    float selectedScale = 1.6f;
    Vector3 baseScale;

    public VRScaleGizmo.Axis Axis { get; private set; }
    public VRGizmoScaleInteractable Interactable { get; private set; }
    public Collider Collider { get; private set; }

    public void Initialize(
        VRScaleGizmo owner,
        VRScaleGizmo.Axis axis,
        VRGizmoScaleInteractable interactable,
        Collider handleCollider,
        MeshRenderer renderer,
        Color color,
        float selectedScale)
    {
        this.owner = owner;
        Axis = axis;
        Interactable = interactable;
        Collider = handleCollider;
        meshRenderer = renderer;
        baseColor = color;
        this.selectedScale = selectedScale;
        baseScale = transform.localScale;

        interactable.selectEntered.AddListener(OnSelectEntered);
        interactable.selectExited.AddListener(OnSelectExited);
        interactable.hoverEntered.AddListener(_ => SetHover(true));
        interactable.hoverExited.AddListener(_ => SetHover(false));
    }

    void OnDestroy()
    {
        if (Interactable == null) return;
        Interactable.selectEntered.RemoveListener(OnSelectEntered);
        Interactable.selectExited.RemoveListener(OnSelectExited);
    }

    public void SetSelected(bool selected)
    {
        transform.localScale = selected ? baseScale * selectedScale : baseScale;
        SetColor(selected ? Color.yellow : baseColor);
    }

    void SetHover(bool hovering)
    {
        if (owner != null && owner.IsDragging) return;
        SetColor(hovering ? Color.Lerp(baseColor, Color.white, 0.35f) : baseColor);
    }

    void SetColor(Color color)
    {
        if (meshRenderer != null)
            meshRenderer.material.color = color;
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        owner?.DebugLog($"Handle select entered axis={Axis} interactor={args.interactorObject?.transform?.name}");
        owner?.BeginDrag(this, args.interactorObject);
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        owner?.DebugLog($"Handle select exited axis={Axis} interactor={args.interactorObject?.transform?.name} selectActive={args.interactorObject?.isSelectActive}");

        if (args.interactorObject != null && args.interactorObject.isSelectActive)
        {
            owner?.DebugLog($"Spurious deselect — trigger still held, continuing drag axis={Axis}");
            owner?.ContinueDrag(this, args.interactorObject);
            return;
        }

        owner?.EndDrag(this, args.interactorObject);
    }
}

public class VRGizmoScaleInteractable : XRSimpleInteractable, IFarAttachProvider
{
    Transform attachTransform;

    public InteractableFarAttachMode farAttachMode { get; set; } = InteractableFarAttachMode.Near;

    protected override void Awake()
    {
        base.Awake();
        EnsureAttachTransform();
    }

    public override Transform GetAttachTransform(IXRInteractor interactor)
    {
        EnsureAttachTransform();
        return attachTransform;
    }

    public void SetAttachPoint(Vector3 worldPoint)
    {
        EnsureAttachTransform();
        attachTransform.position = worldPoint;
    }

    void EnsureAttachTransform()
    {
        if (attachTransform != null) return;
        var go = new GameObject("Scale Attach");
        go.transform.SetParent(transform, false);
        attachTransform = go.transform;
    }
}
