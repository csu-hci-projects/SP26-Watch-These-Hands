using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

/// <summary>
/// Runtime-created XRI translation gizmo. Three colored arrow handles (X/Y/Z) let the user
/// drag along each local axis to translate the target. The axis line is fixed at the grab-start
/// position so the displacement is always measured from the original origin.
/// </summary>
public class VRTranslationGizmo : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    [SerializeField] float handleLength = 0.45f;
    [SerializeField] float shaftThickness = 0.012f;
    [SerializeField] float capSize = 0.055f;
    [SerializeField] float selectedScale = 1.6f;
    [SerializeField] float boundsPadding = 1.35f;
    [SerializeField] float minimumVisibleRadius = 0.75f;
    [SerializeField] bool debugLogging = true;
    [SerializeField] float debugLogInterval = 0.25f;
    [SerializeField] bool verboseLogging = false;
    [SerializeField] float jitterThreshold = 0.08f;

    Transform target;
    VRGizmoTranslationHandle activeHandle;
    IXRSelectInteractor activeInteractor;
    float startProjection;
    Vector3 startTargetPos;
    Vector3 dragAxisWorld;
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

        // Don't resize the gizmo during drag; moving selected handles can cause
        // Near-Far handoff churn and small jumps at the start of a drag.
        if (!isDragging)
            UpdateGizmoScale();

        if (isDragging)
            transform.position = target.position;
        else
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

        // Project against the fixed axis line through startTargetPos so displacement is absolute.
        if (!TryProjectRayToAxisLine(ray, dragAxisWorld, startTargetPos, out float currentProjection))
            return;

        // BeginDrag runs inside XRInteractionManager:Update(); by LateUpdate the interactor
        // may have shifted slightly. Re-anchor on frame 0 so that offset doesn't show as a jump.
        if (dragFrame == 0)
        {
            startProjection = currentProjection;
            prevProjection = currentProjection;
            dragFrame++;
            return;
        }

        float delta = currentProjection - startProjection;
        target.position = startTargetPos + dragAxisWorld * delta;

        float projectionDelta = currentProjection - prevProjection;
        bool jitter = dragFrame > 0 && Mathf.Abs(projectionDelta) > jitterThreshold;

        if (jitter)
            Debug.LogWarning($"[VRTranslationGizmo] JITTER frame={dragFrame} axis={activeHandle.Axis} projDelta={projectionDelta:F4} proj={currentProjection:F4} prevProj={prevProjection:F4} delta={delta:F4} pos={target.position:F3} ray=({ray.origin:F3},{ray.direction:F3}) axisWorld={dragAxisWorld:F3}");
        else if (verboseLogging)
            Debug.Log($"[VRTranslationGizmo] frame={dragFrame} axis={activeHandle.Axis} proj={currentProjection:F4} start={startProjection:F4} delta={delta:F4} pos={target.position:F3} ray=({ray.origin:F3},{ray.direction:F3})");

        prevProjection = currentProjection;
        dragFrame++;
    }

    void SyncToTarget()
    {
        transform.SetPositionAndRotation(target.position, target.rotation);
    }

    void UpdateGizmoScale()
    {
        float desiredRadius = Mathf.Max(minimumVisibleRadius, EstimateTargetRadius() * boundsPadding);
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

    public void BeginDrag(VRGizmoTranslationHandle handle, IXRSelectInteractor interactor)
    {
        if (target == null || handle == null || interactor == null)
        {
            DebugLog($"BeginDrag rejected: target={target}, handle={handle}, interactor={interactor}");
            return;
        }

        dragAxisWorld = GetAxisWorld(handle.Axis);
        startTargetPos = target.position;

        if (!TryGetInteractorRay(interactor, out var ray, out _) ||
            !TryProjectRayToAxisLine(ray, dragAxisWorld, startTargetPos, out startProjection))
        {
            startProjection = Vector3.Dot(handle.transform.position - startTargetPos, dragAxisWorld);
        }

        activeHandle = handle;
        activeInteractor = interactor;
        isDragging = true;
        prevProjection = startProjection;
        dragFrame = 0;
        handle.SetSelected(true);

        DebugLog($"BeginDrag axis={handle.Axis} startProjection={startProjection:F3} startPos={startTargetPos} interactor={interactor.GetType().Name}");
    }

    public void EndDrag(VRGizmoTranslationHandle handle, IXRSelectInteractor interactor)
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

    // Called by the handle when a selectEntered fires while a delayed EndDrag is pending.
    // Keeps all reference points intact — only swaps the interactor.
    public void ContinueDrag(VRGizmoTranslationHandle handle, IXRSelectInteractor interactor)
    {
        if (!isDragging || handle == null || interactor == null)
            return;

        activeInteractor = interactor;
        activeHandle = handle;
        DebugLog($"ContinueDrag (Near-Far handoff) axis={handle.Axis} interactor={interactor.GetType().Name}");
    }

    bool TryProjectRayToAxisLine(Ray ray, Vector3 axisWorld, Vector3 refPoint, out float signedDistance)
    {
        var r = ray.origin - refPoint;
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
            if (origin == null) { ray = default; return false; }
            ray = new Ray(origin.position, origin.forward);
            return true;
        }

        if (interactor is ICurveInteractionDataProvider curveProvider)
        {
            var origin = curveProvider.curveOrigin;
            if (origin == null) { ray = default; return false; }

            ray = new Ray(origin.position, origin.forward);
            return true;
        }

        var attach = interactor.transform;
        if (attach == null) { ray = default; return false; }
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
        var go = new GameObject($"Translate {axis}");
        go.transform.SetParent(transform, false);

        go.transform.localRotation = axis switch
        {
            Axis.X => Quaternion.Euler(0f, 90f, 0f),
            Axis.Y => Quaternion.Euler(-90f, 0f, 0f),
            _ => Quaternion.identity,
        };

        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = CreateArrowMesh(handleLength, shaftThickness, capSize);

        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CreateMaterial(color);

        var collider = go.AddComponent<BoxCollider>();
        collider.center = new Vector3(0f, 0f, handleLength);
        collider.size = Vector3.one * capSize * 1.5f;

        var interactable = go.AddComponent<VRGizmoTranslationInteractable>();
        interactable.colliders.Clear();
        interactable.colliders.Add(collider);
        interactable.selectMode = InteractableSelectMode.Single;
        interactable.distanceCalculationMode = XRBaseInteractable.DistanceCalculationMode.ColliderPosition;

        var handle = go.AddComponent<VRGizmoTranslationHandle>();
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

    static Mesh CreateArrowMesh(float length, float shaftThickness, float capSize)
    {
        float hs = shaftThickness * 0.5f;
        float hc = capSize * 0.5f;
        float capStart = length - capSize;

        var vertices = new List<Vector3>();
        var triangles = new List<int>();

        AppendBox(vertices, triangles,
            new Vector3(-hs, -hs, 0.02f),
            new Vector3( hs,  hs, capStart));

        AppendPyramid(vertices, triangles, capStart, hc, length + hc);

        var mesh = new Mesh { name = "VR Translate Arrow" };
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AppendPyramid(List<Vector3> verts, List<int> tris, float baseZ, float baseHalf, float tipZ)
    {
        int b = verts.Count;
        float h = baseHalf;
        verts.Add(new Vector3(-h, -h, baseZ)); // 0
        verts.Add(new Vector3( h, -h, baseZ)); // 1
        verts.Add(new Vector3( h,  h, baseZ)); // 2
        verts.Add(new Vector3(-h,  h, baseZ)); // 3
        verts.Add(new Vector3( 0,  0, tipZ));  // 4 apex

        // base cap (back-facing)
        tris.Add(b+0); tris.Add(b+2); tris.Add(b+1);
        tris.Add(b+0); tris.Add(b+3); tris.Add(b+2);
        // four sides
        tris.Add(b+0); tris.Add(b+1); tris.Add(b+4);
        tris.Add(b+1); tris.Add(b+2); tris.Add(b+4);
        tris.Add(b+2); tris.Add(b+3); tris.Add(b+4);
        tris.Add(b+3); tris.Add(b+0); tris.Add(b+4);
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
        Debug.Log($"[VRTranslationGizmo] {message}", this);
    }

    void DebugLogThrottled(string message)
    {
        if (!debugLogging || Time.time < nextDebugLogTime) return;
        nextDebugLogTime = Time.time + Mathf.Max(0.01f, debugLogInterval);
        Debug.Log($"[VRTranslationGizmo] {message}", this);
    }
}

public class VRGizmoTranslationHandle : MonoBehaviour
{
    VRTranslationGizmo owner;
    MeshRenderer meshRenderer;
    Color baseColor;
    float selectedScale = 1.6f;
    Vector3 baseScale;

    public VRTranslationGizmo.Axis Axis { get; private set; }
    public VRGizmoTranslationInteractable Interactable { get; private set; }
    public Collider Collider { get; private set; }

    public void Initialize(
        VRTranslationGizmo owner,
        VRTranslationGizmo.Axis axis,
        VRGizmoTranslationInteractable interactable,
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

        // If the trigger is still physically held, the XRI Near-Far interactor dropped us due to a
        // near/far mode transition or attach-distance threshold — not a real release.
        // Keep the drag alive; LateUpdate will end it when isSelectActive goes false.
        if (args.interactorObject != null && args.interactorObject.isSelectActive)
        {
            owner?.DebugLog($"Spurious deselect — trigger still held, continuing drag axis={Axis}");
            owner?.ContinueDrag(this, args.interactorObject);
            return;
        }

        owner?.EndDrag(this, args.interactorObject);
    }
}

public class VRGizmoTranslationInteractable : XRSimpleInteractable, IFarAttachProvider
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

    void EnsureAttachTransform()
    {
        if (attachTransform != null) return;
        var go = new GameObject("Translate Attach");
        go.transform.SetParent(transform, false);
        attachTransform = go.transform;
    }
}
