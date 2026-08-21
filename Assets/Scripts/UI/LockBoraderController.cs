using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Boss 攻击红框 UI：监听 BossTelegraph 表现事件，将世界空间攻击盒投影到 Screen Space Overlay Canvas。
/// 注意：类名/文件名保留为 LockBoraderController（既有拼写）。
/// </summary>
public class LockBoraderController : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("Lock 红框 Image")]
    [SerializeField] private Image _lockImage;

    [Tooltip("Lock 红框 RectTransform")]
    [SerializeField] private RectTransform _lockRect;

    [Tooltip("所属 Canvas（当前为 Screen Space Overlay）")]
    [SerializeField] private Canvas _canvas;

    [Header("表现参数")]
    [Tooltip("红框相对攻击盒每侧的世界空间视觉外扩量")]
    [SerializeField] private float _worldPadding = 0.25f;

    [Tooltip("闪烁加速持续时间（秒），默认约 0.5s")]
    [SerializeField] private float _blinkAccelerationDuration = 0.5f;

    [Tooltip("初始闪烁间隔（秒）")]
    [SerializeField] private float _initialBlinkInterval = 0.2f;

    [Tooltip("最终闪烁间隔（秒）")]
    [SerializeField] private float _finalBlinkInterval = 0.05f;

    private BossTelegraph _telegraph;
    private Camera _camera;

    private Transform _followTarget;
    private Vector2 _boxSize;
    private Vector3 _lockedWorldPosition;
    private bool _isLocked;
    private Coroutine _blinkCoroutine;
    private bool _warningLogged;

    private void OnEnable()
    {
        if (_telegraph == null)
            _telegraph = FindObjectOfType<BossTelegraph>(true);

        if (_camera == null)
            _camera = Camera.main;

        if (_telegraph != null)
        {
            _telegraph.OnFollowShown += HandleFollowShown;
            _telegraph.OnLockShown += HandleLockShown;
            _telegraph.OnHidden += HandleHidden;
        }
        else if (!_warningLogged)
        {
            Debug.LogWarning("[LockBoraderController] 未找到 BossTelegraph，攻击红框 UI 无法接收预警事件。", this);
            _warningLogged = true;
        }

        if (_camera == null && !_warningLogged)
        {
            Debug.LogWarning("[LockBoraderController] 未找到主摄像机，攻击红框无法进行世界坐标换算。", this);
            _warningLogged = true;
        }
    }

    private void OnDisable()
    {
        UnsubscribeTelegraph();
        StopBlink();
        _followTarget = null;
        _isLocked = false;

        if (_lockImage != null)
            _lockImage.enabled = false;
    }

    private void OnDestroy()
    {
        UnsubscribeTelegraph();
        StopBlink();
    }

    private void Update()
    {
        if (_followTarget != null)
        {
            ApplyWorldRect(_followTarget.position, _boxSize);
        }
        else if (_isLocked)
        {
            ApplyWorldRect(_lockedWorldPosition, _boxSize);
        }
    }

    private void UnsubscribeTelegraph()
    {
        if (_telegraph == null)
            return;

        _telegraph.OnFollowShown -= HandleFollowShown;
        _telegraph.OnLockShown -= HandleLockShown;
        _telegraph.OnHidden -= HandleHidden;
        _telegraph = null;
    }

    private void HandleFollowShown(Transform target, Vector2 boxSize)
    {
        StopBlink();
        _isLocked = false;
        _followTarget = target;
        _boxSize = boxSize;
        ShowImage();

        if (target != null)
            ApplyWorldRect(target.position, _boxSize);
    }

    private void HandleLockShown(Vector2 position, Vector2 boxSize)
    {
        StopBlink();
        _followTarget = null;
        _lockedWorldPosition = position;
        _boxSize = boxSize;
        _isLocked = true;
        ShowImage();
        ApplyWorldRect(_lockedWorldPosition, _boxSize);

        _blinkCoroutine = StartCoroutine(BlinkRoutine());
    }

    private void HandleHidden()
    {
        StopBlink();
        _followTarget = null;
        _isLocked = false;

        if (_lockImage != null)
            _lockImage.enabled = false;
    }

    private void ShowImage()
    {
        if (_lockImage != null)
            _lockImage.enabled = true;
    }

    private void StopBlink()
    {
        if (_blinkCoroutine != null)
        {
            StopCoroutine(_blinkCoroutine);
            _blinkCoroutine = null;
        }
    }

    private void ApplyWorldRect(Vector3 worldCenter, Vector2 worldBoxSize)
    {
        if (_lockRect == null || _canvas == null || _camera == null)
            return;

        Vector3 centerScreen = _camera.WorldToScreenPoint(worldCenter);
        if (centerScreen.z < 0f)
            return;

        Vector3 rightWorld = worldCenter + _camera.transform.right * (worldBoxSize.x * 0.5f + _worldPadding);
        Vector3 upWorld = worldCenter + _camera.transform.up * (worldBoxSize.y * 0.5f + _worldPadding);

        Vector3 rightScreen = _camera.WorldToScreenPoint(rightWorld);
        Vector3 upScreen = _camera.WorldToScreenPoint(upWorld);

        float screenWidth = Mathf.Abs(rightScreen.x - centerScreen.x) * 2f;
        float screenHeight = Mathf.Abs(upScreen.y - centerScreen.y) * 2f;

        RectTransform canvasRect = _canvas.GetComponent<RectTransform>();
        if (canvasRect == null)
            return;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, centerScreen, null, out Vector2 localPoint))
            return;

        // 子物体锚点参考点：把 Canvas 局部坐标转换为当前 Lock RectTransform 的 anchoredPosition。
        Vector2 anchorReference = new Vector2(
            Mathf.Lerp(canvasRect.rect.xMin, canvasRect.rect.xMax, _lockRect.anchorMin.x),
            Mathf.Lerp(canvasRect.rect.yMin, canvasRect.rect.yMax, _lockRect.anchorMin.y)
        );

        float scale = _canvas.scaleFactor > 0.001f ? _canvas.scaleFactor : 1f;

        _lockRect.anchoredPosition = localPoint - anchorReference;
        _lockRect.sizeDelta = new Vector2(screenWidth / scale, screenHeight / scale);
    }

    private IEnumerator BlinkRoutine()
    {
        float elapsed = 0f;

        while (_isLocked)
        {
            float interval = GetBlinkInterval(elapsed);

            if (_lockImage != null)
                _lockImage.enabled = true;
            yield return new WaitForSeconds(interval);

            if (!_isLocked)
                yield break;

            if (_lockImage != null)
                _lockImage.enabled = false;
            yield return new WaitForSeconds(interval);

            if (!_isLocked)
                yield break;

            elapsed += interval * 2f;
        }
    }

    private float GetBlinkInterval(float elapsed)
    {
        float t = _blinkAccelerationDuration > 0f
            ? Mathf.Clamp01(elapsed / _blinkAccelerationDuration)
            : 1f;

        return Mathf.Max(0.01f, Mathf.Lerp(_initialBlinkInterval, _finalBlinkInterval, t));
    }
}
