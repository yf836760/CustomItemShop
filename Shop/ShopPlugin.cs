using System;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using OTAPI;
using Mono.Data.Sqlite;
using Terraria.ID;
using System.IO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using System.Text.Json.Nodes;
using System;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI.Hooks;
using OTAPI;
using System.Text.Json.Nodes;
using System.IO;
using static Terraria.NetMessage;
using Econ;


namespace Econ.Shop
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {
        public override string Author => "AK copy from POBC";
        public override string Description => "Shop Plugin";
        public override string Name => "Shop";
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        public Plugin(Main game) : base(game)
        {
        }
        string configPath = Path.Combine(TShock.SavePath, "shop.json");
        private static StreamReader reader;
        private static string json;
        private static JsonNode jsonNode;
        private static JsonNodeOptions jsonNodeOptions = new JsonNodeOptions { PropertyNameCaseInsensitive = false };
        public static string Specifier
        {
            get { return string.IsNullOrWhiteSpace(TShock.Config.Settings.CommandSpecifier) ? "/" : TShock.Config.Settings.CommandSpecifier; }
        }
        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command(
                //permissions: new List<string> { "rpg.ak", "rpg", },
                cmd: this.Cmd,
                "商店", "shop"));
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            GeneralHooks.ReloadEvent += OnReload;

        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                GeneralHooks.ReloadEvent -= OnReload;

            }
            base.Dispose(disposing);
        }
        private void Cmd(CommandArgs args)
        {
            if (args.Parameters.Count != 1 && args.Parameters.Count !=2)
            {
                args.Player.SendErrorMessage("语法错误! 正确语法: {0}shop list \t {0}shop buy <商品编号>", Specifier);
                return;
            }
            string firstParameter = args.Parameters.First<string>();
            
            switch (firstParameter)
            {
                case "list":
                    ShowShopItems();
                    break;
                case "buy":
                    {
                        int itemID;
                        string secondParameter = args.Parameters.ElementAt<string>(1);
                        if (!int.TryParse(secondParameter, out itemID))
                        {
                            args.Player.SendErrorMessage("语法错误! 正确语法: {0}shop buy <商品编号>", Specifier);
                            break;
                        }
                        else
                        {
                            BuyShopItems(itemID);
                            break;
                        }
                    }
                default:
                    break;
            }
            var player = Plugin.econPlayers[args.Player.Index];
            if (args.Player.IsLoggedIn)
            {
                UpdateEcon(player.Econ, player.Account);
                player.Econ = 0;
                int Econ = QueryEcon(player.Account);
                args.Player.SendSuccessMessage($"你的经验是: {Econ}");
            }
        }
        private void ShowShopItems()
        {

        }
        private void BuyShopItems(int itemID)
        {

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
            reader.Close();

        }

        private void OnReload(ReloadEventArgs args)
        {
            reader = new StreamReader(configPath);
            json = reader.ReadToEnd();
            jsonNode = JsonNode.Parse(json, jsonNodeOptions);
            reader.Close();
        }

    }

    class ShopItem
    {
        public int Item { get; }
        public int Amount { get; }
        public int Price { get; }
    }
}