using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static partial class TrainerBehaviour
{
    private static GameObject? _loadingOverlay;
    private static Text? _loadingText;
    private static Image? _loadingBar;
    private static int _loadingTab=-1, _loadingFrame, _loadingPhase;
    private static float _loadingStarted;
    private static IEnumerator<object?>? _catalogSteps;
    private static CanvasGroup? _pageFade;
    private static float _fadeStarted;

    private static bool PreparePageLoading()
    {
        if(_loadingOverlay==null || _loadingTab!=_tab) {
            if(_loadingOverlay!=null) UnityEngine.Object.Destroy(_loadingOverlay);
            if(_catalogSteps!=null) { _catalogSteps.Dispose(); _catalogSteps=null; Catalog.Clear(); }
            _loadingTab=_tab; _loadingFrame=Time.frameCount; _loadingPhase=0; _loadingStarted=Time.realtimeSinceStartup;
            _loadingOverlay=UiObject("LoadingPage",_uiContent!.transform,new Vector2(1080,654),Vector2.zero);
            var bg=_loadingOverlay.AddComponent<Image>(); bg.color=new Color(.74f,.72f,.63f,.98f);
            _loadingText=AddText(_loadingOverlay.transform,"正在准备页面…",new Vector2(320,240),new Vector2(440,50),22,TextAnchor.MiddleCenter);
            var track=UiObject("LoadingTrack",_loadingOverlay.transform,new Vector2(280,4),new Vector2(400,308));
            track.AddComponent<Image>().color=new Color(.2f,.17f,.1f,.25f);
            _loadingBar=UiObject("LoadingSweep",track.transform,new Vector2(70,4),Vector2.zero).AddComponent<Image>();
            _loadingBar.color=new Color(.85f,.55f,.12f,1f);
            return false;
        }
        // Give Unity time to display the overlay before starting expensive work.
        if(Time.frameCount<_loadingFrame+2) return false;
        try {
            if(_tab==2 && _loadingPhase==0) {
                _loadingText!.text="正在读取物品目录…";
                if(Catalog.Count==0 && _catalogSteps==null) _catalogSteps=BuildCatalogSteps().GetEnumerator();
                if(_catalogSteps!=null && _catalogSteps.MoveNext()) return false;
                _catalogSteps?.Dispose(); _catalogSteps=null; _loadingPhase=1;
                return false;
            }
            if(_tab==2 && _loadingPhase==1) {
                _loadingText!.text="正在准备游戏图标…";
                if(!_runtimeIconsLoaded) RefreshRuntimeIconIndex();
                _loadingPhase=2; return false;
            }
            return true;
        }
        catch(Exception ex) {
            _catalogSteps?.Dispose(); _catalogSteps=null; Catalog.Clear(); _loadingPhase=2;
            _status="页面准备失败："+ex.Message;
            LongYinTrainerPlugin.Logger.LogError(ex);
            return true;
        }
    }
    private static void AnimatePageLoading()
    {
        if(_loadingBar!=null) {
            var rect=_loadingBar.GetComponent<RectTransform>();
            rect.anchoredPosition=new Vector2(Mathf.PingPong((Time.realtimeSinceStartup-_loadingStarted)*190f,210f),0);
        }
        if(_pageFade!=null) {
            _pageFade.alpha=Mathf.Clamp01((Time.realtimeSinceStartup-_fadeStarted)/.16f);
            if(_pageFade.alpha>=1f) _pageFade=null;
        }
    }
    private static void FinishPageLoading()
    {
        if(_loadingOverlay==null) return;
        UnityEngine.Object.Destroy(_loadingOverlay); _loadingOverlay=null; _loadingText=null; _loadingBar=null; _loadingTab=-1;
        if(PageRoots.TryGetValue(_tab,out var page) && page!=null) {
            _pageFade=page.GetComponent<CanvasGroup>() ?? page.AddComponent<CanvasGroup>();
            _pageFade.alpha=0; _fadeStarted=Time.realtimeSinceStartup;
        }
    }
}
