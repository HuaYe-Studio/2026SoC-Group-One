using UnityEngine;

public class IceSurface : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if (pc != null) pc.EnterIce(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if (pc != null) pc.ExitIce(this);
    }
}