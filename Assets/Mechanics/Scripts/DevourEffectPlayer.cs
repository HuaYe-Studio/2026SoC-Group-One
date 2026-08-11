using System.Collections;
using UnityEngine;
using DG.Tweening;

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
    private float _originalOrthoSize;
    private Vector3 _originalPosition;

    private void Awake()
    {
        _mainCam = GetComponent<Camera>();
        _originalOrthoSize = _mainCam.orthographicSize;
        _originalPosition = transform.position;
    }

    public IEnumerator PlayZoomIn(Vector3 targetPosition)
    {
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
        transform.DOMove(_originalPosition, zoomOutDuration)
            .SetEase(Ease.InQuad)
            .SetUpdate(true);

        if (_mainCam != null)
        {
            DOTween.To(
                () => _mainCam.orthographicSize,
                sz => _mainCam.orthographicSize = sz,
                _originalOrthoSize,
                zoomOutDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true);
        }

        yield return new WaitForSecondsRealtime(zoomOutDuration);
    }

    public void ResetAll()
    {
        DOTween.Kill(transform);
        if (_mainCam != null)
        {
            DOTween.Kill(_mainCam);
            _mainCam.orthographicSize = _originalOrthoSize;
        }
        transform.position = _originalPosition;
    }
}
