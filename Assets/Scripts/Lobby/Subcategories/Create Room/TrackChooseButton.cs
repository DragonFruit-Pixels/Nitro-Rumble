using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TrackChooseButton : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private Button button;
    [SerializeField] private Image buttonImage;
    [SerializeField] private Image trackImage;
    [SerializeField] private TextMeshProUGUI trackName;
    
    private ITrackChooseListener _trackChooseListener;
    public TrackSO Track { get; private set; }

    private bool _selected = false;
    
    public void Init(ITrackChooseListener trackChooseListener, TrackSO track)
    {
        _trackChooseListener = trackChooseListener;
        Track = track;
        
        Setup();
    }

    private void Setup()
    {
        trackImage.sprite = Track.TrackImage;
        trackName.text = Track.TrackName;
    }
    
    private void OnEnable()
    {
        button.onClick.AddListener(OnClick);
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(OnClick);
    }

    private void OnClick() => OnTrackChooseExternal();

    public void OnTrackChooseExternal()
    {
        _trackChooseListener.OnTrackChoose(Track);
        _trackChooseListener.UnselectOthers(this);
        Select(true);
    }

    public void Select(bool selected)
    {
        if (_selected == selected) return;
        
        _selected = selected;
        buttonImage.color = selected ? Color.green : Color.red;
    }
}

public interface ITrackChooseListener
{
    public void OnTrackChoose(TrackSO track);
    public void UnselectOthers(TrackChooseButton selectedButton);
}