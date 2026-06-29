using System.Collections;
using UnityEngine;

[CreateAssetMenu(fileName = "Car Skin", menuName = "Car Customization/Car Skin")]
public class CarSkinSO : ScriptableObject
{
    public int skinID = -1;
    public string SkinName = "";
    public Mesh SkinMesh;
    public Sprite SkinPreview;
}