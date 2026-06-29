using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CarChooseButton : MonoBehaviour
{
    [SerializeField] private Button _chooseButton;
    [SerializeField] private Image _chooseButtonImage;
    [SerializeField] private TMP_Text _chooseButtonText;
    
    private ICarChooserListener _carChooserListener;
    private CarSkinSO _skin;
    
    public void Init(ICarChooserListener carChooserListener, CarSkinSO skin)
    {
        _carChooserListener = carChooserListener;
        _skin = skin;

        _chooseButtonImage.sprite = _skin.SkinPreview;
        _chooseButtonText.text = _skin.SkinName;
    }

    private void OnEnable()
    {
        _chooseButton.onClick.AddListener(OnClick);
    }
    
    private void OnDisable()
    {
        _chooseButton.onClick.RemoveListener(OnClick);
    }

    private void OnClick()
    {
        _carChooserListener?.ChangeCarSkin(_skin);
    }
}