using System.Runtime.CompilerServices;
using HarmonyLib;
using BepInEx.Configuration;
using BepInEx.Logging;

System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (ctx, name) =>
{
    var file = Path.Combine(AppContext.BaseDirectory, name.Name + ".dll");
    return File.Exists(file) ? ctx.LoadFromAssemblyPath(file) : null;
};
var config = new ConfigEntry<bool>();
DateFreezeControl.Initialize(config, new ManualLogSource());
var game = new GameController();
void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("PASS " + label); }
bool NoFreezePatches() => new[] {
    AccessTools.Method(typeof(GameController), "ChangeDay", Type.EmptyTypes),
    AccessTools.Method(typeof(GameController), "ChangeDay", new[] { typeof(int) })
}.All(m => Harmony.GetPatchInfo(m)?.Prefixes.All(p => p.owner != "codex.longyin.trainer.freeze-date") != false);
game.ChangeDay(); game.ChangeDay(2);
Check(game.Days == 3 && NoFreezePatches(), "disabled startup: both native entry points run without freeze patch");
for (var i = 0; i < 30; i++)
{
    var before = game.Days;
    DateFreezeControl.SetEnabled(true);
    game.ChangeDay(); game.ChangeDay(3);
    Check(game.Days == before && DateFreezeControl.Enabled, $"cycle {i}: freeze blocks both overloads");
    DateFreezeControl.SetEnabled(false);
    game.ChangeDay(); game.ChangeDay(3);
    Check(game.Days == before + 4 && NoFreezePatches(), $"cycle {i}: unfreeze removes patches and resumes progression");
}
config.Value = true;
var oldDays = game.Days; game.ChangeDay();
Check(game.Days == oldDays, "external config enable uses same runtime state");
config.Value = false; game.ChangeDay();
Check(game.Days == oldDays + 1 && NoFreezePatches(), "external config disable removes interception");
DateFreezeControl.SetEnabled(false); game.ChangeDay(2);
Check(game.Days == oldDays + 3 && NoFreezePatches(), "repeated disable remains safe");
Console.WriteLine("64 checks passed with real Harmony and simulated GameController; not a game-save test.");

public class GameController
{
    public int Days;
    [MethodImpl(MethodImplOptions.NoInlining)] public void ChangeDay() => Days++;
    [MethodImpl(MethodImplOptions.NoInlining)] public void ChangeDay(int count) { for (var i = 0; i < count; i++) ChangeDay(); }
}
namespace BepInEx.Configuration
{
    public class ConfigEntry<T>
    {
        private T _value = default!;
        public event EventHandler? SettingChanged;
        public T Value { get => _value; set { if (Equals(_value, value)) return; _value = value; SettingChanged?.Invoke(this, EventArgs.Empty); } }
    }
}
namespace BepInEx.Logging
{
    public class ManualLogSource { public void LogInfo(string text) { } public void LogError(string text) => Console.WriteLine(text); }
}
