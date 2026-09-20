using System;
using UnityEngine;

/// <summary>
/// Runtime Health Crystal for boss recharge/regeneration.
/// - Ensures Collider + Rigidbody exist so arrows physically hit it in the scene.
/// - Implements IArrowTarget so arrow code can call OnArrowHit(...) directly.
/// - Exposes events for damage and destruction so BossCreature / SegmentedDragonManager can react.
/// </summary>
[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]
public class HealthCrystal : MonoBehaviour, IArrowTarget
{
    [Tooltip("Human friendly name shown in debug lists and logs.")]
    public string displayName = "HealthCrystal";

    [Tooltip("Maximum hit points of the crystal.")]
    public float maxHealth = 200f;

    [Tooltip("When true the crystal will be destroyed once health hits zero (default true).")]
    public bool destroyOnZero = true;

    // Current runtime health
    private float currentHealth;

    // Public read-only
    public bool IsDestroyed => currentHealth <= 0f;

    // Events for other systems
    public event Action<HealthCrystal> OnCrystalDestroyed;
    public event Action<HealthCrystal> OnCrystalDamaged;

    private void Reset()
    {
        // Ensure sensible defaults when added in editor
        displayName = string.IsNullOrEmpty(displayName) ? gameObject.name : displayName;

        // Add a SphereCollider if no collider exists (editor convenience)
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            SphereCollider sc = gameObject.AddComponent<SphereCollider>();
            sc.isTrigger = false;
            sc.radius = 1f;
        }

        // Ensure Rigidbody exists and is configured for a static but collidable object
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void Awake()
    {
        currentHealth = maxHealth;
        if (string.IsNullOrEmpty(displayName)) displayName = gameObject.name;

        // Ensure Collider and Rigidbody present and correctly configured at runtime
        Collider col = GetComponent<Collider>();
        if (col == null)
        {
            col = gameObject.AddComponent<SphereCollider>();
            col.isTrigger = false;
        }

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        // Crystal shouldn't be moved by physics, but must receive collision callbacks
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    /// <summary>
    /// Apply damage to the crystal. When health reaches zero, it triggers destruction event.
    /// Also callable by arrow systems via the IArrowTarget interface's OnArrowHit.
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (IsDestroyed) return;

        float dmg = Mathf.Abs(amount);
        currentHealth -= dmg;

        OnCrystalDamaged?.Invoke(this);
        Debug.Log($"[HealthCrystal] {displayName} took {dmg:F1} damage. Remaining: {currentHealth:F1}/{maxHealth:F1}");

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            Debug.Log($"[HealthCrystal] {displayName} destroyed!");
            OnCrystalDestroyed?.Invoke(this);

            if (destroyOnZero)
            {
                // Small delay to allow effects; immediate Destroy is acceptable if visuals not needed
                Destroy(gameObject, 0.1f);
            }
        }
    }

    /// <summary>
    /// Returns normalized 0..1 health
    /// </summary>
    public float GetHealthNormalized()
    {
        return maxHealth > 0f ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
    }

    /// <summary>
    /// Implementation of IArrowTarget - called by arrow systems when
    /// an arrow hits this object. This forwards damage to TakeDamage.
    /// </summary>
    public void OnArrowHit(float damage, Vector3 impactPoint, ElementTypeOB7 elementType)
    {
        // Arrow damage passed through. You can extend elemental behavior here if needed.
        TakeDamage(damage);
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Defensive convenience: if an arrow collides but did not invoke OnArrowHit (rare),
        // try to extract damage from common arrow components and apply it.
        // This keeps the crystal reliably hittable by both physics-based and message-based arrows.

        // Check for a StickingArrow / Arrow component that might carry damage info.
        var sticking = collision.collider.GetComponentInParent<StickingArrow>();
        if (sticking != null)
        {
            // StickingArrow doesn't expose a standard API here, so prefer arrows to call OnArrowHit.
            // If you have a specific arrow damage API, plug it in here.
            return;
        }

        // If arrow systems don't use StickingArrow, nothing forced here.
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.6f);
    }
}