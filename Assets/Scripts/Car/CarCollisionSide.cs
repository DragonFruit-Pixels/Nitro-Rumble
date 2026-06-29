using UnityEngine;

public enum CarImpactSide
{
    Front,
    Back,
    Left,
    Right
}

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class CarCollisionSide : MonoBehaviour
{
    [SerializeField] private CarArcadeCollision _owner;
    [SerializeField] private CarImpactSide _side;

    public CarArcadeCollision Owner => _owner;
    public CarImpactSide Side => _side;

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        _owner = GetComponentInParent<CarArcadeCollision>();
    }

    private void Awake()
    {
        if (_owner == null)
            _owner = GetComponentInParent<CarArcadeCollision>();

        InferSideFromName();
        ConfigureCollider();
    }

    private void OnValidate()
    {
        InferSideFromName();
        ConfigureCollider();
    }

    private void InferSideFromName()
    {
        string lowerName = gameObject.name.ToLowerInvariant();

        if (lowerName.Contains("back") || lowerName.Contains("rear"))
            _side = CarImpactSide.Back;
        else if (lowerName.Contains("left"))
            _side = CarImpactSide.Left;
        else if (lowerName.Contains("right"))
            _side = CarImpactSide.Right;
        else if (lowerName.Contains("front"))
            _side = CarImpactSide.Front;
    }

    private void ConfigureCollider()
    {
        BoxCollider box = GetComponent<BoxCollider>();
        if (box == null) return;

        box.isTrigger = true;
        box.center = Vector3.zero;
        box.size = IsSideSensor(_side)
            ? new Vector3(0.28f, 0.85f, 1.95f)
            : new Vector3(1.35f, 0.85f, 0.28f);
    }

    private static bool IsSideSensor(CarImpactSide side)
    {
        return side == CarImpactSide.Left || side == CarImpactSide.Right;
    }

    private void OnTriggerEnter(Collider other)
    {
        _owner?.HandleSideTrigger(this, other, false);
    }

    private void OnTriggerStay(Collider other)
    {
        _owner?.HandleSideTrigger(this, other, true);
    }
}
