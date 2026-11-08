using System;
using UnityEngine;
using UnityEngine.UI;

public static partial class TrainerBehaviour
{
    private static int _bookTypeFilter = -1, _bookRarityFilter = -1;

    private static Action BuildBookCatalogFilters(Transform parent, Action refresh)
    {
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
