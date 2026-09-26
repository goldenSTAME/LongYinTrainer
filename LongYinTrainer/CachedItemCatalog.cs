using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static partial class TrainerBehaviour
{
    private static void BuildNativeItemCatalog()
    {
        if (ResolveSelectedItem() != null) { BuildLegacyItemCatalog(); return; }
        EnsureCatalog();
        var p = NativePageParent;
        var categories = CatalogCategories();
        var entries = new CatalogEntry?[12];
        var cards = new Button[12];
        var frames = new Image[12];
        var icons = new Image[12];
        var gems = new Image[12];
        var names = new Text[12];
        var missing = new Text[12];
        var categoryButtons = new Button[categories.Count];
        var filtered = new List<CatalogEntry>();
        var pageCount = 1;
        Text pageLabel=null!, title=null!, detail=null!, levelLabel=null!, rareLabel=null!, preview=null!;
        InputField quantity=null!, level=null!, rarity=null!;

        AddText(p, "物品图鉴", Vector2.zero, new Vector2(160,34),22);
        var search = AddInput(p,_catalogSearch,new Vector2(160,0),new Vector2(245,34));
        AddButton(p,"搜索",new Vector2(415,0),new Vector2(80,34),()=> { _catalogSearch=search.text.Trim(); _catalogPage=0; UpdateGrid(); });
        AddButton(p,"清空",new Vector2(505,0),new Vector2(80,34),()=> { search.text=""; _catalogSearch=""; _catalogPage=0; UpdateGrid(); });
        AddButton(p,"刷新数据",new Vector2(595,0),new Vector2(100,34),()=> {
            Catalog.Clear(); _catalogSelected=null; EnsureCatalog(); RefreshRuntimeIconIndex(); UpdateGrid(); UpdateDetails();
        });
        for(var i=0;i<categories.Count;i++) {
            var n=i;
            categoryButtons[i]=AddButton(p,categories[i],new Vector2(0,48+i*38),new Vector2(125,32),()=> {
                if(_catalogCategory==n) return;
                _catalogCategory=n; _catalogPage=0; UpdateGrid();
            });
        }
        for(var i=0;i<12;i++) {
            var n=i;
            cards[i]=AddButton(p,"",new Vector2(140+i%4*142,48+i/4*132),new Vector2(132,122),()=> {
                if(entries[n]==null || ReferenceEquals(_catalogSelected,entries[n])) return;
                SaveInputs(); ClearSelectedItem(); _catalogSelected=entries[n]; UpdateSelection(); UpdateDetails();
            });
            frames[i]=cards[i].GetComponent<Image>();
            var paper=UiObject("CardPaper",cards[i].transform,new Vector2(124,114),new Vector2(4,4));
            var bg=paper.AddComponent<Image>(); bg.color=new Color(.97f,.95f,.87f,.98f); bg.raycastTarget=false;
            icons[i]=UiObject("RuntimeIcon",paper.transform,new Vector2(82,82),new Vector2(21,3)).AddComponent<Image>();
            icons[i].preserveAspect=true; icons[i].raycastTarget=false;
            gems[i]=UiObject("Rarity",cards[i].transform,new Vector2(18,18),new Vector2(57,-7)).AddComponent<Image>();
            gems[i].preserveAspect=true; gems[i].raycastTarget=false;
            names[i]=AddText(paper.transform,"",new Vector2(3,90),new Vector2(118,21),14,TextAnchor.MiddleCenter);
            missing[i]=AddText(paper.transform,"暂无图标",new Vector2(6,26),new Vector2(112,40),14,TextAnchor.MiddleCenter);
        }
        AddButton(p,"上一页",new Vector2(140,452),new Vector2(120,34),()=> { if(_catalogPage>0) { _catalogPage--; UpdateGrid(); } });
        pageLabel=AddText(p,"",new Vector2(270,452),new Vector2(250,34),16,TextAnchor.MiddleCenter);
        AddButton(p,"下一页",new Vector2(530,452),new Vector2(120,34),()=> { if(_catalogPage+1<pageCount) { _catalogPage++; UpdateGrid(); } });
        title=AddText(p,"",new Vector2(730,48),new Vector2(330,38),21,TextAnchor.MiddleCenter);
        detail=AddText(p,"",new Vector2(730,90),new Vector2(330,82),16);
        AddText(p,"数量",new Vector2(730,190),new Vector2(80,34));
        quantity=AddInput(p,_catalogQuantity,new Vector2(815,190),new Vector2(130,34));
        levelLabel=AddText(p,"等级",new Vector2(730,234),new Vector2(80,34));
        level=AddInput(p,_catalogLevel,new Vector2(815,234),new Vector2(130,34));
        rareLabel=AddText(p,"稀有度",new Vector2(730,278),new Vector2(80,34));
        rarity=AddInput(p,_catalogRarity,new Vector2(815,278),new Vector2(130,34));
        preview=AddText(p,"",new Vector2(730,316),new Vector2(330,52),14);
        AddButton(p,"添加到主角背包",new Vector2(730,380),new Vector2(300,44),()=> { SaveInputs(); AddCatalogItem(_catalogSelected,_catalogQuantity,_catalogLevel,_catalogRarity); });
        AddText(p,"稀有度说明\n"+BuildRarityGuide(),new Vector2(730,438),new Vector2(330,160),15);
        AddText(p,"测试版：仅使用游戏资源，旧图片回退已禁用。资源加载后可点击“刷新数据”。\n分类、翻页和选择物品均复用现有控件。",new Vector2(0,520),new Vector2(690,56),15);
        UpdateGrid(); UpdateDetails();
        LongYinTrainerPlugin.Logger.LogInfo("Catalog UI pool built: 12 reusable cards; runtime icons only.");

        void SaveInputs() { _catalogQuantity=quantity.text; _catalogLevel=level.text; _catalogRarity=rarity.text; }
        void UpdateSelection() {
            for(var i=0;i<12;i++) if(entries[i]!=null) ApplySprite(frames[i],_frameSprite,true,Color.white,
                ReferenceEquals(entries[i],_catalogSelected) ? new Color(.72f,.12f,.08f,1f) : RarityColor(entries[i]!.RareLv));
        }
        void UpdateGrid() {
            filtered=FilterCatalog(categories[Math.Clamp(_catalogCategory,0,categories.Count-1)],_catalogSearch);
            pageCount=Math.Max(1,(filtered.Count+11)/12); _catalogPage=Math.Clamp(_catalogPage,0,pageCount-1);
            for(var i=0;i<categories.Count;i++) {
                categoryButtons[i].GetComponent<Image>().color=i==_catalogCategory ? new Color(.70f,.43f,.08f,1f) : new Color(.095f,.08f,.065f,.98f);
            }
            var shown=0; var found=0;
            for(var i=0;i<12;i++) {
                var index=_catalogPage*12+i;
                entries[i]=index<filtered.Count ? filtered[index] : null;
                cards[i].gameObject.SetActive(entries[i]!=null);
                var e=entries[i]; if(e==null) continue;
                shown++; names[i].text=e.Name;
                icons[i].sprite=GetItemSprite(e.IconName); icons[i].enabled=icons[i].sprite!=null;
                if(icons[i].enabled) found++;
                missing[i].gameObject.SetActive(!icons[i].enabled);
                gems[i].sprite=GetItemSprite("RareLv"+Math.Clamp(e.RareLv,0,5)); gems[i].enabled=gems[i].sprite!=null;
            }
            pageLabel.text=$"第 {_catalogPage+1} / {pageCount} 页　共 {filtered.Count} 项";
            UpdateSelection();
            LongYinTrainerPlugin.Logger.LogInfo($"Catalog runtime icons: {found}/{shown} visible cards resolved; PNG fallback DISABLED.");
        }
        void UpdateDetails() {
            var e=_catalogSelected;
            title.text=e?.Name ?? "从左侧图鉴选择物品";
            detail.text=e?.Detail ?? "列表直接读取当前游戏版本的数据。\n即使背包里没有，也可以直接添加。";
            var usesLevel=e!=null && (e.IsEquipment || e.Category=="材料" || e.Category=="宝物");
            var usesRarity=e!=null && (usesLevel || e.Category=="秘籍");
            levelLabel.gameObject.SetActive(usesLevel); level.gameObject.SetActive(usesLevel);
            rareLabel.gameObject.SetActive(usesRarity); rarity.gameObject.SetActive(usesRarity);
            _catalogLevelInput=usesLevel ? level : null; _catalogRarityInput=usesRarity ? rarity : null; _catalogPreviewText=preview;
            preview.text=e==null ? "" : $"原始等级 {e.BaseLevel}｜{RarityName(e.RareLv)}";
            UpdateCatalogPreview();
        }
    }
}
