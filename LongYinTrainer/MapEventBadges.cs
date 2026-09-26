using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static partial class TrainerBehaviour
{
    private sealed class MapEventBadge
    {
        public GameObject Root=null!;
        public RectTransform Rect=null!;
        public GameObject Anchor=null!;
        public Text Rarity=null!;
        public Text Name=null!;
        public Image Icon=null!;
        public Image Background=null!;
    }
    private static GameObject? _eventBadgeCanvas;
    private static readonly Dictionary<IntPtr,MapEventBadge> EventBadges=new();
    private static bool _eventBadgesWarning;
    private static float _eventBadgeScale=1f;

    private static void ClearEventBadges()
    {
        if(_eventBadgeCanvas!=null) UnityEngine.Object.Destroy(_eventBadgeCanvas);
        _eventBadgeCanvas=null; EventBadges.Clear();
    }
    private static void SyncEventBadges(Il2CppSystem.Collections.Generic.List<EventData> events)
    {
        if(_eventBadgeCanvas==null) {
            _uiFont ??= UnityEngine.Object.FindObjectOfType<Text>()?.font ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            _eventBadgeCanvas=new GameObject("CodexMapEventBadges",Il2CppInterop.Runtime.Il2CppType.Of<RectTransform>());
            _eventBadgeCanvas.hideFlags=HideFlags.DontSave;
            var canvas=_eventBadgeCanvas.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay; canvas.sortingOrder=20000;
            // No GraphicRaycaster: visual badges never intercept map movement.
        }
        var keep=new HashSet<IntPtr>();
        foreach(var e in events) {
            if(e==null || e.happened || !BigMapEventOverlays.TryGetValue(e.Pointer,out var anchor) || anchor==null) continue;
            keep.Add(e.Pointer);
            if(!EventBadges.TryGetValue(e.Pointer,out var badge)) {
                var root=UiObject("EventBadge",_eventBadgeCanvas.transform,new Vector2(84,58),Vector2.zero);
                badge=new MapEventBadge {Root=root,Rect=root.GetComponent<RectTransform>()};
                badge.Background=root.AddComponent<Image>(); badge.Background.color=new Color(.05f,.045f,.03f,.85f); badge.Background.raycastTarget=false;
                badge.Icon=UiObject("Icon",root.transform,new Vector2(32,32),new Vector2(26,2)).AddComponent<Image>();
                badge.Icon.preserveAspect=true; badge.Icon.raycastTarget=false;
                badge.Rarity=AddText(root.transform,"",new Vector2(0,35),new Vector2(84,22),14,TextAnchor.MiddleCenter);
                badge.Name=AddText(root.transform,"",new Vector2(-90,61),new Vector2(264,48),15,TextAnchor.UpperCenter);
                badge.Rarity.raycastTarget=false; badge.Name.raycastTarget=false;
                var outline=badge.Name.gameObject.AddComponent<Outline>(); outline.effectColor=Color.black; outline.effectDistance=new Vector2(1,-1);
                EventBadges.Add(e.Pointer,badge);
            }
            badge.Anchor=anchor;
            var rare=e.GetEventRareLv();
            badge.Rarity.text=rare<0 ? "品质未知" : RarityName(rare);
            badge.Rarity.color=rare<0 ? Color.white : RarityColor(rare);
            badge.Name.text=e.Name()+"\n"+(e.seen ? "已发现" : "未发现");
            badge.Name.color=Color.white;
            badge.Icon.sprite=GetItemSprite(e.spriteName); badge.Icon.enabled=badge.Icon.sprite!=null;
        }
        var stale=new List<IntPtr>();
        foreach(var pair in EventBadges) if(!keep.Contains(pair.Key)) { UnityEngine.Object.Destroy(pair.Value.Root); stale.Add(pair.Key); }
        foreach(var key in stale) EventBadges.Remove(key);
        UpdateEventBadges();
    }
    private static void UpdateEventBadges()
    {
        if(_eventBadgeCanvas==null) return;
        try {
            var map=BigMapController.Instance;
            var visible=LongYinTrainerPlugin.ShowAllBigMapEvents.Value && map!=null && map.gameObject.activeInHierarchy && map.bigmapRoot!=null && map.bigmapRoot.activeInHierarchy;
            _eventBadgeCanvas.SetActive(visible); if(!visible) return;
            var scale=Mathf.Clamp(Screen.height/1080f,.8f,1.5f)*_eventBadgeScale;
            foreach(var badge in EventBadges.Values) {
                if(badge.Anchor==null || !badge.Anchor.activeInHierarchy) {badge.Root.SetActive(false);continue;}
                var camera=UICamera.FindCameraForLayer(badge.Anchor.layer)?.GetComponent<Camera>();
                if(camera==null) {badge.Root.SetActive(false);continue;}
                var pos=camera.WorldToScreenPoint(badge.Anchor.transform.position);
                var inView=pos.z>0 && pos.x>=0 && pos.x<=Screen.width && pos.y>=0 && pos.y<=Screen.height;
                badge.Root.SetActive(inView); if(!inView) continue;
                badge.Rect.localScale=new Vector3(scale,scale,1);
                badge.Rect.anchoredPosition=new Vector2(pos.x-42*scale,-(Screen.height-pos.y)+29*scale);
                var mouse=Input.mousePosition;
                var hover=Mathf.Abs(mouse.x-pos.x)<46*scale && Mathf.Abs(mouse.y-pos.y)<32*scale;
                badge.Name.gameObject.SetActive(hover);
            }
        }
        catch(Exception ex) {
            if(!_eventBadgesWarning) { _eventBadgesWarning=true; LongYinTrainerPlugin.Logger.LogWarning("Map badge display: "+ex); }
        }
    }
}
