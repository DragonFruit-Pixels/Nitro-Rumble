using System;
using UnityEngine;

public class FollowTransform : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset;
    [SerializeField] private float speed = 10f;
    
    private void Awake()
    {
        Logger.Assert(target != null, "[FollowTransform] Target is null!");
    }

    private void Update()
    {
        if (target != null)
            FollowTarget();
    }

    private void FollowTarget()
    {
        var distance = (target.transform.position + offset) - transform.position;

        transform.position += distance * Time.deltaTime * speed;
    }
}
