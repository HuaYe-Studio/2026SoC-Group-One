using System.Collections.Generic;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private BaseForm[] allForms;
    [SerializeField] private int activeFormIndex;

    private readonly Dictionary<FormType, int> _formTypeToIndex = new Dictionary<FormType, int>();
    private readonly HashSet<FormType> _unlockedForms = new HashSet<FormType>();
    private readonly HashSet<WaterZone> _waterZones = new HashSet<WaterZone>();

    public bool IsInWater => _waterZones.Count > 0;
    public bool IsSubmerged
    {
        get
        {
            foreach (WaterZone zone in _waterZones)
                if (zone != null && zone.IsDeep)
                    return true;
            return false;
        }
    }

    public void EnterWater(WaterZone zone) => _waterZones.Add(zone);
    public void ExitWater(WaterZone zone) => _waterZones.Remove(zone);

    private readonly HashSet<IceSurface> _iceZones = new HashSet<IceSurface>();
    public bool IsOnIce => _iceZones.Count > 0;
    public void EnterIce(IceSurface zone) => _iceZones.Add(zone);
    public void ExitIce(IceSurface zone) => _iceZones.Remove(zone);

    public BaseForm ActiveForm => (allForms != null && activeFormIndex < allForms.Length)
        ? allForms[activeFormIndex]
        : null;

    private void Awake()
    {
        InitializeForms();
        ActivateDefaultForm();
    }

    private void InitializeForms()
    {
        if (allForms == null) return;

        for (int i = 0; i < allForms.Length; i++)
        {
            if (allForms[i] == null) continue;

            allForms[i].Initialize(this);
            allForms[i].gameObject.SetActive(false);

            FormType formType = ResolveFormType(allForms[i], i);
            _formTypeToIndex[formType] = i;
        }

        _unlockedForms.Add(FormType.Slime);
    }

    private FormType ResolveFormType(BaseForm form, int index)
    {
        FormType declared = form.FormType;
        if (!_formTypeToIndex.ContainsKey(declared))
            return declared;

        if (form is SlimeForm) return FormType.Slime;
        if (form is FrogForm) return FormType.Frog;
        return (FormType)index;
    }

    private void ActivateDefaultForm()
    {
        if (allForms == null || allForms.Length == 0) return;

        activeFormIndex = Mathf.Clamp(activeFormIndex, 0, allForms.Length - 1);

        if (allForms[activeFormIndex] != null)
        {
            allForms[activeFormIndex].gameObject.SetActive(true);
            allForms[activeFormIndex].OnFormActivated();
        }
    }

    private void OnEnable()
    {
        MockEventCenter.OnFormUnlocked += AddNewForm;
        MockEventCenter.OnPlayerDeath += OnPlayerDeathHandler;
        MockEventCenter.OnPlayerRespawn += OnPlayerRespawnHandler;
    }

    private void OnDisable()
    {
        MockEventCenter.OnFormUnlocked -= AddNewForm;
        MockEventCenter.OnPlayerDeath -= OnPlayerDeathHandler;
        MockEventCenter.OnPlayerRespawn -= OnPlayerRespawnHandler;
    }

    private void OnPlayerDeathHandler() => ActiveForm?.Die();

    private void OnPlayerRespawnHandler()
    {
        if (allForms == null) return;
        foreach (BaseForm form in allForms)
            if (form != null) form.Revive();
    }

    private void AddNewForm(FormType formType)
    {
        if (_formTypeToIndex.ContainsKey(formType))
        {
            _unlockedForms.Add(formType);
        }
    }

    public bool IsFormUnlocked(FormType formType)
    {
        return _unlockedForms.Contains(formType);
    }

    public void SwitchToFormByType(FormType formType)
    {
        if (_formTypeToIndex.TryGetValue(formType, out int index))
            SwitchToForm(index);
    }

    public FormType GetCurrentForm() =>
        ActiveForm != null ? ActiveForm.FormType : FormType.Slime;

    public List<FormType> GetUnlockedForms() => new List<FormType>(_unlockedForms);

    public void RestoreForms(FormType current, List<FormType> unlocked)
    {
        _unlockedForms.Clear();
        _unlockedForms.Add(FormType.Slime);
        if (unlocked != null)
            foreach (var f in unlocked)
                _unlockedForms.Add(f);
        _unlockedForms.Add(current);
        SwitchToFormByType(current);
    }

    public BaseForm GetForm(FormType type)
    {
        if (_formTypeToIndex.TryGetValue(type, out int index) && allForms != null && index < allForms.Length)
            return allForms[index];
        return null;
    }

    private void Update()
    {
        if (!PlayerInputReader.HasInstance) return;
        float horizontal = PlayerInputReader.Instance.MoveValue.x;

        if (ActiveForm != null)
            ActiveForm.ProcessInput(horizontal);

    }

    private void SwitchToForm(int index)
    {
        if (index < 0 || index >= allForms.Length) return;
        if (index == activeFormIndex) return;
        if (allForms[index] == null) return;
        if (!_unlockedForms.Contains(allForms[index].FormType)) return;

        if (ActiveForm != null)
        {
            ActiveForm.OnFormDeactivated();
            ActiveForm.gameObject.SetActive(false);
        }

        activeFormIndex = index;
        allForms[index].gameObject.SetActive(true);
        allForms[index].OnFormActivated();

        MockEventCenter.TriggerFormChange(allForms[index].FormType);
    }
}
