using System;
using UnityEngine;
using UnityEngine.UI;

public static partial class TrainerBehaviour
{
    private static int _consumableLevelFilter = -1;
    private static int CatalogDisplayGrade(CatalogEntry entry) => entry.Category is "药品" or "食物" ? entry.BaseLevel : entry.RareLv;
    private static int _bookTypeFilter = -1, _bookRarityFilter = -1;

    private static Action BuildBookCatalogFilters(Transform parent, Action refresh)
    {
        var levelButtons = new Button[7];
        for (var i = 0; i < levelButtons.Length; i++)
        {
            var value = i - 1;
            levelButtons[i] = AddButton(parent, i == 0 ? "全部等级" : LevelName(value),
                new Vector2(i * 98, 498), new Vector2(94, 34), () =>
                { _consumableLevelFilter = value; _catalogPage = 0; refresh(); });
        }
        var levelNote = AddText(parent, "按原生等级筛选；卡框表示等级，右侧稀有度单独设置。", new Vector2(0, 538), new Vector2(690, 28), 14);
        var typeButtons = new Button[10];
        var rarityButtons = new Button[7];
        for (var i = 0; i < typeButtons.Length; i++)
        {
            var value = i - 1;
            typeButtons[i] = AddButton(parent, i == 0 ? "全部武学" : CustomKungfuTypeName(value),
                new Vector2(i % 5 * 138, 498 + i / 5 * 36), new Vector2(132, 32), () =>
                { _bookTypeFilter = value; _catalogPage = 0; refresh(); });
        }
        for (var i = 0; i < rarityButtons.Length; i++)
        {
            var value = i - 1;
            rarityButtons[i] = AddButton(parent, i == 0 ? "全部品级" : RarityName(value),
                new Vector2(i * 98, 574), new Vector2(94, 34), () =>
                { _bookRarityFilter = value; _catalogPage = 0; refresh(); });
        }
        var note = AddText(parent, "按武学种类、原生品级排列；品级不是修炼层数。", new Vector2(0, 610), new Vector2(690, 28), 14);
        return () =>
        {
            var categories = CatalogCategories();
            var visible = categories[Math.Clamp(_catalogCategory, 0, categories.Count - 1)] == "秘籍";
            var consumable = categories[Math.Clamp(_catalogCategory, 0, categories.Count - 1)] is "药品" or "食物";
            levelNote.gameObject.SetActive(consumable);
            for (var i = 0; i < levelButtons.Length; i++) SetBookFilterButton(levelButtons[i], consumable, i - 1 == _consumableLevelFilter);
            note.gameObject.SetActive(visible);
            for (var i = 0; i < typeButtons.Length; i++) SetBookFilterButton(typeButtons[i], visible, i - 1 == _bookTypeFilter);
            for (var i = 0; i < rarityButtons.Length; i++) SetBookFilterButton(rarityButtons[i], visible, i - 1 == _bookRarityFilter);
        };
    }

    private static void SetBookFilterButton(Button button, bool visible, bool selected)
    {
        button.gameObject.SetActive(visible);
        button.GetComponent<Image>().color = selected ? new Color(.70f, .43f, .08f, 1f) : new Color(.095f, .08f, .065f, .98f);
    }
}
