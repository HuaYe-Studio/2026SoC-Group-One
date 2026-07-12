using UnityEngine;

public class DevourableAnimal : MonoBehaviour
{
    [SerializeField] private FormType grantedForm = FormType.Frog;

    public FormType GrantedForm => grantedForm;
    public bool IsTargeted { get; set; }
    public SpriteRenderer SpriteRenderer { get; private set; }

    private void Awake()
    {
        SpriteRenderer = GetComponent<SpriteRenderer>();
    }
}
