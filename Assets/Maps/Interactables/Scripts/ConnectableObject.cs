using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConnectableObject : MonoBehaviour,IConnectable
{
    [SerializeField] private bool isconnectable = true;
    public bool IsConnectable => isconnectable;
}
