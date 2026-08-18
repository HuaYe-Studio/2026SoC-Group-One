using System.Collections;
using Cinemachine;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float minZoom = 2f;
    [SerializeField] private float maxZoom = 8f;

    [Tooltip("BOSS 战期间是否锁定玩家滚轮缩放（EnterBossArena 时锁定，ExitBossArena 恢复）")]
    [SerializeField] private bool _lockZoomDuringBossFight = true;

    private CinemachineVirtualCamera vcam;
    private bool _zoomLocked;
    private Coroutine _zoomRoutine;

    private void Awake()
    {
        vcam = GetComponent<CinemachineVirtualCamera>();
    }

    private void Update()
    {
        if (!PlayerInputReader.HasInstance) return;
        HandleZoom();
    }

    private void HandleZoom()
    {
        if (_zoomLocked) return;

        float scroll = PlayerInputReader.Instance.ScrollValue;
        if (Mathf.Abs(scroll) < 0.01f) return;

        var lens = vcam.m_Lens;
        lens.OrthographicSize = Mathf.Clamp(
            lens.OrthographicSize - scroll * zoomSpeed * Time.deltaTime,
            minZoom, maxZoom);
        vcam.m_Lens = lens;
    }

    /// <summary>
    /// 进入 BOSS 场地：平滑缩放到目标正交尺寸（默认 5 → 6.5 拉远看清全场）。
    /// 由 BossController.EnterArena() 调用；锁定期间玩家滚轮缩放失效。
    /// </summary>
    public void EnterBossArena(float targetSize)
    {
        _zoomLocked = _lockZoomDuringBossFight;
        StartArenaZoom(targetSize);
    }

    /// <summary>
    /// 退出 BOSS 场地：平滑缩回常规尺寸并解除缩放锁定。
    /// 由 BossController 退去时调用。
    /// </summary>
    public void ExitBossArena(float targetSize)
    {
        _zoomLocked = false;
        StartArenaZoom(targetSize);
    }

    private void StartArenaZoom(float targetSize)
    {
        if (vcam == null) return;

        if (_zoomRoutine != null)
            StopCoroutine(_zoomRoutine);
        _zoomRoutine = StartCoroutine(SmoothZoomTo(targetSize));
    }

    private IEnumerator SmoothZoomTo(float targetSize)
    {
        targetSize = Mathf.Clamp(targetSize, minZoom, maxZoom);

        var lens = vcam.m_Lens;
        while (Mathf.Abs(lens.OrthographicSize - targetSize) > 0.01f)
        {
            lens.OrthographicSize = Mathf.MoveTowards(
                lens.OrthographicSize, targetSize, zoomSpeed * Time.deltaTime);
            vcam.m_Lens = lens;
            yield return null;
        }

        lens.OrthographicSize = targetSize;
        vcam.m_Lens = lens;
        _zoomRoutine = null;
    }
}
