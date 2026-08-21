using UnityEngine;

public class Spike : MonoBehaviour
{
    [Header("damage set")]
    public int damage = 1;
    public bool isPlayerOnSpike = false;
    public Collision2D col;
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            TryDamagePlayer(collision);
            isPlayerOnSpike = true;
            col = collision;
        }
    }
    private void OnCollisionExit2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            isPlayerOnSpike = false;
        }
    }
    private void Update()
    {
        if (isPlayerOnSpike)
        {
            TryDamagePlayer(col);
        }
    }
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            isPlayerOnSpike = true;
            TryDamagePlayer(collision);
        }
    }
    private void TryDamagePlayer(Collision2D collision)
    {
        if (collision == null || collision.gameObject == null) return;
        PlayerHP playerHP = collision.gameObject.GetComponent<PlayerHP>();
        if (playerHP != null)
        {
            playerHP.TakeDamage(damage);
        }
    }
}