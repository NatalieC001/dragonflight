using UnityEngine;

/// <summary>
/// This is the DEFAULT placeholder brain for the Dragon.
/// It implements the IDragonBrain interface to act as the single entry point for logic.
///
/// IMPORTANT FOR EXTENSION:
/// When you are ready to use your new Node-Tree system, you will NOT use this script.
/// You will create your own script (e.g., NodeTreeDragonBrain) that implements IDragonBrain,
/// attach it to the Boss GameObject, and remove this DefaultDragonBrain component.
/// </summary>
public class DefaultDragonBrain : MonoBehaviour, IDragonBrain
{
    [Header("Current State (Read Only)")]
    [SerializeField]
    [Tooltip("This shows what the Brain is currently telling the Dragon to do.")]
    private DragonState currentState = DragonState.Idle;

    public DragonState CurrentState => currentState;

    [Header("Energy System Settings")]
    [Tooltip("How much stamina the boss has for attacking before it must retreat.")]
    public float maxStamina = 100f;
    [Tooltip("How fast stamina drains while attacking.")]
    public float staminaDrainRate = 10f;
    [Tooltip("How fast stamina recovers while on the observation spline.")]
    public float staminaRechargeRate = 15f;
    private float currentStamina;

    [Header("Evasion Settings")]
    [Tooltip("The boss will attempt to jump to an escape route if taking rapid damage.")]
    public float evasionDamageThreshold = 50f;

    private float recentDamageAccumulator = 0f;
    private float damageDecayTimer = 0f;

    // References to the "Body"
    private BossCreature bossHealth;
    private AirborneBossMovement movementSystem;

    private void Awake()
    {
        bossHealth = GetComponent<BossCreature>();
        movementSystem = GetComponent<AirborneBossMovement>();

        currentStamina = maxStamina;
    }

    private void Start()
    {
        // Start in Idle/Orchestrator state
        ChangeState(DragonState.Idle);
    }

    private void Update()
    {
        if (currentState == DragonState.Dying) return;

        // 1. Handle Stamina System (Logic moved here from BossCreature)
        if (currentState == DragonState.Attacking)
        {
            currentStamina -= staminaDrainRate * Time.deltaTime;
            if (currentStamina <= 0)
            {
                currentStamina = 0;
                EnterEvadingState();
            }
        }
        else if (currentState == DragonState.Regenerating)
        {
            currentStamina += staminaRechargeRate * Time.deltaTime;
            if (currentStamina >= maxStamina)
            {
                currentStamina = maxStamina;
                Debug.Log("<color=green>[DefaultDragonBrain] Stamina full! Diving back in to attack!</color>");
                ChangeState(DragonState.Attacking);
            }
        }

        // 2. Handle Damage Decay (Logic moved here from BossCreature)
        if (recentDamageAccumulator > 0)
        {
            damageDecayTimer += Time.deltaTime;
            if (damageDecayTimer > 3f) // Reset accumulator after 3 seconds of no damage
            {
                recentDamageAccumulator = 0f;
                damageDecayTimer = 0f;
            }
        }
    }

    private void ChangeState(DragonState newState)
    {
        if (currentState == DragonState.Dying) return; // Cannot change state if dying

        currentState = newState;

        // Command the movement system based on the new state
        if (movementSystem != null)
        {
            switch (newState)
            {
                case DragonState.Idle:
                case DragonState.Regenerating:
                    movementSystem.StartObservationPath();
                    break;
                case DragonState.Attacking:
                    // In the default logic, attacking might use observation path or freeform,
                    // depending on how it was set up. We'll leave it as observation for now.
                    movementSystem.StartObservationPath();
                    break;
                case DragonState.Evading:
                    movementSystem.StartEscapePath();
                    break;
                case DragonState.Dying:
                    movementSystem.StopMovement();
                    break;
                // Other states (TerritoryControl, CallingMinions, ProtectingCrystal)
                // would have their specific movement commands here when implemented.
            }
        }
    }

    private void EnterEvadingState()
    {
        Debug.Log("<color=cyan>[DefaultDragonBrain] Dragon is exhausted or hurt! Fleeing to recharge!</color>");

        // Permanently decay stamina so fights don't last forever
        maxStamina *= 0.7f;
        if (maxStamina < 20f) maxStamina = 20f; // Minimum stamina floor so it can still fight briefly

        ChangeState(DragonState.Evading);
    }

    // --- IDragonBrain Interface Implementation ---

    public void OnDamageTaken(float amount)
    {
        if (currentState == DragonState.Dying) return;

        // If the boss is hit while idle, maybe it engages early!
        if (currentState == DragonState.Idle)
        {
            Debug.Log("<color=red>[DefaultDragonBrain] You dared to shoot the boss while it was watching? It attacks early!</color>");
            ChangeState(DragonState.Attacking);
        }

        // Evaluate if we need to evade
        if (currentState != DragonState.Idle)
        {
            recentDamageAccumulator += amount;
            damageDecayTimer = 0f; // Reset decay

            if (recentDamageAccumulator >= evasionDamageThreshold)
            {
                Debug.Log("<color=red>[DefaultDragonBrain] Threat level high! Ordering tactical evasion.</color>");

                // Reset accumulators so we don't re-trigger immediately
                recentDamageAccumulator = 0f;
                damageDecayTimer = 0f;

                EnterEvadingState();
            }
        }
    }

    public void OnDeath()
    {
        Debug.Log("<color=red>[DefaultDragonBrain] Command received: Dragon is dying.</color>");
        ChangeState(DragonState.Dying);
    }

    public void OnPathFinished()
    {
        // When the boss finishes an escape route, it should start recharging.
        if (currentState == DragonState.Evading)
        {
            Debug.Log("<color=cyan>[DefaultDragonBrain] Recharging stamina on the observation deck!</color>");
            ChangeState(DragonState.Regenerating);
        }
    }
}
