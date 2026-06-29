using System;
using Extensions;
using UnityEngine;

public class LookAtTransform : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private bool flip;
    
    private void Update()
    {
        Vector3 targetPos = flip ? 2f * transform.position - target.position : target.position;
        targetPos.y = transform.position.y;
        
        transform.LookAt(targetPos);
    } 
}