using System.Collections;
using UnityEngine;

public class DisappearingPlatform : MonoBehaviour
{

    public float delayBeforeDisappear = 0.5f;

    public float delayBeforeReappear = 0.5f;
    
    public SpriteRenderer spriteRenderer;
    public Collider2D platformCollider; 

    private bool _isTriggered;

    private void Start()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (platformCollider == null) platformCollider = GetComponent<Collider2D>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!_isTriggered && collision.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            StartCoroutine(DisappearAndReappearRoutine());
        }
    }

    private IEnumerator DisappearAndReappearRoutine()
    {
        _isTriggered = true;

        yield return new WaitForSeconds(delayBeforeDisappear);

        SetPlatformState(false);

        yield return new WaitForSeconds(delayBeforeReappear);

        SetPlatformState(true);

        _isTriggered = false; 
    }

    private void SetPlatformState(bool active)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = active;
        }

        if (platformCollider != null)
        {
            platformCollider.enabled = active;
        }
    }
}