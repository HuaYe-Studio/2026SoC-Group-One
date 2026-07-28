using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Signboard : MonoBehaviour
{
    [Header("UI Hint")]
    [Tooltip(" UI Canvas Panel")]
    public GameObject uiHint;

    private void Start()
    {
        if (uiHint != null)
        {
            uiHint.SetActive(false);
        }
    }
    private void OnTriggerEnter2D(Collider2D other)
    {
        Debug.Log("111" + other.name);
        if (other.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            if (uiHint != null)
            {
                uiHint.SetActive(true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Player"))
        {
            if (uiHint != null)
            {
                uiHint.SetActive(false);
            }
        }
    }
}