using UnityEngine;

/// <summary>
/// This component now acts purely as the "Body" of the Dragon.
/// It holds stats (health, resistances) and reports when it gets hit to the Brain.
/// It NO LONGER makes decisions about whether to evade, attack, or retreat.
/// </summary>
[RequireComponent(typeof(BaseBossMovement))]
public class BossCreature : MonoBehaviour
{
    [System.Serializable]
    public struct ElementalModifier
    {
        public ElementTypeOB7 arrowType;
        [Tooltip("1.0 = normal damage. 2.0 = double damage (weakness). 0.0 = immune. -1.0 = heals the boss!")]
        public float damageMultiplier;
    }

    [Header("Boss Stats")]
    public float maxHealth = 500f;
    private float currentHealth;

    [Header("Elemental Resistances")]
    [Tooltip("If an arrow type isn't listed here, it deals standard 1.0x damage.")]
    public ElementalModifier[] elementalModifiers;

    [Header("Anatomy Tracking")]
    [Tooltip("Dynamically found on Awake. Used as the origin point for breath attacks or projectiles.")]
    public Transform mouthTransform { get; private set; }

    private CreatureStatusEffects statusEffects;
    private BaseBossMovement movementSystem;

    // The "Brain" that makes decisions when this "Body" takes damage
    private IDragonBrain brain;

    // Injected by WaveSpawner to explicitly report Boss death progression
    private WaveSpawner waveSpawner;

    public void Initialize(WaveSpawner spawner)
    {
        waveSpawner = spawner;
    }

    private void Awake()
    {
        statusEffects = GetComponent<CreatureStatusEffects>();
        movementSystem = GetComponent<BaseBossMovement>();
        brain = GetComponent<IDragonBrain>();
        currentHealth = maxHealth;

        if (brain == null)
        {
            Debug.LogWarning("[BossCreature] No IDragonBrain found on this GameObject! The Dragon will not react to damage.");
        }
    }

    private void Start()
    {
        // Boss anatomy spawns cleanly at root. It doesn't need to be forced onto a track immediately!
        SegmentedDragonManager dragonBody = GetComponent<SegmentedDragonManager>();
        if (dragonBody != null)
        {
            // Try to get the SplineComputer already assigned to this root's SplineFollower.
            // AirborneBossMovement.Start() will assign it in the same frame or next frame;
            // passing null here is safe — SegmentedDragonManager handles null gracefully by
            // placing all segments at the root spawn position until the spline is provided via SwitchToNewSpline().
            Dreamteck.Splines.SplineFollower rootFollower = GetComponent<Dreamteck.Splines.SplineFollower>();
            Dreamteck.Splines.SplineComputer initialSpline = rootFollower != null ? rootFollower.spline : null;

            Debug.Log($"[BossCreature] Instructing SegmentedDragonManager to spawn anatomy. Initial spline: {(initialSpline != null ? initialSpline.name : "none — segments will cluster at spawn position until first path is assigned")}");
            dragonBody.InitializeDragon(initialSpline);
        }

        FindMouthTransform();
    }

    /// <summary>
    /// Dynamically searches all children (even deeply nested ones) for an object exactly named 'MouthTransform'.
    /// </summary>
    private void FindMouthTransform()
    {
        if (mouthTransform != null) return; // Already assigned in Inspector

        Transform[] allChildren = GetComponentsInChildren<Transform>(true);
        foreach (Transform t in allChildren)
        {
            if (t.name == "mouthTransform")
            {
                mouthTransform = t;
                Debug.Log($"<color=green>[BossCreature] Successfully located MouthTransform on {t.parent.name}</color>");
                return;
            }
        }

        Debug.LogWarning("[BossCreature] Could not find a child object named 'MouthTransform'. Breath attacks may fail!");
    }

    /// <summary>
    /// Called when the player shoots the boss.
    /// </summary>
    public void TakeDamage(float baseAmount, Vector3 hitPoint, ElementTypeOB7 arrowType = ElementTypeOB7.Normal)
    {
        // 0. Trigger Status Effects (Slows, Freezes)
        if (statusEffects != null)
        {
            statusEffects.ApplyElementalEffect(arrowType);
        }

        // 1. Calculate actual damage based on elemental weaknesses/resistances
        float actualDamage = baseAmount;
        if (elementalModifiers != null)
        {
            foreach (var mod in elementalModifiers)
            {
                if (mod.arrowType == arrowType)
                {
                    actualDamage *= mod.damageMultiplier;
                    break;
                }
            }
        }

        // 1.5 Apply Brittle modifier if frozen by previous ice arrows
        if (statusEffects != null && statusEffects.IsBrittle && actualDamage > 0)
        {
            Debug.Log($"<color=cyan>[BossCreature] Boss Brittle shattered! Damage doubled.</color>");
            actualDamage *= 2.0f;
        }

        // 2. Apply damage or healing
        if (actualDamage < 0)
        {
            currentHealth -= actualDamage;
            if (currentHealth > maxHealth) currentHealth = maxHealth;
            Debug.Log($"<color=green>[BossCreature] Absorbed {arrowType} magic! HEALED for {-actualDamage}. Current Health: {currentHealth}</color>");
            return;
        }

        currentHealth -= actualDamage;
        Debug.Log($"<color=orange>[BossCreature] Hit by {arrowType} arrow! Took {actualDamage} damage. Remaining Health: {currentHealth}</color>");

        if (currentHealth <= 0)
        {
            Die();
            return;
        }

        // Pass the event to the Brain to decide how to react
        if (brain != null)
        {
            brain.OnDamageTaken(actualDamage);
        }
    }

    private void Die()
    {
        Debug.Log("<color=red>[BossCreature] The Boss has been defeated!</color>");

        // Ping the injected WaveSpawner so the level progression can cleanly move to Victory.
        // Fall back to a scene search if the boss was manually placed (not spawned via WaveSpawner).
        WaveSpawner spawnerToNotify = waveSpawner;
        if (spawnerToNotify == null)
        {
            spawnerToNotify = FindFirstObjectByType<WaveSpawner>();
            if (spawnerToNotify != null)
            {
                Debug.Log("[BossCreature] WaveSpawner not injected — found it via scene search.");
            }
        }

        if (spawnerToNotify != null)
        {
            spawnerToNotify.NotifyTargetDestroyed();
        }
        else
        {
            Debug.LogWarning("[BossCreature] No WaveSpawner found — level progression cannot advance after boss death!");
        }

        // Notify the Brain that we died
        if (brain != null)
        {
            brain.OnDeath();
        }

        // Stop movement
        if (movementSystem != null)
        {
            movementSystem.enabled = false;
        }

        // Command all indestructible pieces (Head, Legs, Tail) to dissolve
        SegmentedDragonManager dragonBody = GetComponent<SegmentedDragonManager>();
        if (dragonBody != null)
        {
            dragonBody.TriggerTotalDeath();
        }

        // Clean up the main boss entity
        Destroy(gameObject, 1f);
    }
}
