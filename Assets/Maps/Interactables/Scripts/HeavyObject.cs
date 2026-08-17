using UnityEngine;

public class HeavyObject : MonoBehaviour, IHeavy
{
    [SerializeField] private bool isHeavy = true;

    public bool IsHeavy => isHeavy ;
    public void SetHeavyState(bool heavy)
    {
        isHeavy = heavy;
    }

    public void ToggleHeavy()
    {
        isHeavy = !isHeavy;
    }
}