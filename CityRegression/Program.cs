using System;
using System.Collections.Generic;

static class Program
{
    static int count;
    static void Check(bool value,string name) { if(!value) throw new Exception(name); Console.WriteLine("PASS "+name); count++; }
    static void Main()
    {
        var a=new AreaData{areaID=1,Pointer=(IntPtr)11}; var b=new AreaData{areaID=2,Pointer=(IntPtr)12};
        var w=new WorldData{Pointer=(IntPtr)1,Areas=new(){{1,a},{2,b}}};
        var av=new float[]{90,80,70,1000,1500}; var bv=new float[]{50,60,30,2000,3000};
        CityStateLock.Apply(w,a,av,true); CityStateLock.Apply(w,b,bv,true);
        Check(CityStateLock.IsLocked(w,a)&&CityStateLock.IsLocked(w,b),"two independent city locks");
        a.safe=1;b.people=2;CityStateLock.Enforce(w,a);
        Check(a.safe==90&&b.people==2,"targeted enforcement affects only changed city");
        CityStateLock.Enforce(w);Check(b.people==2000&&a.people==1000,"frame enforcement restores both cities");
        av[0]=0;a.safe=1;CityStateLock.Enforce(w);Check(a.safe==90,"snapshot is not aliased to input array");
        CityStateLock.Apply(w,a,new float[]{99,98,97,20,30},false);a.safe=0;CityStateLock.Enforce(w);
        Check(a.safe==99&&b.safe==50,"editing locked city updates only its own snapshot");
        CityStateLock.Unlock(w,a);a.safe=2;CityStateLock.Enforce(w);
        Check(a.safe==2&&CityStateLock.IsLocked(w,b),"unlock one city preserves others and current values");
        var before=CityStateLock.Read(b);
        foreach(var bad in new[]{new float[]{101,50,50,1,2},new float[]{50,float.NaN,50,1,2},new float[]{50,50,50,3,2},new float[]{50,50,50,0,0},new float[]{50,50,50,-1,2}})
        {try{CityStateLock.Apply(w,b,bad,true);throw new Exception("accepted invalid state");}catch(ArgumentException){}}
        Check(CityStateLock.Read(b).SequenceEqual(before),"invalid input causes no partial writes");
        Check(b.areaInfoDirty&&b.areaDetailDirty,"writes invalidate game city displays");
        var replacement=new AreaData{areaID=2,Pointer=(IntPtr)99};w.Areas[2]=replacement;CityStateLock.Enforce(w);
        Check(!CityStateLock.IsLocked(w,replacement)&&replacement.safe==0,"replacement area instance is not overwritten");
        CityStateLock.Apply(w,a,new float[]{20,20,20,20,20},true);
        var other=new WorldData{Pointer=(IntPtr)2,Areas=new(){{1,a}}};CityStateLock.Enforce(other);
        Check(!CityStateLock.IsLocked(other,a),"different save releases all locks even when IDs match");
        CityStateLock.Apply(other,a,new float[]{30,30,30,30,30},true);CityStateLock.Clear();a.safe=9;CityStateLock.Enforce(other);
        Check(a.safe==9,"explicit load reset releases locks");
        CityStateLock.Apply(other,a,new float[]{30,30,30,30,30},false);a.safe=8;CityStateLock.Enforce(other);
        Check(a.safe==8,"apply without lock remains editable by the game");
        CityStateLock.Clear(); CityStateLock.Apply(other,a,new float[]{30,30,30,30,30},true);
        other.Lookups=0; a.areaInfoDirty=false; a.areaDetailDirty=false;
        for(var frame=0;frame<120;frame++) CityStateLock.Poll(other,frame/60.0);
        Check(other.Lookups==4,"120 frames perform only four fallback lookups per locked city");
        Check(!a.areaInfoDirty&&!a.areaDetailDirty,"unchanged polling does not dirty city UI");
        a.safe=1; CityStateLock.Enforce(other,a);
        Check(a.safe==30,"native mutation enforcement remains immediate between polls");
        a.safe=2; CityStateLock.Poll(other,1.999);
        Check(a.safe==2,"direct writes wait for bounded fallback");
        CityStateLock.Poll(other,2.0); Check(a.safe==30,"direct writes restored at next half-second poll");
        CityStateLock.Poll(w,2.001);
        Check(!CityStateLock.HasLocks,"world change clears locks even before next poll is due");
        other.Lookups=0; CityStateLock.Poll(other,3);
        Check(other.Lookups==0,"disabled locks perform no city lookups");
        Console.WriteLine($"{count} city checks passed; mock data, not native game tests.");
    }
}
public class AreaData {public IntPtr Pointer; public int areaID; public float safe,support,defence,people,maxPeople; public bool areaInfoDirty,areaDetailDirty;}
public class WorldData {public IntPtr Pointer;public int Lookups;public Dictionary<int,AreaData> Areas=new();public AreaData? GetArea(int id){Lookups++;return Areas.TryGetValue(id,out var a)?a:null;}}
public static class LongYinTrainerPlugin {public static TestLog Logger=new();}
public class TestLog {public void LogWarning(string s)=>throw new Exception(s);}
