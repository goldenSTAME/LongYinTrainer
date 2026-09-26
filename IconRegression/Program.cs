using UnityEngine;
using System.Reflection;

var atlas = new NGUIAtlas { texture = new Texture2D { height = 256, width = 256 } };
atlas.spriteList.Add(new UISpriteData { name = "future-item", x = 10, y = 20, width = 30, height = 40 });
Resources.Assets.Add(atlas);
void Check(bool ok, string label) { if (!ok) throw new Exception(label); Console.WriteLine("PASS " + label); }
var icon = TrainerBehaviour.Get("future-item");
Check(icon != null && icon.rect.y == 196, "New icon without exported PNG and top-to-bottom UV conversion");
Check(ReferenceEquals(icon, TrainerBehaviour.Get("future-item")), "Visible icon sprite reused");
Check(TrainerBehaviour.Get("later-item") == null, "Missing icon does not throw");
atlas.spriteList.Add(new UISpriteData { name = "later-item", width = 20, height = 20 });
TrainerBehaviour.Refresh();
Check(TrainerBehaviour.Get("later-item") != null, "Previously missing icon appears after refresh");
Check(ReferenceEquals(icon, TrainerBehaviour.Get("future-item")), "Unchanged icon survives index refresh");
atlas.spriteList[0].x = 50;
TrainerBehaviour.Refresh();
Check(!ReferenceEquals(icon, TrainerBehaviour.Get("future-item")) && icon!.destroyed, "Changed atlas rect replaces and releases owned sprite");
atlas.spriteList.RemoveAt(0);
TrainerBehaviour.Refresh();
Check(TrainerBehaviour.Get("future-item") == null, "Removed icon not retained in runtime index");
atlas.spriteList.Add(new UISpriteData { name = "bad", x = 250, width = 30, height = 20 });
TrainerBehaviour.Refresh();
Check(TrainerBehaviour.Get("bad") == null, "Out-of-bounds atlas region ignored");
Resources.Assets.Clear();
TrainerBehaviour.Refresh();
var scans = Resources.Scans;
TrainerBehaviour.Get("none"); TrainerBehaviour.Get("none2");
Check(Resources.Scans == scans, "Unavailable assets are throttled instead of rescanned per card");
Resources.Assets.Add(atlas);
Time.realtimeSinceStartup += 6;
TrainerBehaviour.Refresh();
Check(TrainerBehaviour.Get("later-item") != null, "Delayed assets appear after explicit refresh");
var native = new Sprite { name = "native-future" };
Resources.Assets.Add(native);
TrainerBehaviour.Refresh();
Check(ReferenceEquals(native, TrainerBehaviour.Get("native-future")), "Native sprite used directly without PNG or UV reconstruction");
Resources.Assets.Remove(native);
TrainerBehaviour.Refresh();
Check(!native.destroyed, "Refresh never destroys game-owned sprites");
Console.WriteLine("12 regression checks passed (mock Unity; in-game validation still required).");

public static partial class TrainerBehaviour {
 public static Sprite? Get(string name) => GetRuntimeItemSprite(name);
 public static void Refresh() => RefreshRuntimeIconIndex();
}
public static class LongYinTrainerPlugin { public static Log Logger = new(); }
public class Log { public void LogInfo(string s) {} public void LogWarning(string s) => Console.WriteLine(s); }
public class UISpriteData { public string name=""; public int x,y,width,height; }
public class NGUIAtlas : UnityEngine.Object { public Texture2D texture=null!; public Il2CppSystem.Collections.Generic.List<UISpriteData> spriteList=new(); }
public class UIAtlas : NGUIAtlas {}
namespace Il2CppInterop.Runtime { public static class Il2CppType { public static Type Of<T>() => typeof(T); } }
namespace Il2CppSystem.Collections.Generic { public class List<T> : System.Collections.Generic.List<T> {} }
namespace UnityEngine {
 public class Object { public bool destroyed; public string name=""; public T? TryCast<T>() where T:class => this as T; public static void Destroy(Object o) => o.destroyed=true; }
 public class Texture2D : Object { public int width,height; }
 public record struct Rect(float x,float y,float width,float height);
 public record struct Vector2(float x,float y);
 public enum SpriteMeshType { FullRect }
 public class Sprite : Object { public Rect rect; public static Sprite Create(Texture2D t, Rect r, Vector2 v, float p, uint e, SpriteMeshType m) => new Sprite {rect=r}; }
 public static class Time { public static float realtimeSinceStartup=10; }
 public static class Resources {
  public static System.Collections.Generic.List<Object> Assets=new(); public static int Scans;
  public static Object[] LoadAll(string s, Type t) { Scans++; return Assets.Where(o=>o.GetType()==t).ToArray(); }
  public static Object[] FindObjectsOfTypeAll(Type t) { Scans++; return Assets.Where(o=>o.GetType()==t).ToArray(); }
 }
}
