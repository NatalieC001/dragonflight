using UnityEngine;
using Dreamteck.Splines;

/// <summary>
/// Inherits from BaseBossMovement.
/// Used for purely flying bosses (like the Dragon or Butterfly).
/// Exclusively requests Airborne paths from the BossPathManager.
/// 
/// Movement is driven by the ROOT object's SplineFollower (assigned here),
/// and SegmentedDragonManager's breadcrumb system drags all body segments behind it.
///
/// Note: This script is now purely a movement executor. Decisions on WHEN to escape
/// or WHEN to observe are handed by IDragonBrain.
/// </summary>
public class AirborneBossMovement : BaseBossMovement
{
    [Header("Airborne Movement Settings")]
    [Tooltip("Speed at which the dragon follows its observation spline (units/sec).")]
    public float observationSpeed = 8f;

    [Tooltip("Speed at which the dragon flies along an escape route.")]
    public float escapeSpeed = 18f;

    private GameObject currentActivePath;
    private SplineFollower rootFollower;
    private IDragonBrain brain;

    // --- Initialise once the scene is ready ---
    protected override void Awake()
    {
        base.Awake(); // finds pathManager and bossBrain

        // The root object IS the SplineFollower that drives the whole dragon.
        rootFollower = GetComponent<SplineFollower>();
        if (rootFollower == null)
        {
            rootFollower = gameObject.AddComponent<SplineFollower>();
            Debug.Log($"[{gameObject.name}] AirborneBossMovement: Added SplineFollower to root.");
        }

        brain = GetComponent<IDragonBrain>();

        // Start with following disabled — Start() will assign the first path.
        rootFollower.follow = false;

        // Listen for when a path finishes so we can tell the brain
        if (rootFollower != null)
        {
            rootFollower.onNode += OnNodePassed;
        }
    }

    private void OnNodePassed(System.Collections.Generic.List<SplineTracer.NodeConnection> passed)
    {
        // Simple way to detect end of an open path (like an escape route)
        if (rootFollower != null && !rootFollower.spline.isClosed && rootFollower.GetPercent() >= 0.99)
        {
            if (brain != null)
            {
                brain.OnPathFinished();
            }
        }
    }

    protected virtual void Start()
    {
        // InitialiseOnObservationPath is now typically called by the Brain,
        // but we can leave this here as a fallback or if the brain starts in Idle.
    }

    /// <summary>
    /// Called by the Brain. Picks the first available Airborne observation path and snaps the root onto it.
    /// </summary>
    public void StartObservationPath()
    {
        if (pathManager == null)
        {
            Debug.LogWarning($"[{gameObject.name}] AirborneBossMovement: No BossPathManager in scene — cannot initialise path.");
            return;
        }

        currentActivePath = GetObservationPath(PathTypeTag.PathType.Airborne);

        if (currentActivePath != null)
        {
            AssignSplineAndFollow(currentActivePath, observationSpeed);
            Debug.Log($"<color=green>[{gameObject.name}] AirborneBossMovement: Dragon placed on observation path '{currentActivePath.name}'.</color>");
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] AirborneBossMovement: No Airborne observation paths found. Dragon will stay at spawn position.");
        }
    }

    /// <summary>
    /// Called by the Brain. Finds an escape route and follows it.
    /// </summary>
    public void StartEscapePath()
    {
        GameObject escapePath = FindNearestEscapeRoute(PathTypeTag.PathType.Airborne);
        if (escapePath != null)
        {
            currentActivePath = escapePath;
            AssignSplineAndFollow(currentActivePath, escapeSpeed);
            Debug.Log($"[{gameObject.name}] Airborne movement jumping to escape route: {currentActivePath.name}");
        }
        else
        {
            Debug.LogWarning($"[{gameObject.name}] Tried to evade, but no Airborne escape routes found by BossPathManager!");
            // Freestyle fallback
            if (rootFollower != null) rootFollower.follow = false;
            transform.Translate(Vector3.up * escapeSpeed * Time.deltaTime, Space.World);
        }
    }

    /// <summary>
    /// Stops the SplineFollower entirely.
    /// </summary>
    public void StopMovement()
    {
        if (rootFollower != null)
        {
            rootFollower.follow = false;
        }
    }

    /// <summary>
    /// Assigns a SplineComputer from the given path GameObject to the root SplineFollower and starts following.
    /// </summary>
    private void AssignSplineAndFollow(GameObject pathObj, float speed)
    {
        if (rootFollower == null || pathObj == null) return;

        // The path prefab can hold the SplineComputer directly on the root or on a child.
        SplineComputer splineComputer = pathObj.GetComponentInChildren<SplineComputer>();
        if (splineComputer == null)
        {
            Debug.LogWarning($"[{gameObject.name}] Path '{pathObj.name}' has no SplineComputer. Cannot follow.");
            return;
        }

        rootFollower.spline = splineComputer;
        rootFollower.followSpeed = speed;

        // Let the brain decide if it loops or not. Usually observation loops, escape doesn't.
        // As a quick fix for the transition, we'll assume closed splines loop and open ones don't.
        if (splineComputer.isClosed)
        {
             rootFollower.wrapMode = SplineFollower.Wrap.Loop;
        }
        else
        {
             rootFollower.wrapMode = SplineFollower.Wrap.Default;
        }

        rootFollower.follow = true;

        // Also update the SegmentedDragonManager's internal spline reference so the breadcrumb
        // history stays in sync with whichever track the dragon is currently following.
        SegmentedDragonManager dragonBody = GetComponent<SegmentedDragonManager>();
        if (dragonBody != null)
        {
            dragonBody.SwitchToNewSpline(splineComputer);
        }
    }

    // --- Core Movement Tick ---

    protected override void TickMovement()
    {
        if (isTethered)
        {
            ApplyTetherRubberBand();
            return;
        }

        // We no longer read BossPhase here. The Brain tells us what to do via methods.
        // If we have an open path and we've reached the end, notify the brain (fallback if onNode doesn't catch it)
        if (rootFollower != null && rootFollower.follow && rootFollower.spline != null && !rootFollower.spline.isClosed)
        {
             if (rootFollower.GetPercent() >= 0.999)
             {
                 rootFollower.follow = false; // Stop moving past the end
                 if (brain != null) brain.OnPathFinished();
             }
        }
    }

    // --- Forced immediate evasion (Legacy override) ---

    public override void ForceImmediateEvasion()
    {
        // This is here to satisfy the base class, but shouldn't be called directly anymore.
        StartEscapePath();
    }

    // --- Phase change hook (Legacy override) ---

    public override void OnPhaseChanged(int newPhase)
    {
        // We no longer use this. The Brain handles state.
        base.OnPhaseChanged(newPhase);
    }

    // --- Tether rubber-band ---

    private void ApplyTetherRubberBand()
    {
        // Keep the dragon within the tether radius of the anchor.
        SegmentedDragonManager dragonBody = GetComponent<SegmentedDragonManager>();
        if (dragonBody == null || !dragonBody.IsTethered) return;

        Transform anchor = dragonBody.TetherAnchorTransform;
        float maxLength = dragonBody.TetherMaxLength;
        if (anchor == null || maxLength <= 0f) return;

        Vector3 toAnchor = anchor.position - transform.position;
        if (toAnchor.magnitude > maxLength)
        {
            // Rubber-band: push root back toward anchor boundary.
            transform.position = anchor.position - toAnchor.normalized * maxLength;
            if (rootFollower != null) rootFollower.follow = false;
        }
    }
}
