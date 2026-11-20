using System;
using UnityEngine;
using UnityEngine.UI;

public static partial class TrainerBehaviour
{
    private static int _consumableLevelFilter = -1;
    private static int CatalogDisplayGrade(CatalogEntry entry) => entry.Category is "药品" or "食物" ? entry.BaseLevel : entry.RareLv;
    private static int _bookTypeFilter = -1, _bookRarityFilter = -1;

    private static Action BuildBookCatalogFilters(Transform parent, Action refresh, bool editingItem)
    {
        const int filterTop = 498;
        const int rowHeight = 32;
        const int gap = 6;
        const int rowStep = rowHeight + gap;
        var levelButtons = new Button[7];
        for (var i = 0; i < levelButtons.Length; i++)
        {
            var value = i - 1;
            levelButtons[i] = AddButton(parent, i == 0 ? "全部等级" : LevelName(value),
                new Vector2(i * 98, filterTop), new Vector2(92, rowHeight), () =>
                { if (_consumableLevelFilter == value) return; _consumableLevelFilter = value; _catalogPage = 0; refresh(); });
        }
        var typeButtons = new Button[10];
        var rarityButtons = new Button[7];
        for (var i = 0; i < typeButtons.Length; i++)
        {
            var value = i - 1;
            typeButtons[i] = AddButton(parent, i == 0 ? "全部武学" : CustomKungfuTypeName(value),
                new Vector2(i % 5 * 138, filterTop + i / 5 * rowStep), new Vector2(132, rowHeight), () =>
                { if (_bookTypeFilter == value) return; _bookTypeFilter = value; _catalogPage = 0; refresh(); });
        }
        for (var i = 0; i < rarityButtons.Length; i++)
        {
            var value = i - 1;
            rarityButtons[i] = AddButton(parent, i == 0 ? "全部品级" : RarityName(value),
                new Vector2(i * 98, filterTop + 2 * rowStep), new Vector2(92, rowHeight), () =>
                { if (_bookRarityFilter == value) return; _bookRarityFilter = value; _catalogPage = 0; refresh(); });
        }
        var note = AddText(parent, "", Vector2.zero, new Vector2(690, 40), 15, TextAnchor.UpperLeft);
        return () =>
        {
            var categories = CatalogCategories();
            var category = categories[Math.Clamp(_catalogCategory, 0, categories.Count - 1)];
            var visible = category == "秘籍";
            var consumable = category is "药品" or "食物";
            var rowCount = visible ? 3 : consumable ? 1 : 0;
            var filterHint = visible ? "按武学种类、秘籍品级筛选。" : consumable ? "卡框颜色表示原生等级，稀有度可在右侧设置。" : "";
            var modeHint = editingItem ? "正在编辑背包物品。点击图鉴卡片可切回生成。" : "";
            note.text = filterHint.Length > 0 && modeHint.Length > 0 ? filterHint + "\n" + modeHint : filterHint + modeHint;
            note.rectTransform.anchoredPosition = new Vector2(0, -(filterTop + rowCount * rowStep));
            note.gameObject.SetActive(note.text.Length > 0);
            for (var i = 0; i < levelButtons.Length; i++) SetBookFilterButton(levelButtons[i], consumable, i - 1 == _consumableLevelFilter);
            for (var i = 0; i < typeButtons.Length; i++) SetBookFilterButton(typeButtons[i], visible, i - 1 == _bookTypeFilter);
            for (var i = 0; i < rarityButtons.Length; i++) SetBookFilterButton(rarityButtons[i], visible, i - 1 == _bookRarityFilter);
        };
    }

    private static void SetBookFilterButton(Button button, bool visible, bool selected)
    {
        button.gameObject.SetActive(visible);
        if (!visible) return;
        StyleNavigation(button, selected);
    }
}
