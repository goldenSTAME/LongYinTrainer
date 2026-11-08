int checks = 0;
void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; }
Check(BookCatalogRules.IsEligible(995, 0, "龙胤归藏功"), "hidden internal book");
Check(BookCatalogRules.IsEligible(1050, 4, "星落九天"), "hidden sword book");
Check(BookCatalogRules.IsEligible(0, 0, "吐纳法"), "ID zero");
Check(!BookCatalogRules.IsEligible(-1, 0, "invalid"), "invalid ID");
Check(!BookCatalogRules.IsEligible(1, 9, "invalid"), "invalid type");
Check(!BookCatalogRules.IsEligible(1, 0, " "), "blank definition");
for (int type = 0; type < 9; type++)
for (int rarity = 0; rarity < 6; rarity++)
for (int selectedType = -1; selectedType < 9; selectedType++)
for (int selectedRarity = -1; selectedRarity < 6; selectedRarity++)
    Check(BookCatalogRules.Matches(type, rarity, selectedType, selectedRarity) ==
        ((selectedType == -1 || selectedType == type) && (selectedRarity == -1 || selectedRarity == rarity)), "combined filter");
Check(BookCatalogRules.Compare(0, 0, 4, 5) < 0, "category first");
Check(BookCatalogRules.Compare(4, 5, 4, 0) < 0, "highest rarity first");
Check(BookCatalogRules.Compare(4, 5, 4, 5) == 0, "name tie breaker");
if (args.Length > 0)
{
    var rows = File.ReadAllLines(args[0]).Select(line => line.Split('\t')).ToArray();
    foreach (var row in rows)
        Check(BookCatalogRules.IsEligible(int.Parse(row[0]), int.Parse(row[1]), row[3]), "native definition eligibility");
    foreach (var id in new[] { "995", "1050" })
    {
        var row = rows.Single(r => r[0] == id);
        Check(row[2] == "5" && row[4] == "1", "native hidden top-grade definition");
    }
    Console.WriteLine($"Checked {rows.Length} native definitions from extracted game resource.");
}
Console.WriteLine($"PASS {checks} catalog checks; no game inventory was changed.");
