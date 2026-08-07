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

    private Camera mainCam;
    private float originalOrthoSize;
    private Vector3 originalPosition;

    private void Awake()
    {
        mainCam = GetComponent<Camera>();
        originalOrthoSize = mainCam.orthographicSize;
        originalPosition = transform.position;
    }

    public IEnumerator PlayZoomIn(Vector3 targetPosition)
    {
        Vector3 targetCamPos = new Vector3(targetPosition.x, targetPosition.y, transform.position.z);

        transform.DOMove(targetCamPos, zoomInDuration)
            .SetEase(zoomEase)
            .SetUpdate(true);

        if (mainCam != null)
        {
            DOTween.To(
                () => mainCam.orthographicSize,
                sz => mainCam.orthographicSize = sz,
                closeUpOrthoSize,
                zoomInDuration)
                .SetEase(zoomEase)
                .SetUpdate(true);
        }

        yield return new WaitForSecondsRealtime(zoomInDuration);
    }

    public IEnumerator PlayDevour(IDevourable target)
    {
        if (mainCam != null)
            mainCam.transform.DOShakePosition(shakeDuration, shakeStrength).SetUpdate(true);

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
        transform.DOMove(originalPosition, zoomOutDuration)
            .SetEase(Ease.InQuad)
            .SetUpdate(true);

        if (mainCam != null)
        {
            DOTween.To(
                () => mainCam.orthographicSize,
                sz => mainCam.orthographicSize = sz,
                originalOrthoSize,
                zoomOutDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true);
        }

        yield return new WaitForSecondsRealtime(zoomOutDuration);
    }

    public void ResetAll()
    {
        DOTween.Kill(transform);
        if (mainCam != null)
        {
            DOTween.Kill(mainCam);
            mainCam.orthographicSize = originalOrthoSize;
        }
        transform.position = originalPosition;
    }
}
