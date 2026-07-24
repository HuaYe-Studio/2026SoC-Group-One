using UnityEngine;

public class DevourableAnimal : MonoBehaviour
{
    [SerializeField] private FormType grantedForm = FormType.Frog;

    [Header("Spit")]
    [SerializeField] private float spitForce = 10f;

    public FormType GrantedForm => grantedForm;
    public bool IsTargeted { get; set; }
    public SpriteRenderer SpriteRenderer { get; private set; }

    private Rigidbody2D _rb;
    private Animator _animator;

    private void Awake()
    {
        SpriteRenderer = GetComponent<SpriteRenderer>();
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
    }

    public void PlayBeingDevoured()
    {
        if (_rb != null)
            _rb.velocity = Vector2.zero;
        SafeSetTrigger("Devoured");
    }

    public void PlayBeingSpitOut(Vector2 direction)
    {
        // 恢复被吞噬效果改变的视觉状态
        if (SpriteRenderer != null)
        {
            SpriteRenderer.color = new Color(1f, 1f, 1f, 1f);
        }
        transform.localScale = Vector3.one;

        if (_rb != null)
            _rb.velocity = direction.normalized * spitForce;
        SafeSetTrigger("SpitOut");
    }

    private void SafeSetTrigger(string name)
    {
        if (_animator == null) return;
        foreach (var param in _animator.parameters)
        {
            if (param.name == name && param.type == AnimatorControllerParameterType.Trigger)
            {
                _animator.SetTrigger(name);
                return;
            }
        }
    }
}
