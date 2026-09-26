using System;
using UnityEngine;
public static partial class TrainerBehaviour
{
    private static SphereCollider? _nativeVisionCollider;
    private static float _nativeVisionRadius;
    private static IntPtr _nativeVisionWorld;
    private static bool _nativeVisionLogged;
    private static void RestoreNativeVision()
    {
        if(_nativeVisionCollider!=null) _nativeVisionCollider.radius=_nativeVisionRadius;
        _nativeVisionCollider=null; _nativeVisionWorld=IntPtr.Zero; _nativeVisionLogged=false;
    }
    public static void ApplyNativeFullVision(BigmapNpcController controller)
    {
        try {
            if(!LongYinTrainerPlugin.ShowAllBigMapEvents.Value) { RestoreNativeVision(); return; }
            var player=Player;
            if(controller==null || player==null || controller.heroData?.Pointer!=player.Pointer) return;
            var collider=controller.seeRangeCollider;
            var world=GameController.Instance?.worldData;
            if(collider==null || world==null) return;
            if(_nativeVisionCollider!=collider || _nativeVisionWorld!=world.Pointer) {
                RestoreNativeVision(); _nativeVisionCollider=collider; _nativeVisionRadius=collider.radius; _nativeVisionWorld=world.Pointer;
            }
            // Only enlarge the native sight trigger. Never touch hover or
            // interaction colliders, NPC sight, event callbacks or event flags.
            var scale=collider.transform.lossyScale;
            var maxAxis=Mathf.Max(Mathf.Abs(scale.x),Mathf.Max(Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
            if(maxAxis<.0001f) return;
            collider.radius=Mathf.Max(_nativeVisionRadius,100000f/maxAxis);
            if(!_nativeVisionLogged) {
                _nativeVisionLogged=true;
                LongYinTrainerPlugin.Logger.LogInfo($"Native full vision enabled for player {player.heroID}; original radius={_nativeVisionRadius}, radius={collider.radius}; interaction range unchanged.");
            }
        }
        catch(Exception ex) { LongYinTrainerPlugin.Logger.LogWarning("Native vision: "+ex.Message); }
    }
}
