using System.Collections;
using UnityEngine;
using DG.Tweening;
using Cinemachine;

public class DevourEffectPlayer : MonoBehaviour
{
    [Header("Camera Zoom")]
    [SerializeField] private float zoomInDuration = 0.35f;
    [SerializeField] private float zoomOutDuration = 0.35f;
    [SerializeField] private float closeUpOrthoSize = 3.5f;
    [SerializeField] private Ease zoomEase = Ease.OutQuad;

    [Header("Devour Animation")]
    [SerializeField] private float devourDuration = 1.2f;
    [SerializeField] private Ease devourEase = Ease.InBack;
    [SerializeField] private float shakeStrength = 0.2f;
    [SerializeField] private float shakeDuration = 0.3f;

    private Camera _mainCam;
    private CinemachineBrain _brain;
    private float _returnOrthoSize;
    private Vector3 _returnPosition;

    private void Awake()
    {
        _mainCam = GetComponent<Camera>();
        _brain = _mainCam != null ? _mainCam.GetComponent<CinemachineBrain>() : null;
        _returnOrthoSize = _mainCam.orthographicSize;
        _returnPosition = transform.position;
    }

    public IEnumerator PlayZoomIn(Vector3 targetPosition)
    {
        // Capture the follow position before the brain is disabled and the camera is moved,
        // so PlayZoomOut returns to the player's current spot instead of the scene-start position.
        _returnPosition = transform.position;
        _returnOrthoSize = _mainCam != null ? _mainCam.orthographicSize : _returnOrthoSize;

        if (_brain != null) _brain.enabled = false;

        Vector3 targetCamPos = new Vector3(targetPosition.x, targetPosition.y, transform.position.z);

        transform.DOMove(targetCamPos, zoomInDuration)
            .SetEase(zoomEase)
            .SetUpdate(true);

        if (_mainCam != null)
        {
            DOTween.To(
                () => _mainCam.orthographicSize,
                sz => _mainCam.orthographicSize = sz,
                closeUpOrthoSize,
                zoomInDuration)
                .SetEase(zoomEase)
                .SetUpdate(true);
        }

        yield return new WaitForSecondsRealtime(zoomInDuration);
    }

    public IEnumerator PlayDevour(IDevourable target)
    {
        if (_mainCam != null)
            _mainCam.transform.DOShakePosition(shakeDuration, shakeStrength).SetUpdate(true);

        Transform targetTransform = target.Transform;
        SpriteRenderer sr = target.SpriteRenderer;

        Sequence seq = DOTween.Sequence();
        seq.SetUpdate(true);

        if (targetTransform != null)
        {
            seq.Join(targetTransform.DOScale(Vector3.one * 1.3f, devourDuration * 0.2f)
                .SetEase(Ease.OutQuad));
            seq.Append(targetTransform.DOScale(Vector3.zero, devourDuration * 0.8f)
                .SetEase(devourEase));
        }

        if (sr != null)
            seq.Join(sr.DOFade(0f, devourDuration).SetEase(Ease.InCubic));

        yield return seq.WaitForCompletion();
    }

    public IEnumerator PlayZoomOut()
    {
        transform.DOMove(_returnPosition, zoomOutDuration)
            .SetEase(Ease.InQuad)
            .SetUpdate(true);

        if (_mainCam != null)
        {
            DOTween.To(
                () => _mainCam.orthographicSize,
                sz => _mainCam.orthographicSize = sz,
                _returnOrthoSize,
                zoomOutDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true);
        }

        yield return new WaitForSecondsRealtime(zoomOutDuration);

        if (_brain != null) _brain.enabled = true;
    }

    public void ResetAll()
    {
        DOTween.Kill(transform);
        if (_mainCam != null)
        {
            DOTween.Kill(_mainCam);
            _mainCam.orthographicSize = _returnOrthoSize;
        }
        transform.position = _returnPosition;
        if (_brain != null) _brain.enabled = true;
    }
}
