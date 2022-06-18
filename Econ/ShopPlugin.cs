using System;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using Mono.Data.Sqlite;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using TShockAPI.Hooks;
using Microsoft.Xna.Framework;

namespace Econ
{
    [ApiVersion(2, 1)]
    public class ShopPlugin : TerrariaPlugin
    {
        public override string Author => "AK copy from POBC";
        public override string Description => "Shop Plugin";
        public override string Name => "Shop";
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        public ShopPlugin(Main game) : base(game)
        {
        }
        private static string configPath = Path.Combine(TShock.SavePath, "shop.json");
        private static StreamReader reader;
        private static string json;
        private static JsonNode jsonNode;
        private static JsonNodeOptions jsonNodeOptions = new JsonNodeOptions { PropertyNameCaseInsensitive = false };
        private static IEnumerable<ShopItem> shopItems;

        private string configPath_customitem = Path.Combine(TShock.SavePath, "shop_customitem.json");
        private static StreamReader customReader;
        private static string customJson;
        private static JsonNode customJsonNode;
        private static IEnumerable<ShopCustomItem> shopCustomItems;

        private Dictionary<ShopItem, string> itemDictionary;
        const int AMOUNT_OF_All_ITEMS = 9999;
        private Dictionary<int, List<int>> itemsBought;

        public static string Specifier
        {
            get { return string.IsNullOrWhiteSpace(TShock.Config.Settings.CommandSpecifier) ? "/" : TShock.Config.Settings.CommandSpecifier; }
        }
        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command(
                //permissions: new List<string> { "rpg.ak", "rpg", },
                cmd: this.Cmd,
                "shop"));
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            GeneralHooks.ReloadEvent += OnReload;
            AccountHooks.AccountCreate += OnAccountCreate;
            PlayerHooks.PlayerPostLogin += OnPlayerPostLogin;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                GeneralHooks.ReloadEvent -= OnReload;
                AccountHooks.AccountCreate -= OnAccountCreate;
                PlayerHooks.PlayerPostLogin -= OnPlayerPostLogin;
            }
            base.Dispose(disposing);
        }
        private void Cmd(CommandArgs args)
        {
            if (!args.Player.IsLoggedIn)
            {
                args.Player.SendErrorMessage("Not logged in!");
                return;
            }
            else if (args.Parameters.Count != 1 && args.Parameters.Count !=2)
            {
                args.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}shop list \t {0}shop buy <itemID>", Specifier);
                return;
            }
            else
            {
                string firstParameter = args.Parameters.First<string>();

                switch (firstParameter)
                {
                    case "list":
                        ShowShopItems(args.Player);
                        break;
                    case "buy":
                        {
                            int itemID;
                            string secondParameter = args.Parameters.ElementAt<string>(1);
                            bool isValidID = int.TryParse(secondParameter, out itemID);

                            if (!isValidID)
                            {
                                args.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}shop buy <itemID>", Specifier);
                                break;
                            }
                            else if (!CheckItemIDIncluded(itemID))
                            {
                                args.Player.SendErrorMessage("The good isn't on sell");
                                break;
                            }
                            else
                            {
                                BuyShopItems(args.Player, itemID);
                                break;
                            }
                        }
                    default:
                        break;
                }
            }
            
        }
        private void ShowShopItems(TSPlayer tSPlayer)
        {
            
            string itemList = CreateItemList(tSPlayer);

            tSPlayer.SendMessage(itemList, Color.GreenYellow);
            return;
        }
        private void CreateItemDictionary()
        {
            Dictionary<ShopItem, string> _itemDictionary = new Dictionary<ShopItem, string>();
            foreach (var item in shopItems)
            {
                string composite;
                string itemToAdd = "";
                
                if (item.Item < 5042)
                {
                    composite = "ID.{0, -6} [i/s{1}:{2}] {3,-9} {4}\n";
                    itemToAdd = String.Format(composite, item.Item, item.Amount, item.Item, item.Price, item.Text);
                }
                else
                {
                    foreach (var customItem in shopCustomItems)
                    {
                        if (item.Item == customItem.Item)
                        {
                            Item customitem = TShock.Utils.GetItemById(customItem.ItemID);
                            composite = "ID.{0,-6} [i/s{1}:{2}] {3,-9} {4}\n";
                            itemToAdd = String.Format(composite, customItem.Item, item.Amount, customItem.ItemID, item.Price, item.Text); 
                        }
                    }
                }
                _itemDictionary.Add(item, itemToAdd);
            }
            itemDictionary = _itemDictionary;

        }
        /*
        public enum SortCriteria
        {
            RankThenPrice,
            Price,
        }
        public class SortByShopItem : IComparer<ShopItem>
        {
            public SortCriteria SortBy = SortCriteria.RankThenPrice;
            public int Compare(ShopItem x, ShopItem y)
            {
                if (SortBy == SortCriteria.RankThenPrice)
                {
                    if (x.Rank < y.Rank)
                    {
                        return -1;
                    }
                    if (x.Rank > y.Rank)
                    {
                        return 1;
                    }
                    return 0;
                }
                else
                {
                    if (x.Price < y.Price)
                    {
                        return -1;
                    }
                    if (x.Price > y.Price)
                    {
                        return 1;
                    }
                    return 0;
                }
            }
        }
        */
        private string CreateItemList(TSPlayer tSPlayer)
        {
            int accountID = tSPlayer.Account.ID;
            int playerRank = RankPlugin.rankIDMap[accountID];
            string itemListUnlocked = "Unlocked items:\n";
            string itemListLocked = "[c/FF0000:Locked items:]\n";
            string result = "";
            var itemUnlocked = 
                from item in itemDictionary
                where item.Key.Rank <= playerRank
                select item;
            
            var itemLocked =
                from item in itemDictionary
                where item.Key.Rank > playerRank
                select item;
            bool haveBought = false;
            if (itemUnlocked.Count() == 0)
            {
                itemListUnlocked = "";
            }
            else
            {
                foreach (var shopitemkey in itemUnlocked)
                {

                    ShopItem item = shopitemkey.Key;
                    haveBought = CheckWhetherBought(tSPlayer, item.Item);
                    if (item.Item < AMOUNT_OF_All_ITEMS)
                    {
                        string composite = "ID.{0, -6} [i/s{1}:{2}] {3,-9} {4}\n";
                        itemListUnlocked += String.Format(composite, item.Item, item.Amount, item.Item, item.Price, item.Text);
                    }
                    else
                    {
                        foreach (var customItem in shopCustomItems)
                        {
                            if (item.Item == customItem.Item)
                            {
                                Item customitem = TShock.Utils.GetItemById(customItem.ItemID);
                                string composite = "ID.{0,-6} [i/s{1}:{2}] {3,-9} {4}\n";
                                itemListUnlocked += String.Format(composite, customItem.Item, item.Amount, customItem.ItemID, haveBought ? 0 : item.Price, item.Text);
                            }
                        }
                    }
                }
            }
            if (itemLocked.Count() == 0)
            {
                itemListLocked = "";
            }
            else
            {
                foreach (var shopitemkey in itemLocked)
                {

                    ShopItem item = shopitemkey.Key;
                    
                    int rank = GetRankOfItemID(item.Item);
                    if (item.Item < AMOUNT_OF_All_ITEMS)
                    {
                        string composite = "[c/FF0000:ID.{0, -6}] [i/s{1}:{2}] [c/FF0000:-Unlock Level{5,-6}][c/FF0000:{3,-9} {4}]\n";
                        itemListLocked += String.Format(composite, item.Item, item.Amount, item.Item, item.Price, item.Text, rank);
                    }
                    else
                    {
                        foreach (var customItem in shopCustomItems)
                        {
                            if (item.Item == customItem.Item)
                            {
                                Item customitem = TShock.Utils.GetItemById(customItem.ItemID);
                                string composite = "[c/FF0000:ID.{0,-6}] [i/s{1}:{2}] [c/FF0000:-Unlock Level{5,-6}][c/FF0000:{3,-9} {4}]\n";
                                itemListLocked += String.Format(composite, customItem.Item, item.Amount, customItem.ItemID, item.Price, item.Text, rank);
                            }
                        }
                    }
                }
            }
            
            result = itemListUnlocked + itemListLocked;
            return result;
        }
        private int GetRankOfItemID(int itemID)
        {
            var rank =
                from item in shopItems
                where item.Item == itemID
                select item.Rank;
            return rank.First();
        }
        private bool CheckWhetherBought(TSPlayer tSPlayer, int itemID)
        {
            int accountID = tSPlayer.Account.ID;
            List<int> itemsBoughtList = itemsBought[accountID];
            bool haveBought = itemsBoughtList.Contains(itemID);
            return haveBought;
        }
        private void BuyShopItems(TSPlayer tSPlayer, int itemID)
        {
            
            int accountID = tSPlayer.Account.ID;
            bool haveBought = CheckWhetherBought(tSPlayer, itemID);
            int itemRank = GetRankOfItemID(itemID);
            int rank = RankPlugin.rankIDMap[accountID];
            int price = haveBought && itemID > AMOUNT_OF_All_ITEMS ? 0 : GetPriceOfItemID(itemID);
            int amount = GetAmountOfItemID(itemID);
            string text = GetTextOfItemID(itemID);
            
            EconPlugin.UpdateEcon(EconPlugin.econPlayers[tSPlayer.Index].Econ, accountID);
            int econ = EconPlugin.QueryEcon(accountID);


            if (!tSPlayer.InventorySlotAvailable)
            {
                tSPlayer.SendErrorMessage("Maybe you could buy an bigger bag");
                return;
            }
            else if (econ < price)
            {
                tSPlayer.SendErrorMessage($"{econ}? Here is not a charity");
                return;
            }
            else if (rank < itemRank)
            {
                tSPlayer.SendErrorMessage($"Level up needed");
                return;
            }
            else
            {
                string sFilePath = Path.Combine(TShock.SavePath, "tshock.sqlite");
                using (SqliteConnection oSqlLiteConnection = new SqliteConnection("URI=file:" + sFilePath))
                {
                    oSqlLiteConnection.Open();
                    string sqlCommand2 = $"UPDATE Econ SET Econ = Econ - {price} where Account = {accountID}";
                    SqliteCommand cmd = new SqliteCommand(sqlCommand2, oSqlLiteConnection);
                    cmd.ExecuteNonQuery();
                }
                if (text.Contains("customitem"))
                {
                    BuyCustomItem(itemID, tSPlayer);
                    
                    if (!haveBought)
                    {
                        RecordBoughtCustomItem(tSPlayer, itemID);
                        CreateitemBoughtList();
                    }
                    /*
                    Item item = TShock.Utils.GetItemById(itemID);
                    int itemIndex = Item.NewItem(tSPlayer.TPlayer.GetItemSource_OpenItem(itemID), (int)tSPlayer.X, (int)tSPlayer.Y, item.width, item.height, item.type, item.maxStack);
                    Item targetItem = Main.item[itemIndex];
                    targetItem.playerIndexTheItemIsReservedFor = tSPlayer.Index;
                    // customitem 3116 scale 5 damage 1000 shoot 16 shootspeed 50 usetime 5
                    targetItem.damage = 1000;
                    targetItem.knockBack = 12;
                    //targetItem.useAnimation =
                    targetItem.useTime = 5;
                    targetItem.shoot = 16;
                    targetItem.shootSpeed = 50;
                    //targetItem.width
                    //targetItem.height
                    //targetItem.scale = 5
                    //targetItem.ammo
                    //targetItem.useAmmo
                    //targetItem.notAmmo
                    TSPlayer.All.SendData(PacketTypes.UpdateItemDrop, null, itemIndex);
                    TSPlayer.All.SendData(PacketTypes.ItemOwner, null, itemIndex);
                    TSPlayer.All.SendData(PacketTypes.TweakItem, null, itemIndex, 255, 63);
                    */
                }
                else
                {
                    tSPlayer.GiveItem(itemID, amount);
                }
                if (itemID < AMOUNT_OF_All_ITEMS)
                {
                    tSPlayer.SendSuccessMessage($"Succeed in buying [i/s{amount}:{itemID}]{text}，remaining econ:{econ - price}");
                }
                else
                {
                    Item customitem = TShock.Utils.GetItemById(GetCustomItemID(itemID));
                    int maxstack = customitem.maxStack;
                    tSPlayer.SendSuccessMessage($"Succeed in buying [i/s{amount}:{GetCustomItemID(itemID)}]{text}，remaining econ:{econ - price}");
                }
                
            }
        }
        private string GetTextOfItemID(int itemID)
        {
            var text =
                from item in shopItems
                where item.Item == itemID
                select item.Text;
            return text.First();
        }
        private int GetPriceOfItemID(int itemID)
        {
            var price =
                from item in shopItems
                where item.Item == itemID
                select item.Price;
            return price.First();
        }
        private int GetAmountOfItemID(int itemID)
        {
            var amount =
                from item in shopItems
                where item.Item == itemID
                select item.Amount;
            return amount.First();
        }
        private void BuyCustomItem(int item, TSPlayer tSPlayer)
        {
            int itemID = GetCustomItemID(item);
            int amount = GetAmountOfItemID(item);
            Item customItem = TShock.Utils.GetItemById(itemID);
            int itemIndex = Item.NewItem(tSPlayer.TPlayer.GetItemSource_OpenItem(itemID), (int)tSPlayer.X, (int)tSPlayer.Y, customItem.width, customItem.height, customItem.type, amount);
            Item targetItem = Main.item[itemIndex];
            targetItem.playerIndexTheItemIsReservedFor = tSPlayer.Index;
            targetItem.damage = GetCustomItemDamage(item);
            targetItem.shoot = GetCustomItemShoot(item);
            targetItem.knockBack = GetCustomItemKnockBack(item);
            targetItem.useTime = GetCustomItemUseTime(item);
            targetItem.shootSpeed = GetCustomItemShootSpeed(item);
            targetItem.scale = GetCustomItemScale(item);
            targetItem.ammo = GetCustomItemAmmo(item);
            targetItem.useAnimation = GetCustomItemUseAnimation(item);
            targetItem.useAmmo = GetCustomItemUseAmmo(item);
            TSPlayer.All.SendData(PacketTypes.UpdateItemDrop, null, itemIndex);
            TSPlayer.All.SendData(PacketTypes.ItemOwner, null, itemIndex);
            TSPlayer.All.SendData(PacketTypes.TweakItem, null, itemIndex, 255, 63);
        }
        private int GetCustomItemID(int item)
        {
            var itemID =
                from customItem in shopCustomItems
                where customItem.Item == item
                select customItem.ItemID;
            return itemID.First();
        }
        private int GetCustomItemDamage(int item)
        {
            var damage =
                from customItem in shopCustomItems
                where customItem.Item == item
                select customItem.Damage;
            return damage.First();
        }
        private int GetCustomItemShoot(int item)
        {
            var shoot =
                from customItem in shopCustomItems
                where customItem.Item == item
                select customItem.Shoot;
            return shoot.First();
        }
        private int GetCustomItemKnockBack(int item)
        {
            var knockBack =
                from customItem in shopCustomItems
                where customItem.Item == item
                select customItem.KnockBack;
            return knockBack.First();
        }
        private int GetCustomItemUseTime(int item)
        {
            var useTime =
                from customItem in shopCustomItems
                where customItem.Item == item
                select customItem.UseTime;
            return useTime.First();
        }
        private int GetCustomItemShootSpeed(int item)
        {
            var shootSpeed =
                from customItem in shopCustomItems
                where customItem.Item == item
                select customItem.ShootSpeed;
            return shootSpeed.First();
        }
        private int GetCustomItemScale(int item)
        {
            var scale =
                from customItem in shopCustomItems
                where customItem.Item == item
                select customItem.Scale;
            return scale.First();
        }
        private int GetCustomItemAmmo(int item)
        {
            var ammo =
                from customItem in shopCustomItems
                where customItem.Item == item
                select customItem.Ammo;
            return ammo.First();
        }
        private int GetCustomItemUseAmmo(int item)
        {
            var useAmmo =
                from customItem in shopCustomItems
                where customItem.Item == item
                select customItem.UseAmmo;
            return useAmmo.First();
        }
        private int GetCustomItemUseAnimation(int item)
        {
            var useAnimation =
                from customItem in shopCustomItems
                where customItem.Item == item
                select customItem.UseAnimation;
            return useAnimation.First();
        }
        private bool CheckItemIDIncluded(int itemID)
        {
            IEnumerable<int> itemIDList =
                from item in shopItems
                select item.Item;
            bool result = itemIDList.ToList().Contains(itemID);
            return result;
        }
        public void OnInitialize(EventArgs args)
        {
            if (!File.Exists(configPath))
            {
                File.Create(configPath);
            }
            reader = new StreamReader(configPath);
            json = reader.ReadToEnd();
            jsonNode = JsonNode.Parse(json, jsonNodeOptions);
            shopItems =
                from item in jsonNode.AsArray()
                orderby (int)item["Rank"]
                select new ShopItem
                {
                    Item = (int)item["Item"],
                    Rank = (int)item["Rank"],
                    Text = (string)item["Text"],
                    Amount = (int)item["Amount"],
                    Price = (int)item["Price"]
                };
            reader.Close();


            if (!File.Exists(configPath_customitem))
            {
                File.Create(configPath_customitem);
            }
            customReader = new StreamReader(configPath_customitem);
            customJson = customReader.ReadToEnd();
            customJsonNode = JsonNode.Parse(customJson, jsonNodeOptions);
            shopCustomItems =
                from item in customJsonNode.AsArray()
                select new ShopCustomItem
                {
                    Item = (int)item["Item"],
                    ItemID = (int)item["ItemID"],
                    Damage = (int)item["Damage"],
                    Shoot = (int)item["Shoot"],
                    KnockBack = (int)item["KnockBack"],
                    UseTime = (int)item["UseTime"],
                    ShootSpeed = (int)item["ShootSpeed"],
                    Scale = (int)item["Scale"],
                    Ammo = (int)item["Ammo"],
                    UseAmmo = (int)item["UseAmmo"],
                    UseAnimation = (int)item["UseAnimation"]
                };
            customReader.Close();
            //CreateItemList();
            CreateItemDictionary();
            CreateitemBoughtList();


        }
        private void OnReload(ReloadEventArgs args)
        {
            reader = new StreamReader(configPath);
            json = reader.ReadToEnd();
            jsonNode = JsonNode.Parse(json, jsonNodeOptions);
            shopItems =
                from item in jsonNode.AsArray()
                select new ShopItem
                {
                    Item = (int)item["Item"],
                    Rank = (int)item["Rank"],
                    Text = (string)item["Text"],
                    Amount = (int)item["Amount"],
                    Price = (int)item["Price"]
                };
            reader.Close();

            if (!File.Exists(configPath_customitem))
            {
                File.Create(configPath_customitem);
            }
            customReader = new StreamReader(configPath_customitem);
            customJson = customReader.ReadToEnd();
            customJsonNode = JsonNode.Parse(customJson, jsonNodeOptions);
            shopCustomItems =
                from item in customJsonNode.AsArray()
                select new ShopCustomItem
                {
                    Item = (int)item["Item"],
                    ItemID = (int)item["ItemID"],
                    Damage = (int)item["Damage"],
                    Shoot = (int)item["Shoot"],
                    KnockBack = (int)item["KnockBack"],
                    UseTime = (int)item["UseTime"],
                    ShootSpeed = (int)item["ShootSpeed"],
                    Scale = (int)item["Scale"],
                    Ammo = (int)item["Ammo"],
                    UseAmmo = (int)item["UseAmmo"],
                    UseAnimation = (int)item["UseAnimation"]
                };
            customReader.Close();
            //CreateItemList();
            CreateItemDictionary();
        }
        private void OnAccountCreate(AccountCreateEventArgs e)
        {
            EconPlugin.UpdateEcon(0, e.Account.ID);//to create a table for new users
            CreateitemBoughtList();
        }
        private void OnPlayerPostLogin(PlayerPostLoginEventArgs e)
        {
            EconPlugin.UpdateEcon(0, e.Player.Account.ID);
            CreateitemBoughtList();
        }
        private void RecordBoughtCustomItem(TSPlayer tSPlayer, int item)
        {
            int accountID = tSPlayer.Account.ID;
            if (itemsBought[accountID].Contains(item))
            {
                return;
            }
            else
            {
                string sFilePath = Path.Combine(TShock.SavePath, "tshock.sqlite");
                itemsBought[accountID].Add(item);
                
                if (itemsBought[accountID].Count == 0)
                {
                    using (SqliteConnection oSqlLiteConnection = new SqliteConnection("URI=file:" + sFilePath))
                    {
                        oSqlLiteConnection.Open();
                        string itemToString = item.ToString();
                        string sqlCommand2 = $"UPDATE Econ SET Items = {itemToString} where Account = {accountID}";
                        SqliteCommand cmd = new SqliteCommand(sqlCommand2, oSqlLiteConnection);
                        cmd.ExecuteNonQuery();
                    }
                }
                else
                {
                    string itemsToRecord = "";
                    foreach (var itemBought in itemsBought[accountID])
                    {
                        itemsToRecord = itemsToRecord + ',' + itemBought.ToString();
                    }


                    using (SqliteConnection oSqlLiteConnection = new SqliteConnection("URI=file:" + sFilePath))
                    {
                        oSqlLiteConnection.Open();
                        string sqlCommand2 = $"UPDATE Econ SET Items = '{itemsToRecord}' where Account = {accountID}";
                        SqliteCommand cmd = new SqliteCommand(sqlCommand2, oSqlLiteConnection);
                        cmd.ExecuteNonQuery();
                    }
                }
                
            }
        }
        private void CreateitemBoughtList()
        {
            string sFilePath = Path.Combine(TShock.SavePath, "tshock.sqlite");
            Dictionary<int, List<int>> _itemsBought = new Dictionary<int, List<int>>();
            using (SqliteConnection oSqlLiteConnection = new SqliteConnection("URI=file:" + sFilePath))
            {
                oSqlLiteConnection.Open();
                string sqlCommand2 = $"SELECT Account, Items from Econ";
                SqliteCommand cmd = new SqliteCommand(sqlCommand2, oSqlLiteConnection);
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int accountID = reader.GetInt32(0);
                        string[] items = reader.GetString(1).Split(',');
                        List<int> itemsList = new List<int>();
                        foreach (var item in items)
                        {
                            if (int.TryParse(item,out int result))
                            {
                                itemsList.Add(result);
                            }
                            else
                            {
                                itemsList = new List<int>() { 0};
                            }
                        }
                        _itemsBought.Add(accountID,itemsList);
                    }
                }
            } 
            itemsBought = _itemsBought;
        }
    }

    public class ShopItem
    {
        public int Item { get; set; }
        public int Rank { get; set; }
        public string Text { get; set; }
        public int Amount { get; set; }
        public int Price { get; set; }
    }
    public class ShopCustomItem
    {
        public int Item { get; set; }
        public int ItemID { get; set; }
        public int Damage { get; set; }
        public int Shoot { get; set; }
        public int KnockBack { get; set; }
        public int UseTime { get; set; }
        public int ShootSpeed { get; set; }
        public int Scale { get; set; }
        public int Ammo { get; set; }
        public int UseAmmo { get; set; }
        public int UseAnimation { get; set; }

    }
}