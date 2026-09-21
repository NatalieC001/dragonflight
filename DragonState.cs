using UnityEngine;

/// <summary>
/// This enum represents the current state of the Dragon.
///
/// IMPORTANT NOTE FOR EXTENSION:
/// This enum does NOT drive the logic of the Dragon. Instead, it acts as a "visual label".
/// Your future Node-Tree or AI logic system will update this value so that you (the developer)
/// can look at the Unity Inspector in real-time and see exactly what the AI is currently trying to do.
///
/// If you need to add more states in the future (e.g., "Sleeping", "Searching"),
/// you simply add them to this list, and then tell your Node-Tree to set the state to that new value.
/// </summary>
public enum DragonState
{
    Idle,               // Orchestrating, observing, or waiting
    Attacking,          // Actively engaging the player (breathing fire, shooting)
    Evading,            // Fleeing through the environment after taking heavy damage
    Regenerating,       // Resting or returning to base to regain stamina
    Dying,              // Health reached 0, performing death sequence
    TerritoryControl,   // Trying to corral the player or dominate an area
    CallingMinions,     // Summoning smaller enemies for support
    ProtectingCrystal   // Defending health/power crystals in the scene
}
