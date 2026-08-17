using UnityEngine;

public class Water : MonoBehaviour
{
    [Header("damage set")]
    public int damage = 1;
    public bool isPlayerInWater = false;
    public Collider2D col;
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            TryDamagePlayer(other);
            isPlayerInWater = true;
            col = other;
        }
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            isPlayerInWater = false;
        }
    }
    private void Update()
    {
        if (isPlayerInWater)
        {
            TryDamagePlayer(col);
        }
    }
    private void OnTriggerStay2D(Collider2D other)
    {
        if (other.gameObject.CompareTag("Player"))
        {
            isPlayerInWater = true;
            TryDamagePlayer(other);
        }
    }
    private void TryDamagePlayer(Collider2D other)
    {
        PlayerHP playerHP = other.gameObject.GetComponentInParent<PlayerHP>();
        playerHP.TakeDamage(damage);
    }
}