using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TransitionManager : Singleton<TransitionManager>
{
    [Header("Transition Settings")] 
    [SerializeField] private float _baseDuration = 3f;
    [SerializeField] private CanvasGroup _faderCanvasGroup;
    
    public event Action OnTransitionStarted;
    public event Action OnTransitionCompleted;
    
    
    public override void Awake()
    {
        base.Awake();
        DontDestroyOnLoad(transform.root.gameObject);
    }

    public void TransitionToScene(string sceneName)
    {
        StartCoroutine(TransitionToSceneCoroutine(sceneName));
    }

    private IEnumerator TransitionToSceneCoroutine(string sceneName)
    {
        OnTransitionStarted?.Invoke();
        
        var halfDuration = _baseDuration / 2f;
        var currentHalfDuration = halfDuration;
        
        while (currentHalfDuration > 0f)
        {
            currentHalfDuration -= Time.deltaTime;
            
            var visibility = 1 - currentHalfDuration / halfDuration;
            _faderCanvasGroup.alpha = visibility;
            
            yield return null;
        }
        _faderCanvasGroup.alpha = 1f;
        
        yield return SceneManager.LoadSceneAsync(sceneName);
        
        while (currentHalfDuration < halfDuration)
        {
            currentHalfDuration += Time.deltaTime;
            
            var visibility = 1 - currentHalfDuration / halfDuration;
            _faderCanvasGroup.alpha = visibility;
            
            yield return null;
        }
        _faderCanvasGroup.alpha = 0f;
        
        OnTransitionCompleted?.Invoke();
    }
}
