using UnityEngine;

/// <summary>Persistent prefab component for a collectible gem.</summary>
[RequireComponent(typeof(CircleCollider2D))]
public sealed class SwingGemPickup : MonoBehaviour
{
    [HideInInspector] public TwinSwingGame game;
    [HideInInspector] public int index;
    [HideInInspector] public bool collected;
    public Transform visual;
    public CircleCollider2D pickupCollider;
}
