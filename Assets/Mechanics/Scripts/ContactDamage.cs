using UnityEngine;

public class ContactDamage : MonoBehaviour
{
    [SerializeField] private int damage = 1;
    [SerializeField] private Vector2 knockback = new Vector2(8f, 4f);
    [SerializeField] private LayerMask targetLayer;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!IsTargetLayer(collision.gameObject.layer)) return;

        PlayerHP hp = collision.gameObject.GetComponentInParent<PlayerHP>();
        if (hp == null) return;

        hp.TakeDamage(damage);
        ApplyKnockback(collision.gameObject);
    }

    private bool IsTargetLayer(int layer)
    {
        return targetLayer.value == 0 || (targetLayer.value & (1 << layer)) != 0;
    }

    private void ApplyKnockback(GameObject target)
    {
        Rigidbody2D rb = target.GetComponentInParent<Rigidbody2D>();
        if (rb == null) return;
        float dir = Mathf.Sign(rb.transform.position.x - transform.position.x);
        if (dir == 0f) dir = 1f;
        rb.velocity = new Vector2(knockback.x * dir, knockback.y);
    }
}