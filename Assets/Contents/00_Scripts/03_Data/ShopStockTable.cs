using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ShopMerchantWeights
{
    public int Rui = 1;
    public int Ronan = 1;
    public int Opel = 1;
}

[Serializable]
public class ShopStockRow
{
    public int No;
    public string Category;
    public string Name_KR;
    public string SO_Name;
    public string SO_Class;
    public string Item_ID;
    public int Price;
    public bool Rui;
    public bool Ronan;
    public bool Opel;
    public bool Implemented;
    public string Note;
}

[Serializable]
public class ShopStockFile
{
    public ShopMerchantWeights merchantWeights;
    public int slotCount = 6;
    public List<ShopStockRow> stock = new List<ShopStockRow>();
}

public class ShopStockOffer
{
    public ShopStockRow Row;

    public bool SellsTo(ShopManager.MapMerchant merchant)
    {
        if (Row == null) return false;
        switch (merchant)
        {
            case ShopManager.MapMerchant.Louis: return Row.Rui;
            case ShopManager.MapMerchant.Ronan: return Row.Ronan;
            default: return Row.Opel;
        }
    }
}

public class ShopStockTable
{
    public ShopMerchantWeights Weights = new ShopMerchantWeights();
    public int SlotCount = 6;
    public bool Loaded;
    public int UnimplementedCount;
    public List<ShopStockRow> Rows = new List<ShopStockRow>();
    readonly Dictionary<string, ShopStockOffer> offers = new Dictionary<string, ShopStockOffer>();

    public static string Key(string soClass, string soName)
    {
        return (soClass ?? "") + "\n" + (soName ?? "");
    }

    public static string KeyOf(BaseItemDataSO item)
    {
        if (item == null) return "";
        return Key(item.GetType().Name, item.name);
    }

    public static ShopStockTable Parse(string json)
    {
        var table = new ShopStockTable();
        if (string.IsNullOrEmpty(json)) return table;

        json = json.Replace("\"Price\": null", "\"Price\": 0");
        json = json.Replace("\"Price\":null", "\"Price\": 0");
        ShopStockFile raw = JsonUtility.FromJson<ShopStockFile>(json);
        if (raw == null || raw.stock == null) return table;

        table.Loaded = true;
        if (raw.merchantWeights != null) table.Weights = raw.merchantWeights;
        if (raw.slotCount > 0) table.SlotCount = raw.slotCount;
        table.Rows = raw.stock;

        for (int i = 0; i < raw.stock.Count; i++)
        {
            ShopStockRow row = raw.stock[i];
            if (row == null) continue;
            if (!row.Implemented)
            {
                table.UnimplementedCount++;
                continue;
            }

            string key = Key(row.SO_Class, row.SO_Name);
            if (table.offers.ContainsKey(key)) continue;
            table.offers[key] = new ShopStockOffer { Row = row };
        }

        return table;
    }

    public ShopStockOffer Find(BaseItemDataSO item)
    {
        if (item == null) return null;
        offers.TryGetValue(KeyOf(item), out ShopStockOffer offer);
        return offer;
    }

    public bool Sells(ShopStockRow row, ShopManager.MapMerchant merchant)
    {
        if (row == null) return false;
        switch (merchant)
        {
            case ShopManager.MapMerchant.Louis: return row.Rui;
            case ShopManager.MapMerchant.Ronan: return row.Ronan;
            default: return row.Opel;
        }
    }

    public int Count(ShopManager.MapMerchant merchant, bool includeUnimplemented)
    {
        int count = 0;
        for (int i = 0; i < Rows.Count; i++)
        {
            ShopStockRow row = Rows[i];
            if (row == null) continue;
            if (!includeUnimplemented && !row.Implemented) continue;
            if (Sells(row, merchant)) count++;
        }
        return count;
    }

    public ShopManager.MapMerchant RollMerchant()
    {
        int rui = Mathf.Max(0, Weights.Rui);
        int ronan = Mathf.Max(0, Weights.Ronan);
        int opel = Mathf.Max(0, Weights.Opel);
        int total = rui + ronan + opel;
        if (total <= 0) return (ShopManager.MapMerchant)UnityEngine.Random.Range(0, 3);

        int roll = UnityEngine.Random.Range(0, total);
        if (roll < rui) return ShopManager.MapMerchant.Louis;
        if (roll < rui + ronan) return ShopManager.MapMerchant.Ronan;
        return ShopManager.MapMerchant.Opel;
    }

    public List<string> MissingNames(IList<BaseItemDataSO> pool)
    {
        var have = new HashSet<string>();
        if (pool != null)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] == null) continue;
                have.Add(KeyOf(pool[i]));
            }
        }

        var missing = new List<string>();
        for (int i = 0; i < Rows.Count; i++)
        {
            ShopStockRow row = Rows[i];
            if (row == null || !row.Implemented) continue;
            if (!have.Contains(Key(row.SO_Class, row.SO_Name)))
                missing.Add(string.IsNullOrEmpty(row.SO_Name) ? row.Item_ID : row.SO_Name);
        }
        return missing;
    }
}
