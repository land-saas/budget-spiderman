using UnityEngine;

/// <summary>A saved rope attachment point. The game recycles it without replacing its art.</summary>
[RequireComponent(typeof(Rigidbody2D),typeof(CircleCollider2D))]
public sealed class SwingAnchor : MonoBehaviour
{
    public Rigidbody2D body;
    public Transform ring;
}
