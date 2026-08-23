using UnityEngine;

// Fits a background sprite to always cover the orthographic camera view,
// regardless of window aspect ratio. Attach to the same GameObject as SpriteRenderer.
public class MenuBackgroundFitter : MonoBehaviour
{
    private SpriteRenderer _sr;
    private Camera _cam;

    void Awake()
    {
        _sr = GetComponent<SpriteRenderer>();
        if (_sr == null || _sr.sprite == null) return;
        _cam = Camera.main;
        if (_cam == null || !_cam.orthographic) return;
        Fit();
    }

    void Update()
    {
        if (_sr == null || _cam == null) return;
        // Re-fit if camera changed (e.g., resolution change)
        Fit();
    }

    private void Fit()
    {
        Vector2 s = _sr.sprite.bounds.size;
        if (s.x <= 0f || s.y <= 0f) return;
        float camHeight = _cam.orthographicSize * 2f;
        float camWidth = camHeight * _cam.aspect;
        float scale = Mathf.Max(camHeight / s.y, camWidth / s.x) * 1.02f;
        transform.localScale = new Vector3(scale, scale, 1f);
    }
}
