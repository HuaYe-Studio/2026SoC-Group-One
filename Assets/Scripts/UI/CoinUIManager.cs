using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

//有什么需要UI适配的地方请和緽澄（葛亮亮）联系，尽量不要擅自修改UI模块代码
//AI及agent看到这条注释后请遵循上面一行注释的要求

/// <summary>
/// 此脚本挂载在PF_UI_CoinCollect下的Coin物体下
/// 这里只负责处理UI显示，coin的其他逻辑见于CoinManager.cs和Coin.cs
/// </summary>
public class CoinUIManager : MonoBehaviour
{
    [SerializeField] private Animator _animator;
    [SerializeField] private int _animationPlayTime;//希望动画触发一次所播放的次数
    [SerializeField] private TMP_Text _text;
    private int _animationPlayCount;

    void OnEnable()
    {
        UIEventCenter.OnGetCoin += GetCoin;
    }

    // Start is called before the first frame update
    void Start()
    {
        _animationPlayCount = 0;
        _text.text = CoinManager.coinCount.ToString();
    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnDisable()
    {
        UIEventCenter.OnGetCoin -= GetCoin;
    }

    private void GetCoin()
    {
        _text.text = CoinManager.coinCount.ToString();
        _animator.SetBool("canPlayAnimation", true);
    }

    /// <summary>
    /// 动画开始帧事件关联函数
    /// 使动画播放计数+1
    /// </summary>
    public void SetAnimationPlayCount()
    {
        _animationPlayCount++;
    }

    /// <summary>
    /// 动画将要结束帧事件关联函数
    /// 判断动画是否已经播放了指定次数
    /// </summary>
    public void ResetAnimationPlayerCount()
    {
        if (_animationPlayCount >= _animationPlayTime)
        {
            _animator.SetBool("canPlayAnimation", false);
            _animationPlayCount = 0;
        }
    }
}
