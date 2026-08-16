using UnityEngine;

/// <summary>
/// [个体差异] 动物基础数值随机组件：Awake 时按配置区间随机基础数值（倍率，1=物种基准），
/// 使同种动物个体间产生差异；并推导单一"强度分"（0~1）供领地大小等系统使用。
/// 用法：挂到动物 GameObject（与 AnimalBase 同级）。BT 会自动补挂，可手动挂到 prefab 调参。
/// </summary>
public class AnimalStats : MonoBehaviour
{
    [Header("随机区间（倍率，1 = 物种基准）")]
    [Tooltip("速度倍率区间")]
    [SerializeField] private Vector2 _speedRange = new Vector2(0.8f, 1.2f);
    [Tooltip("感知倍率区间")]
    [SerializeField] private Vector2 _perceptionRange = new Vector2(0.8f, 1.2f);
    [Tooltip("攻击倍率区间")]
    [SerializeField] private Vector2 _attackRange = new Vector2(0.8f, 1.2f);

    [Header("强度分权重（三个倍率的加权平均）")]
    [SerializeField] private float _speedWeight = 0.4f;
    [SerializeField] private float _perceptionWeight = 0.2f;
    [SerializeField] private float _attackWeight = 0.4f;

    [Tooltip("是否把速度倍率应用到 AnimalBase.MoveSpeed（真实影响个体移动速度）")]
    [SerializeField] private bool _applySpeedToAnimal = true;

    private float _speed;
    private float _perception;
    private float _attack;

    public float Speed => _speed;
    public float Perception => _perception;
    public float Attack => _attack;

    /// <summary>归一化强度分 0~1：三倍率加权后映射到配置区间的相对位置，越高越强。</summary>
    public float Strength { get; private set; }

    private void Awake()
    {
        _speed = Roll(_speedRange);
        _perception = Roll(_perceptionRange);
        _attack = Roll(_attackRange);

        float raw = _speed * _speedWeight + _perception * _perceptionWeight + _attack * _attackWeight;
        float min = _speedRange.x * _speedWeight + _perceptionRange.x * _perceptionWeight + _attackRange.x * _attackWeight;
        float max = _speedRange.y * _speedWeight + _perceptionRange.y * _perceptionWeight + _attackRange.y * _attackWeight;
        Strength = Mathf.InverseLerp(min, max, raw);

        if (_applySpeedToAnimal)
        {
            AnimalBase animal = GetComponent<AnimalBase>();
            if (animal != null)
                animal.ApplySpeedMultiplier(_speed);
        }
    }

    private static float Roll(Vector2 range)
    {
        return Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }
}
