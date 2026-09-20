using UnityEngine;
using Dreamteck.Splines;

/// <summary>
/// Inherits from BaseBossMovement.
/// Used for purely flying bosses (like the Dragon or Butterfly).
/// Exclusively requests Airborne paths from the BossPathManager.
/// 
/// Movement is driven by the ROOT object's SplineFollower (assigned here),
/// and SegmentedDragonManager's breadcrumb system drags all body segments behind it.
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

        // Start with following disabled — Start() will assign the first path.
        rootFollower.follow = false;
    }

    protected virtual void Start()
    {
        InitialiseOnObservationPath();
    }

    /// <summary>
    /// Picks the first available Airborne observation path and snaps the root onto it.
    /// Called once on Start so the dragon is immediately visible and moving.
    /// </summary>
    private void InitialiseOnObservationPath()
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
        rootFollower.wrapMode = SplineFollower.Wrap.Loop;
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

        // Read phase from the BossCreature brain to drive movement decisions.
        bool desiresToEscape = bossBrain != null &&
            (bossBrain.currentPhase == BossCreature.BossPhase.Exhausted);

        if (desiresToEscape)
        {
            ExecuteEscapeChoreography();
        }
        else
        {
            ExecuteObservationSpline();
        }
    }

    // --- Observation (looping patrol path) ---

    private void ExecuteObservationSpline()
    {
        // If we already have a path assigned and the follower is running, nothing to do.
        if (currentActivePath != null && rootFollower != null && rootFollower.follow)
        {
            return;
        }

        // Otherwise pick a path and start following.
        currentActivePath = GetObservationPath(PathTypeTag.PathType.Airborne);
        if (currentActivePath != null)
        {
            AssignSplineAndFollow(currentActivePath, observationSpeed);
            Debug.Log($"[{gameObject.name}] Resuming observation spline: {currentActivePath.name}");
        }
    }

    // --- Escape choreography ---

    private void ExecuteEscapeChoreography()
    {
        if (currentActivePath == null)
        {
            currentActivePath = FindNearestEscapeRoute(PathTypeTag.PathType.Airborne);
        }

        if (currentActivePath != null)
        {
            AssignSplineAndFollow(currentActivePath, escapeSpeed);
        }
        else
        {
            // Freestyle fallback: just fly upward if no escape route exists.
            transform.Translate(Vector3.up * escapeSpeed * Time.deltaTime, Space.World);
        }
    }

    // --- Forced immediate evasion (called by BossCreature when burst damage threshold hit) ---

    public override void ForceImmediateEvasion()
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
        }
    }

    // --- Phase change hook (called by BossCreature.ChangePhase) ---

    public override void OnPhaseChanged(int newPhase)
    {
        base.OnPhaseChanged(newPhase);

        BossCreature.BossPhase phase = (BossCreature.BossPhase)newPhase;

        switch (phase)
        {
            case BossCreature.BossPhase.Orchestrator:
            case BossCreature.BossPhase.Recharging:
                // Return to observation loop — pick a fresh path.
                currentActivePath = null;
                ExecuteObservationSpline();
                break;

            case BossCreature.BossPhase.Exhausted:
                // ForceImmediateEvasion will be called separately by BossCreature.EnterExhaustedPhase.
                break;
        }
    }

    // --- Tether rubber-band ---

    private void ApplyTetherRubberBand()
    {
        // Keep the dragon within the tether radius of the anchor.
        if (bossBrain == null) return;

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
