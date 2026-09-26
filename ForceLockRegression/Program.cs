var f=new ForceData {forceID=25,Pointer=(IntPtr)25,forceName="A",resourceStore=new(){100,200}};
var w=new World {Pointer=(IntPtr)1}; w.forces.Add(25,f); GameController.Instance.worldData=w;
void Check(bool b,string s){if(!b)throw new Exception(s);Console.WriteLine("PASS "+s);}
TrainerBehaviour.Toggle(f); f.resourceStore[0]=20; f.forceStorage.money=0; TrainerBehaviour.EnforceForceLock(f);
Check(f.resourceStore[0]==100 && f.forceStorage.money==500,"Restores money and resources");
f.resourceStore[1]=999; TrainerBehaviour.EnforceForceLock(); Check(f.resourceStore[1]==200,"Fixed values maintained with panel closed");
var other=new ForceData {forceID=26,Pointer=(IntPtr)26,resourceStore=new(){3}}; w.forces.Add(26,other);
TrainerBehaviour.EnforceForceLock(other); Check(other.resourceStore[0]==3,"Unrelated faction unchanged");
TrainerBehaviour.Toggle(f); f.resourceStore[0]=10; TrainerBehaviour.EnforceForceLock(); Check(f.resourceStore[0]==10,"Unlock stops restoration");
TrainerBehaviour.Toggle(f); GameController.Instance.worldData=new World {Pointer=(IntPtr)2}; TrainerBehaviour.EnforceForceLock();
GameController.Instance.worldData=w; f.resourceStore[0]=1; TrainerBehaviour.EnforceForceLock(); Check(f.resourceStore[0]==1,"Changing world releases lock permanently");
TrainerBehaviour.Toggle(f); f.Pointer=(IntPtr)100; f.resourceStore[0]=0; TrainerBehaviour.EnforceForceLock(); Check(f.resourceStore[0]==0,"Replaced faction instance not modified");
f.resourceStore[0]=float.NaN; TrainerBehaviour.Toggle(f); Check(!TrainerBehaviour.Locked(f),"Invalid values cannot be locked");
Console.WriteLine("7 force lock regression checks passed (mock game).");
public static partial class TrainerBehaviour { private static string _status=""; public static void Toggle(ForceData f)=>ToggleForceLock(f); public static bool Locked(ForceData f)=>IsForceLocked(f); }
public class ForceData {public int forceID;public IntPtr Pointer;public string forceName="";public List<float> resourceStore=new();public Storage forceStorage=new();public bool forceDetailDirty;}
public class Storage {public int money=500;}
public class World {public IntPtr Pointer; public Dictionary<int,ForceData> forces=new();public ForceData? GetForce(int id)=>forces.GetValueOrDefault(id);}
public class GameController {public static GameController Instance=new();public World? worldData;}
public static class LongYinTrainerPlugin {public static Log Logger=new();}
public class Log {public void LogWarning(string s)=>Console.WriteLine(s);}
namespace UnityEngine.UI {public class Text {public string text="";}}
