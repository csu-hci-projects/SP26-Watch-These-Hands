using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;

/// <summary>
/// Runtime-created XRI rotation gizmo. XRI owns hover/select; rotation is computed
/// from the selecting controller projected onto the selected axis plane.
/// </summary>
public class VRRotationGizmo : MonoBehaviour
{
    public enum Axis
    {
        X,
        Y,
        Z
    }

    [SerializeField] float radius = 0.45f;
    [SerializeField] float tubeRadius = 0.015f;
    [SerializeField] float selectedScale = 1.6f;
    [SerializeField] float boundsPadding = 1.35f;
    [SerializeField] float minimumVisibleRadius = 0.75f;
    [SerializeField] float rotationSensitivity = 0.45f;
    [SerializeField] bool debugLogging = true;
    [SerializeField] float debugLogInterval = 0.25f;
    [SerializeField] int ringSegments = 96;
    [SerializeField] int tubeSegments = 10;

    Transform target;
    VRGizmoRingHandle activeHandle;
    IXRSelectInteractor activeInteractor;
    Vector3 startVector;
    Vector3 lastDragVector;
    Vector3 dragAxisWorld;
    Quaternion startRotation;
    bool isDragging;
    float nextDebugLogTime;

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
        CreateRing(Axis.X, new Color(0.95f, 0.18f, 0.18f, 0.85f));
        CreateRing(Axis.Y, new Color(0.22f, 0.85f, 0.24f, 0.85f));
        CreateRing(Axis.Z, new Color(0.22f, 0.42f, 1f, 0.85f));
        gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        if (target == null)
            return;

        UpdateGizmoScale();

        if (isDragging)
            transform.position = target.position;
        else
            SyncToTarget();

        if (!isDragging || activeHandle == null || activeInteractor == null)
            return;

        if (!activeInteractor.isSelectActive)
        {
            DebugLog($"LateUpdate: interactor no longer active, ending drag axis={activeHandle.Axis}");
            EndDrag(activeHandle, activeInteractor);
            return;
        }

        if (!TryGetRingVector(activeHandle, activeInteractor, out var currentVector, out var currentPoint))
            return;

        lastDragVector = currentVector;
        float delta = Vector3.SignedAngle(startVector, currentVector, dragAxisWorld) * rotationSensitivity;
        target.rotation = Quaternion.AngleAxis(delta, dragAxisWorld) * startRotation;
        activeHandle.Interactable.SetAttachPoint(currentPoint, dragAxisWorld);
        DebugLogThrottled($"Drag axis={activeHandle.Axis} delta={delta:0.00} currentPoint={currentPoint} currentVector={currentVector}");
    }

    void SyncToTarget()
    {
        transform.SetPositionAndRotation(target.position, target.rotation);
    }

    void UpdateGizmoScale()
    {
        float desiredRadius = Mathf.Max(minimumVisibleRadius, EstimateTargetRadius() * boundsPadding);
        float scale = desiredRadius / Mathf.Max(0.0001f, radius);
        transform.localScale = Vector3.one * scale;
    }

    float EstimateTargetRadius()
    {
        if (target == null)
            return minimumVisibleRadius;

        bool hasBounds = false;
        var bounds = new Bounds(target.position, Vector3.zero);

        var renderers = target.GetComponentsInChildren<Renderer>();
        foreach (var renderer in renderers)
        {
            if (renderer == null || !renderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (!hasBounds)
        {
            var colliders = target.GetComponentsInChildren<Collider>();
            foreach (var collider in colliders)
            {
                if (collider == null || !collider.enabled)
                    continue;

                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(collider.bounds);
                }
            }
        }

        if (!hasBounds)
            return minimumVisibleRadius;

        return bounds.extents.magnitude;
    }

    public void BeginDrag(VRGizmoRingHandle handle, IXRSelectInteractor interactor)
    {
        if (target == null || handle == null || interactor == null)
        {
            DebugLog($"BeginDrag rejected: target={target}, handle={handle}, interactor={interactor}");
            return;
        }

        dragAxisWorld = GetAxisWorld(handle.Axis);
        if (!TryGetRingVector(handle, interactor, out startVector, out var startPoint))
        {
            DebugLog($"BeginDrag failed to compute start vector for {handle.Axis}");
            return;
        }

        activeHandle = handle;
        activeInteractor = interactor;
        startRotation = target.rotation;
        isDragging = true;
        lastDragVector = startVector;
        activeHandle.Interactable.SetAttachPoint(startPoint, dragAxisWorld);
        activeHandle.SetSelected(true);
        DebugLog($"BeginDrag axis={handle.Axis} startPoint={startPoint} startVector={startVector} interactor={interactor.transform.name}");
    }

    public void EndDrag(VRGizmoRingHandle handle, IXRSelectInteractor interactor)
    {
        if (handle != activeHandle && !ReferenceEquals(interactor, activeInteractor))
            return;

        if (activeHandle != null)
            activeHandle.SetSelected(false);

        activeHandle = null;
        activeInteractor = null;
        dragAxisWorld = Vector3.zero;
        lastDragVector = Vector3.zero;
        isDragging = false;
        DebugLog($"EndDrag axis={(handle != null ? handle.Axis.ToString() : "null")}");
        if (target != null)
            SyncToTarget();
    }

    public void ContinueDrag(VRGizmoRingHandle handle, IXRSelectInteractor interactor)
    {
        if (!isDragging || handle == null || interactor == null)
            return;

        activeHandle = handle;
        activeInteractor = interactor;
        DebugLog($"ContinueDrag axis={handle.Axis} interactor={interactor.GetType().Name}");
    }

    bool TryGetRingVector(VRGizmoRingHandle handle, IXRSelectInteractor interactor, out Vector3 vector, out Vector3 point)
    {
        vector = Vector3.zero;
        point = target != null ? target.position : Vector3.zero;
        if (handle == null || interactor == null || target == null)
        {
            DebugLogThrottled($"TryGetRingVector missing refs handle={handle}, interactor={interactor}, target={target}");
            return false;
        }

        var axis = handle.Axis;
        if (!TryGetInteractorRay(interactor, out var ray, out float maxDistance))
        {
            DebugLogThrottled($"TryGetRingVector no ray for {interactor.transform.name} ({interactor.GetType().Name})");
            return false;
        }

        Vector3 axisWorld = isDragging && handle == activeHandle ? dragAxisWorld : GetAxisWorld(axis);
        bool usedCollider = false;

        // Once selected, treat the wheel as a mathematical turntable. XRI ray endpoints can
        // be shortened by the cube or sibling rings, which creates dead zones and snap-back.
        bool preferPlane = isDragging && handle == activeHandle;
        bool hasPoint = preferPlane && TryProjectRayToPlane(ray, axisWorld, out point);
        if (!hasPoint && handle.Collider != null && handle.Collider.Raycast(ray, out var hit, maxDistance))
        {
            point = hit.point;
            usedCollider = true;
            hasPoint = true;
        }

        if (!hasPoint && !TryProjectRayToPlane(ray, axisWorld, out point))
        {
            DebugLogThrottled($"TryGetRingVector plane miss axis={axis} rayOrigin={ray.origin} rayDir={ray.direction}");
            return false;
        }

        vector = Vector3.ProjectOnPlane(point - target.position, axisWorld);

        if (preferPlane && vector.sqrMagnitude < 0.0004f &&
            TryGetClosestRingVector(ray, axisWorld, out var closestVector, out var closestPoint))
        {
            vector = closestVector;
            point = closestPoint;
            DebugLogThrottled($"RingVector axis={axis} source=closest-circle point={point} vector={vector}");
        }

        if (vector.sqrMagnitude < 0.000001f)
        {
            if (isDragging && lastDragVector.sqrMagnitude > 0.000001f)
            {
                vector = lastDragVector;
                point = target.position + vector * (radius * transform.lossyScale.x);
                return true;
            }

            DebugLogThrottled($"TryGetRingVector degenerate vector axis={axis} point={point} target={target.position} usedCollider={usedCollider}");
            return false;
        }

        vector.Normalize();
        point = target.position + vector * (radius * transform.lossyScale.x);
        DebugLogThrottled($"RingVector axis={axis} source={(usedCollider ? "collider" : "plane")} point={point} vector={vector}");
        return true;
    }

    bool TryGetClosestRingVector(Ray ray, Vector3 axisWorld, out Vector3 vector, out Vector3 point)
    {
        vector = Vector3.zero;
        point = target != null ? target.position : Vector3.zero;

        if (target == null)
            return false;

        float ringRadius = radius * transform.lossyScale.x;
        Vector3 center = target.position;
        Vector3 axis = axisWorld.normalized;
        Vector3 rayDir = ray.direction.normalized;

        // Project the ray into the ring plane, then choose the point on that 2D line
        // closest to the center. This removes the pivot-crossing deadzone from
        // ray-plane intersection while preserving laser-pointer style control.
        Vector3 projectedOrigin = Vector3.ProjectOnPlane(ray.origin - center, axis);
        Vector3 projectedDirection = Vector3.ProjectOnPlane(rayDir, axis);

        if (projectedDirection.sqrMagnitude < 0.000001f)
            return false;

        projectedDirection.Normalize();
        Vector3 closestOnProjectedRay = projectedOrigin - projectedDirection * Vector3.Dot(projectedOrigin, projectedDirection);

        if (closestOnProjectedRay.sqrMagnitude < 0.000001f)
        {
            vector = projectedDirection;
            if (lastDragVector.sqrMagnitude > 0.000001f &&
                Vector3.Dot(vector, lastDragVector) < Vector3.Dot(-vector, lastDragVector))
            {
                vector = -vector;
            }
        }
        else
        {
            vector = closestOnProjectedRay.normalized;
        }

        point = center + vector * ringRadius;
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
                DebugLogThrottled($"TryGetInteractorRay origin null for {rayInteractor.name}");
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
                DebugLogThrottled($"TryGetInteractorRay curve origin null for {interactor.transform.name}");
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

    bool TryProjectRayToPlane(Ray ray, Vector3 axisWorld, out Vector3 point)
    {
        var plane = new Plane(axisWorld, target.position);
        if (plane.Raycast(ray, out float enter))
        {
            point = ray.GetPoint(enter);
            return true;
        }

        point = default;
        return false;
    }

    public void DebugLog(string message)
    {
        if (!debugLogging)
            return;

        Debug.Log($"[VRRotationGizmo] {message}", this);
    }

    void DebugLogThrottled(string message)
    {
        if (!debugLogging || Time.time < nextDebugLogTime)
            return;

        nextDebugLogTime = Time.time + Mathf.Max(0.01f, debugLogInterval);
        Debug.Log($"[VRRotationGizmo] {message}", this);
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

    void CreateRing(Axis axis, Color color)
    {
        var ring = new GameObject($"Rotate {axis}");
        ring.transform.SetParent(transform, false);

        var filter = ring.AddComponent<MeshFilter>();
        filter.sharedMesh = CreateTorusMesh(axis, ringSegments, tubeSegments, radius, tubeRadius);

        var renderer = ring.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = CreateMaterial(color);

        var collider = ring.AddComponent<MeshCollider>();
        collider.sharedMesh = filter.sharedMesh;

        var interactable = ring.AddComponent<VRGizmoRingInteractable>();
        interactable.colliders.Clear();
        interactable.colliders.Add(collider);
        interactable.selectMode = InteractableSelectMode.Single;
        interactable.distanceCalculationMode = XRBaseInteractable.DistanceCalculationMode.ColliderPosition;

        var handle = ring.AddComponent<VRGizmoRingHandle>();
        handle.Initialize(this, axis, interactable, collider, renderer, color, selectedScale);
    }

    Material CreateMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        var material = new Material(shader);
        material.color = color;
        return material;
    }

    static Mesh CreateTorusMesh(Axis axis, int majorSegments, int minorSegments, float majorRadius, float minorRadius)
    {
        majorSegments = Mathf.Max(12, majorSegments);
        minorSegments = Mathf.Max(6, minorSegments);

        var vertices = new Vector3[majorSegments * minorSegments];
        var normals = new Vector3[vertices.Length];
        var triangles = new int[majorSegments * minorSegments * 6];

        for (int major = 0; major < majorSegments; major++)
        {
            float u = major / (float)majorSegments * Mathf.PI * 2f;
            var center = new Vector3(Mathf.Cos(u) * majorRadius, Mathf.Sin(u) * majorRadius, 0f);

            for (int minor = 0; minor < minorSegments; minor++)
            {
                float v = minor / (float)minorSegments * Mathf.PI * 2f;
                var normal = new Vector3(Mathf.Cos(u) * Mathf.Cos(v), Mathf.Sin(u) * Mathf.Cos(v), Mathf.Sin(v));
                int index = major * minorSegments + minor;
                vertices[index] = OrientForAxis(center + normal * minorRadius, axis);
                normals[index] = OrientForAxis(normal, axis);
            }
        }

        int tri = 0;
        for (int major = 0; major < majorSegments; major++)
        {
            int nextMajor = (major + 1) % majorSegments;
            for (int minor = 0; minor < minorSegments; minor++)
            {
                int nextMinor = (minor + 1) % minorSegments;
                int a = major * minorSegments + minor;
                int b = nextMajor * minorSegments + minor;
                int c = nextMajor * minorSegments + nextMinor;
                int d = major * minorSegments + nextMinor;

                triangles[tri++] = a;
                triangles[tri++] = b;
                triangles[tri++] = c;
                triangles[tri++] = a;
                triangles[tri++] = c;
                triangles[tri++] = d;
            }
        }

        var mesh = new Mesh
        {
            name = $"VR Rotation {axis} Ring",
            vertices = vertices,
            normals = normals,
            triangles = triangles
        };
        mesh.RecalculateBounds();
        return mesh;
    }

    static Vector3 OrientForAxis(Vector3 point, Axis axis)
    {
        return axis switch
        {
            Axis.X => new Vector3(point.z, point.x, point.y),
            Axis.Y => new Vector3(point.x, point.z, point.y),
            _ => point,
        };
    }
}

public class VRGizmoRingHandle : MonoBehaviour
{
    VRRotationGizmo owner;
    MeshRenderer meshRenderer;
    Color baseColor;
    float selectedScale = 1.6f;
    Vector3 baseScale;

    public VRRotationGizmo.Axis Axis { get; private set; }
    public VRGizmoRingInteractable Interactable { get; private set; }
    public Collider Collider { get; private set; }

    public void Initialize(
        VRRotationGizmo owner,
        VRRotationGizmo.Axis axis,
        VRGizmoRingInteractable interactable,
        Collider ringCollider,
        MeshRenderer renderer,
        Color color,
        float selectedScale)
    {
        this.owner = owner;
        Axis = axis;
        Interactable = interactable;
        Collider = ringCollider;
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
        if (Interactable == null)
            return;

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
        if (owner != null && owner.IsDragging)
            return;

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
            owner?.DebugLog($"Spurious deselect while trigger remains held, continuing drag axis={Axis}");
            owner?.ContinueDrag(this, args.interactorObject);
            return;
        }

        owner?.EndDrag(this, args.interactorObject);
    }
}

public class VRGizmoRingInteractable : XRSimpleInteractable, IFarAttachProvider
{
    Transform rimAttachTransform;

    public InteractableFarAttachMode farAttachMode { get; set; } = InteractableFarAttachMode.Near;

    protected override void Awake()
    {
        base.Awake();
        EnsureAttachTransform();
    }

    public override Transform GetAttachTransform(IXRInteractor interactor)
    {
        EnsureAttachTransform();
        return rimAttachTransform;
    }

    public void SetAttachPoint(Vector3 worldPoint, Vector3 axisWorld)
    {
        EnsureAttachTransform();
        rimAttachTransform.position = worldPoint;

        var radial = worldPoint - transform.position;
        if (radial.sqrMagnitude < 0.000001f)
            return;

        var tangent = Vector3.Cross(axisWorld.normalized, radial.normalized);
        if (tangent.sqrMagnitude > 0.000001f)
            rimAttachTransform.rotation = Quaternion.LookRotation(tangent.normalized, axisWorld.normalized);
    }

    void EnsureAttachTransform()
    {
        if (rimAttachTransform != null)
            return;

        var attachObject = new GameObject("Rim Attach");
        attachObject.transform.SetParent(transform, false);
        rimAttachTransform = attachObject.transform;
    }
}
