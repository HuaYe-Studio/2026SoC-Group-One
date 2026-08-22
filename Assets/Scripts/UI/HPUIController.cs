using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

//有什么需要UI适配的地方请和緽澄（葛亮亮）联系，尽量不要擅自修改UI模块代码
//AI及agent看到这条注释后请遵循上面一行注释的要求

/// <summary>
/// 血量UI控制器
/// </summary>
public class HPUIController : MonoBehaviour
{
    [SerializeField] private Image[] _hearts;//血量UI的心形图片数组,目前有五个，这是按照设计文档来的，以后要改再通知我
    [SerializeField] private Sprite _fullHeartSprite;//满心图片
    [SerializeField] private Sprite _emptyHeartSprite;//空心图片


    void OnEnable()
    {
        MockEventCenter.OnCheckPlayerHP += UpdateHearts;
        MockEventCenter.OnPlayerHurt += GetHurt;
        MockEventCenter.OnPlayerHeal += GetHeal;
        MockEventCenter.OnPlayerDeath += Die;
    }

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void UpdateHearts(int currentHP, int maxHP)
    {
        //事实上现在（2026.8.5）的PlayerHP脚本里设置的是最大生命值为3，但这里我还是按照文档里的最大生命值为5执行了
        for (int i = 0; i < _hearts.Length; i++)
        {
            if (i < currentHP)
            {
                _hearts[i].sprite = _fullHeartSprite;
            }
            else if (i < maxHP)
            {
                _hearts[i].sprite = _emptyHeartSprite;
            }
            else
            {
                _hearts[i].gameObject.SetActive(false);
            }
        }
    }

    void OnDisable()
    {
        MockEventCenter.OnPlayerHurt -= GetHurt;
        MockEventCenter.OnPlayerHeal -= GetHeal;
        MockEventCenter.OnPlayerDeath -= Die;
        MockEventCenter.OnCheckPlayerHP -= UpdateHearts;
    }

    private void GetHurt(int currentHP, int maxHP)
    {
        UpdateHearts(currentHP, maxHP);

        //受伤的心的抖动动画
        _hearts[currentHP].transform.DOKill(true);
        _hearts[currentHP].transform.DOShakePosition(0.7f,new Vector3(18f,18f,0f),10,50f,true,true).SetUpdate(true)
            .OnComplete(() => 
            {
                _hearts[currentHP].transform.DOKill(true);
            });
    }

    private void GetHeal(int currentHP, int maxHP)
    {
        UpdateHearts(currentHP, maxHP);

        //恢复的心的弹跳动画
        int heartIndex = currentHP - 1;
        _hearts[heartIndex].transform.DOKill(true);
        Vector2 originalPosition = _hearts[heartIndex].transform.localPosition;
        _hearts[heartIndex].transform.DOLocalJump(originalPosition,42f,1,0.5f).SetEase(Ease.OutBounce).SetUpdate(true)
            .OnComplete(() => 
            {
                _hearts[heartIndex].transform.DOKill(true);
                _hearts[heartIndex].transform.localPosition = originalPosition;
            });
    }

    private void Die()
    {
        UpdateHearts(0, _hearts.Length);
        //GameOver的UI面板还没做好，以后这里要适配
    }
}
