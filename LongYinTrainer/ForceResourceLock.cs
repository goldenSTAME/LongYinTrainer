using System;
using UnityEngine.UI;

public static partial class TrainerBehaviour
{
    private static int _lockedForceId = int.MinValue;
    private static IntPtr _lockedWorld, _lockedForcePointer;
    private static int _lockedMoney;
    private static float[] _lockedResources = Array.Empty<float>();
    private static Text? _forceLockButtonText;
    private static int _forceLockPageId;

    private static bool IsForceLocked(ForceData? force) => force != null && force.forceID == _lockedForceId && force.Pointer == _lockedForcePointer;

    private static void ToggleForceLock(ForceData? force)
    {
        try
        {
            if (IsForceLocked(force)) { ClearForceLock(); _status="资源锁定已解除。"; return; }
            var world=GameController.Instance?.worldData;
            if(force==null || world==null || world.GetForce(force.forceID)?.Pointer!=force.Pointer)
                throw new InvalidOperationException("势力已改变，请重新打开势力页面。");
            CaptureForceLock(force);
            _lockedWorld=world.Pointer;
            _lockedForcePointer=force.Pointer;
            _lockedForceId=force.forceID;
            _status=$"已锁定 {force.forceName} 的金钱和 {_lockedResources.Length} 项资源。";
            RefreshForceLockLabel();
        }
        catch(Exception ex) { _status="锁定失败："+ex.Message; }
    }

    private static void CaptureForceLock(ForceData force)
    {
        var values=new float[force.resourceStore?.Count ?? 0];
        var money=force.forceStorage?.money ?? 0;
        if(money<0) throw new InvalidOperationException("金钱不能为负数。");
        for(var i=0;i<values.Length;i++) {
            values[i]=force.resourceStore![i];
            if(!float.IsFinite(values[i]) || values[i]<0) throw new InvalidOperationException("资源必须是非负有限数值。");
        }
        _lockedMoney=money; _lockedResources=values;
    }

    private static void RefreshForceLockLabel()
    {
        if(_forceLockButtonText!=null) _forceLockButtonText.text=_lockedForceId==_forceLockPageId ? "解除资源锁定" : "锁定当前资源";
    }
    private static void ClearForceLock()
    {
        _lockedForceId=int.MinValue; _lockedWorld=IntPtr.Zero; _lockedForcePointer=IntPtr.Zero;
        _lockedResources=Array.Empty<float>(); RefreshForceLockLabel();
    }
    public static void EnforceForceLock(ForceData? changedForce=null)
    {
        if(_lockedForceId==int.MinValue) return;
        try {
            var world=GameController.Instance?.worldData;
            if(world==null || world.Pointer!=_lockedWorld) { ClearForceLock(); return; }
            if(changedForce!=null && changedForce.forceID!=_lockedForceId) return;
            var force=world.GetForce(_lockedForceId);
            if(force==null || force.Pointer!=_lockedForcePointer || (force.resourceStore?.Count ?? 0)!=_lockedResources.Length) { ClearForceLock(); return; }
            var dirty=false;
            if(force.forceStorage!=null && force.forceStorage.money!=_lockedMoney) { force.forceStorage.money=_lockedMoney; dirty=true; }
            for(var i=0;i<_lockedResources.Length;i++) if(force.resourceStore![i]!=_lockedResources[i]) { force.resourceStore[i]=_lockedResources[i]; dirty=true; }
            if(dirty) force.forceDetailDirty=true;
        }
        catch(Exception ex) { ClearForceLock(); LongYinTrainerPlugin.Logger.LogWarning("Resource lock released: "+ex.Message); }
    }
}
