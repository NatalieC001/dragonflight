using UnityEngine;

/// <summary>
/// This interface defines the SINGLE POINT OF ENTRY for the Dragon's intelligence (the "Brain").
///
/// HOW IT WORKS:
/// - The "Body" components (like BossCreature for health, or AirborneBossMovement for moving) do NOT make decisions.
/// - Instead, they report events to the Brain (e.g., "I took damage", "My stamina is low").
/// - The Brain receives these events, uses its logic (Node Tree, State Machine, etc.) to decide what to do,
///   and then issues commands back to the Body (e.g., "Start Evading!").
///
/// HOW TO EXTEND IT:
/// - When you build your custom Node-Tree system, you will create a script that implements this interface:
///   `public class MyNodeTreeBrain : MonoBehaviour, IDragonBrain { ... }`
/// - You will implement these methods to trigger the corresponding logic in your node tree.
/// - Once your custom Brain is ready, you can simply attach it to the Dragon and remove the DefaultDragonBrain.
/// </summary>
public interface IDragonBrain
{
    /// <summary>
    /// The current visual state of the Dragon.
    /// The Brain should update this whenever it changes what it's doing so the developer can see it in the Inspector.
    /// </summary>
    DragonState CurrentState { get; }

    /// <summary>
    /// Called by BossCreature when the dragon takes damage.
    /// The Brain decides if this is enough damage to trigger an evasion or other reaction.
    /// </summary>
    /// <param name="amount">The amount of damage taken.</param>
    void OnDamageTaken(float amount);

    /// <summary>
    /// Called by BossCreature when health reaches zero.
    /// The Brain should transition to the Dying state and command the body to perform death sequences.
    /// </summary>
    void OnDeath();

    /// <summary>
    /// Called by AirborneBossMovement when the dragon finishes its current path.
    /// The Brain decides where the dragon should go next.
    /// </summary>
    void OnPathFinished();

    // You can easily add more events here in the future as your Node Tree gets more complex!
    // Example: void OnCrystalAttacked(HealthCrystal crystal);
    // Example: void OnPlayerSpotted(Transform playerTransform);
}
