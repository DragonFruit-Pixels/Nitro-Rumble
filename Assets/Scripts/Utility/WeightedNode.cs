using UnityEngine;

public class WeightedNode<T>
{
    public T Value;
    public int Weight = 0;
    
    public WeightedNode(T value, int weight)
    {
        Value = value;
        Weight = weight;
    }

    public WeighingResults CompareWeight(WeightedNode<T> otherNode)
    {
        if (Weight == 0 || otherNode.Weight == 0)
            return WeighingResults.Ignore;

        if (Weight > otherNode.Weight) return WeighingResults.Higher;
        if (Weight < otherNode.Weight) return WeighingResults.Lower;

        return WeighingResults.Equal;
    }
}

public enum WeighingResults
{
    Higher,
    Lower,
    Equal,
    Ignore
}
