using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StickyResin : MonoBehaviour
{
    private Rigidbody2D rbA;
    private List<FixedJoint2D> joints = new List<FixedJoint2D>();
    private List<GameObject> connectObjects = new List<GameObject>();
    public float WaitTime = 0.5f;

    // 存储正在延迟连接的对象，防止重复触发
    private List<GameObject> pendingConnections = new List<GameObject>();

    void Start()
    {
        rbA = GetComponent<Rigidbody2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 如果已经连接过了，或者正在等待连接，则忽略
        if (connectObjects.Contains(other.gameObject) || pendingConnections.Contains(other.gameObject))
            return;

        IConnectable connectable = other.gameObject.GetComponent<IConnectable>();
        if (connectable != null && connectable.IsConnectable)
        {
            StartCoroutine(DelayedConnect(other.gameObject));
        }
    }

    private IEnumerator DelayedConnect(GameObject target)
    {
        // 添加到等待列表
        pendingConnections.Add(target);

        // 等待0.5秒
        yield return new WaitForSeconds(WaitTime);

        // 从等待列表移除
        pendingConnections.Remove(target);

        // 再次检查目标是否还存在，以及是否已被连接（防止在延迟期间被其他逻辑处理）
        if (target == null || connectObjects.Contains(target))
            yield break;

        // 获取目标的刚体
        Rigidbody2D targetRb = target.GetComponent<Rigidbody2D>();
        if (targetRb == null)
            yield break;

        // 创建固定关节
        FixedJoint2D joint = gameObject.AddComponent<FixedJoint2D>();
        joint.connectedBody = targetRb;

        // 添加到列表
        joints.Add(joint);
        connectObjects.Add(target);

        Debug.Log($"已连接到: {target.name}");
    }

    /*private void OnTriggerExit2D(Collider2D other)
    {
        // 如果正在等待连接，取消等待并移除
        if (pendingConnections.Contains(other.gameObject))
        {
            StopCoroutine(DelayedConnect(other.gameObject));
            pendingConnections.Remove(other.gameObject);
            return;
        }

        // 如果已经连接，断开关节
        if (connectObjects.Contains(other.gameObject))
        {
            IConnectable connectable = other.gameObject.GetComponent<IConnectable>();
            if (connectable != null)
            {
                Rigidbody2D targetRb = other.gameObject.GetComponent<Rigidbody2D>();
                FixedJoint2D jointToRemove = joints.Find(j => j.connectedBody == targetRb);
                if (jointToRemove != null)
                {
                    joints.Remove(jointToRemove);
                    Destroy(jointToRemove);
                }
            }
            connectObjects.Remove(other.gameObject);
        }
    }*/

    public void OnDestroy()
    {
        // 取消所有等待中的连接
        foreach (GameObject pending in pendingConnections)
        {
            StopCoroutine(DelayedConnect(pending));
        }
        pendingConnections.Clear();

        // 销毁所有关节
        foreach (FixedJoint2D joint in joints)
        {
            if (joint != null)
            {
                Destroy(joint);
            }
        }
        joints.Clear();
        connectObjects.Clear();
    }
}

public interface IConnectable
{
    bool IsConnectable { get; }
}