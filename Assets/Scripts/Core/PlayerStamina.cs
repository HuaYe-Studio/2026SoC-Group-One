using UnityEngine;

public class PlayerStamina : MonoBehaviour
{
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float recoverPerSecond = 30f;

    private float _current;

    public float Current => _current;
    public float Max => maxStamina;
    public float RecoverPerSecond => recoverPerSecond;
    public float Ratio => maxStamina > 0f ? _current / maxStamina : 0f;
    public bool IsEmpty => _current <= 0f;

    private void Awake()
    {
        _current = maxStamina;
    }

    public bool Spend(float amount)
    {
        if (_current <= 0f) return false;
        _current = Mathf.Max(0f, _current - amount);
        return true;
    }

    public void Restore(float amount)
    {
        _current = Mathf.Min(maxStamina, _current + amount);
    }
}
