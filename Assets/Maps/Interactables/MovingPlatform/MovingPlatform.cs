using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    public Transform PosStart, PosEnd;
    [SerializeField] float movespeed;
    Transform targetPos;
    void Start()
    {
        targetPos = PosEnd;
    }
    void Update()
    {
        if (Vector2.Distance(transform.position, PosStart.position) < 0.1f) targetPos = PosEnd;
        if (Vector2.Distance(transform.position, PosEnd.position) < 0.1f) targetPos = PosStart;
        transform.position = Vector2.MoveTowards(transform.position, targetPos.position, movespeed * Time.deltaTime);
    }
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(collision.gameObject.layer==LayerMask.NameToLayer("Player") || collision.gameObject.layer == LayerMask.NameToLayer("Animal"))  
        {
            collision.transform.parent = this.transform;
        }
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.layer == LayerMask.NameToLayer("Player") || collision.gameObject.layer == LayerMask.NameToLayer("Animal"))
        {
            collision.transform.parent = null;
        }
    }
}
