using UnityEngine;

public class WaterZone : MonoBehaviour
{
    [SerializeField] private bool deepWater = false;
    public bool IsDeep => deepWater;

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if (pc != null) pc.EnterWater(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerController pc = other.GetComponentInParent<PlayerController>();
        if (pc != null) pc.ExitWater(this);
    }
}