using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StickyResin : MonoBehaviour
{
    private Rigidbody2D rbA;
    private FixedJoint2D currentJoint;
    void Start()
    {
        rbA=GetComponent<Rigidbody2D>();
    }
    private List<FixedJoint2D> joints=new List<FixedJoint2D>();
    private List<GameObject> connectObjects=new List<GameObject>();
    private void OnTriggerEnter2D(Collider2D other)
    {
        IConnectable connectable = other.gameObject.GetComponent<IConnectable>();
        if (connectObjects.Contains(other.gameObject))
            return;
        if (connectable != null)
        {
            FixedJoint2D joint=gameObject.AddComponent<FixedJoint2D>();
            joint.connectedBody = other.gameObject.GetComponent<Rigidbody2D>();
            joints.Add(joint);
        }
    }
    private void OnTriggerExit2D(Collider2D other)
    {
        IConnectable connectabale= other.gameObject.GetComponent<IConnectable>();
        if (!connectObjects.Contains(other.gameObject))
            return;
        if (connectabale != null)
        {
            FixedJoint2D jointToRemove=joints.Find(j=>j.connectedBody == other.gameObject.GetComponent<Rigidbody2D>());
            if(jointToRemove != null )
            {
                joints.Remove(jointToRemove);
                Destroy(jointToRemove);
            }
        }
        connectObjects.Remove(other.gameObject);
    }
}
public interface IConnectable
{
    bool IsConnectable {  get; }
}
