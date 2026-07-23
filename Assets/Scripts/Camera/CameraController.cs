using Cinemachine;
using UnityEngine;

public class CameraController : MonoBehaviour
{
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float minZoom = 2f;
    [SerializeField] private float maxZoom = 8f;

    private CinemachineVirtualCamera vcam;

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
        float scroll = PlayerInputReader.Instance.ScrollValue;
        if (Mathf.Abs(scroll) < 0.01f) return;

        var lens = vcam.m_Lens;
        lens.OrthographicSize = Mathf.Clamp(
            lens.OrthographicSize - scroll * zoomSpeed * Time.deltaTime,
            minZoom, maxZoom);
        vcam.m_Lens = lens;
    }
}
