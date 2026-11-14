using System.Collections;
using System.Reflection;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

[BepInPlugin("codex.longyin.character-audit", "Character Audit", "1.0.0")]
public class CharacterAudit : BasePlugin
{
    static CharacterAudit self = null!;
    static bool done;
    static Type T = typeof(TrainerBehaviour);
    static BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
    static object? Get(string n) => T.GetField(n, flags)!.GetValue(null);
    static void Set(string n, object? v) => T.GetField(n, flags)!.SetValue(null, v);
    static object? Call(string n, params object[] args) => T.GetMethod(n, flags)!.Invoke(null, args);
    static void Check(bool ok, string text) { if (!ok) throw new Exception(text); self.Log.LogInfo("PASS " + text); }
    public override void Load() { self = this; new Harmony("codex.longyin.character-audit").Patch(AccessTools.Method(typeof(GameDataController), "Update"), postfix: new HarmonyMethod(typeof(CharacterAudit), nameof(Tick))); }
    static void Click(string label)
    {
        foreach (var c in (IEnumerable)Get("PageClicks")!)
        {
            var rect=(RectTransform)c.GetType().GetField("Rect")!.GetValue(c)!;
            if(rect.GetComponentInChildren<Text>()?.text == label) { ((Action)c.GetType().GetField("Action")!.GetValue(c)!)(); return; }
        }
        throw new Exception("Button missing: " + label);
    }
    static void Tick()
    {
        if (done || GameDataController.Instance?.kungfuSkillDataBase == null || GameDataController.Instance.kungfuSkillDataBase.Count == 0) return;
        done = true;
        object? prior = Get("_activePageContent");
        GameObject? root = null;
        try
        {
            Call("EnsureNativeUi");
            var h = new HeroData(); h.heroName="QA only"; h.heroID=-98765;
            root = (GameObject)Call("UiObject", "CharacterAudit", ((RectTransform)Get("_uiContent")!).transform, new Vector2(1080,654), Vector2.zero)!;
            Set("_activePageContent",root.GetComponent<RectTransform>());
            void Clear() { foreach(Transform child in root.transform) UnityEngine.Object.Destroy(child.gameObject); ((IList)Get("PageClicks")!).Clear(); }
            for(int kind=1;kind<=3;kind++)
            {
                var b=kind==1?h.baseAttri:kind==2?h.baseFightSkill:h.baseLivingSkill;
                var c=kind==1?h.maxAttri:kind==2?h.maxFightSkill:h.maxLivingSkill;
                var count=kind==1?GlobalData.AttriName.Count:kind==2?GlobalData.FightSkillName.Count:GlobalData.LivingSkillName.Count;
                b.Clear();c.Clear(); for(int i=0;i<count;i++){b.Add(20);c.Add(100);}
                Call("BuildCharacterStats",h,kind);
                Click("基础值全部填入"); Click("仅应用基础值");
                Check(b[0]==200 && c[0]==100,"base-only writes preserve caps kind="+kind);
                foreach(var input in root.GetComponentsInChildren<InputField>()) if(input.GetComponent<RectTransform>().anchoredPosition == new Vector2(770,-200)) input.text="150";
                Click("上限全部填入"); Click("仅应用上限");
                Check(b[0]==200 && c[0]==150,"cap-only writes preserve base kind="+kind);
                foreach(var input in root.GetComponentsInChildren<InputField>()) if(input.GetComponent<RectTransform>().anchoredPosition == new Vector2(200,-95)) input.text="NaN";
                Click("应用两列");
                Check(b[0]==200 && c[0]==150,"invalid numeric input leaves both columns unchanged kind="+kind);
                Clear();
            }
            Call("BuildCharacterPreferences",h);
            bool initial=h.kungfuSkillFocus.Contains(0);
            Click(GlobalData.FightSkillName[0]);
            Click("应用专长与喜好");
            Check(h.hobby != null && h.kungfuSkillFocus != null,"preference editor creates valid native lists");
            Check(h.kungfuSkillFocus.Contains(0)!=initial,"specialty selection updates native hero");
            Click(GlobalData.FightSkillName[0]); Click("应用专长与喜好");
            Check(h.kungfuSkillFocus.Contains(0)==initial,"specialty deselection updates native hero");
            bool hadHobby=h.hobby.Contains(40);
            Click(GlobalData.TreasureTypeName[0]); Click("应用专长与喜好");
            Check(h.hobby.Contains(40)!=hadHobby,"hobby selection updates native hero");
            Check(GlobalData.GetHobbyString(40)==GlobalData.TreasureTypeName[0],"hobby labels match native item encoding");
            Click(GlobalData.TreasureTypeName[0]); Click("应用专长与喜好");
            Check(h.hobby.Contains(40)==hadHobby,"hobby deselection updates native hero");
            Click("下一页");Click("上一页");
            Check(true,"hobby paging callbacks execute");
            Clear();
            var before=h.GenerateFaceCode();
            Call("BuildCharacterFace",h);
            Click("应用到当前角色");
            Check(h.GenerateFaceCode()==before,"native appearance roundtrip");
            var inputs=root.GetComponentsInChildren<InputField>();
            foreach(var field in inputs) field.text="0/bad/code";
            Click("应用到当前角色");
            Check(h.GenerateFaceCode()==before,"invalid appearance preserves previous data");
            foreach(var field in inputs) field.text=(before.StartsWith("0/")?"1/":"0/")+before.Substring(2);
            Click("应用到当前角色");
            Check(h.GenerateFaceCode()!=before,"different native appearance code actually changes synthetic hero");
            self.Log.LogInfo("CHARACTER AUDIT COMPLETE (synthetic hero, no saves written)");
        }
        catch(Exception ex) {self.Log.LogError("CHARACTER AUDIT FAILED: "+ex);}
        finally {Set("_activePageContent",prior); ((IList)Get("PageClicks")!).Clear(); if(root!=null)UnityEngine.Object.Destroy(root);}
    }
}
