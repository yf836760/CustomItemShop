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


namespace Econ
{
    [ApiVersion(2, 1)]
    public class RankPlugin : TerrariaPlugin
    {
        public override string Author => "AK copy from POBC";
        public override string Description => "Econ Plugin";
        public override string Name => "Econ";
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;
        public RankPlugin(Main game) : base(game)
        {
        }
        private static string configPath = Path.Combine(TShock.SavePath, "rank.json");
        private static StreamReader reader;
        private static string json;
        private static JsonNode jsonNode;
        private static JsonNodeOptions jsonNodeOptions = new JsonNodeOptions { PropertyNameCaseInsensitive = false };
        private static IEnumerable<RankCostColor> rankCostColors;
        public static string Specifier
        {
            get { return string.IsNullOrWhiteSpace(TShock.Config.Settings.CommandSpecifier) ? "/" : TShock.Config.Settings.CommandSpecifier; }
        }
        private static int maxLevel = 0;
        public static Dictionary<int, int> rankIDMap;
        public static Dictionary<int, int> rankCostMap;
        public static Dictionary<int, string> rankColorMap;


        public override void Initialize()
        {
            Commands.ChatCommands.Add(new Command(
                //permissions: new List<string> { "rpg.ak", "rpg", },
                cmd: this.Cmd,
                "rank"));
            TShockAPI.Hooks.PlayerHooks.PlayerChat += OnChat;
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            AccountHooks.AccountCreate += OnAccountCreate;
            GeneralHooks.ReloadEvent += OnReload;
            PlayerHooks.PlayerPostLogin += OnPlayerPostLogin;
            CreateIDRankDictionary();
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                TShockAPI.Hooks.PlayerHooks.PlayerChat -= OnChat;
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                GeneralHooks.ReloadEvent -= OnReload;
                AccountHooks.AccountCreate -= OnAccountCreate;
                PlayerHooks.PlayerPostLogin -= OnPlayerPostLogin;
            }
            base.Dispose(disposing);
        }
        private void OnPlayerPostLogin(PlayerPostLoginEventArgs e)
        {
            EconPlugin.UpdateEcon(0, e.Player.Account.ID);
            CreateIDRankDictionary();
        }
        private void OnAccountCreate(AccountCreateEventArgs e)
        {
            EconPlugin.UpdateEcon(0, e.Account.ID);
            CreateIDRankDictionary();
            
        }

        private void OnChat(TShockAPI.Hooks.PlayerChatEventArgs args)
        {
            Player plr = args.Player.TPlayer;
            string name = plr.name;
            int rank = rankIDMap[args.Player.Account.ID];
            string color = rankColorMap[rank];
            args.TShockFormattedText = $"[c/{color}:Level-{rank}] {name}: {args.RawText}";
            args.Handled = false;
        }
        
        private void Cmd(CommandArgs args)
        {

            if (args.Parameters.Count != 0 )
            {
                args.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}rank", Specifier);
                return;
            }
            
            if (args.Player.IsLoggedIn)
            {
                int accountID = args.Player.Account.ID;
                int rankNow = rankIDMap[accountID];
                int costNeeded = rankNow < maxLevel ? rankCostMap[rankNow + 1] : int.MaxValue;
                EconPlugin.UpdateEcon(EconPlugin.econPlayers[args.Player.Index].Econ, accountID);
                int econ = EconPlugin.QueryEcon(accountID);
                if (rankNow == maxLevel)
                {
                    args.Player.SendErrorMessage("You already level max");
                    
                }
                else if (econ > costNeeded)
                {
                    RankUp(accountID, costNeeded);
                    econ = EconPlugin.QueryEcon(accountID);
                    string color = rankColorMap[rankNow + 1];
                    args.Player.SendSuccessMessage($"Now [c/{color}:Level_{rankNow + 1}]，remaining econ: {econ}");
                    CreateIDRankDictionary();
                }
                else
                {
                    args.Player.SendErrorMessage($"Not enough econ！Need {rankCostMap[rankNow + 1]} to level up");
                }
            }
            else { args.Player.SendErrorMessage("Not logged in!"); }
        }
        private void CreateMaxLevel()
        {
            foreach (var rankCostColor in rankCostColors)
            {
                
                maxLevel = maxLevel > rankCostColor.Rank ? maxLevel : rankCostColor.Rank;
            }
        }
        private void CreateIDRankDictionary()
        {
            
            string sFilePath = Path.Combine(TShock.SavePath, "tshock.sqlite");
            using (SqliteConnection oSqlLiteConnection = new SqliteConnection("URI=file:" + sFilePath))
            {
                oSqlLiteConnection.Open();
                string sqlCommand2 = $"SELECT Account, Rank from Econ";
                SqliteCommand cmd = new SqliteCommand(sqlCommand2, oSqlLiteConnection);
                
                using (var reader = cmd.ExecuteReader())
                {
                    Dictionary<int, int> _rankIDMap = new Dictionary<int, int>();
                    while (reader.Read())
                    {
                        int id = reader.GetInt32(0);
                        int rank = reader.GetInt32(1);
                        _rankIDMap.Add(id, rank);
                    }
                    rankIDMap = _rankIDMap;
                }
            }
        }
        private void CreateRankCostDictionary()
        {
            Dictionary<int, int> _rankCostMap = new Dictionary<int, int>();
            foreach (var rankcostcolor in rankCostColors)
            {
                
                _rankCostMap.Add(rankcostcolor.Rank, rankcostcolor.Cost);
            }
            rankCostMap = _rankCostMap;
        }
        private void CreateRankColorsDictionary()
        {
            Dictionary<int, string> _rankColorMap = new Dictionary<int, string>();
            foreach (var rankcostcolor in rankCostColors)
            {
                _rankColorMap.Add(rankcostcolor.Rank, rankcostcolor.Color);
            }
            rankColorMap = _rankColorMap;
        }
        private void CreateRankCostColorsIEnumable()
        {
            reader = new StreamReader(configPath);
            json = reader.ReadToEnd();
            jsonNode = JsonNode.Parse(json, jsonNodeOptions);
            rankCostColors =
                from rank in jsonNode.AsArray()
                select new RankCostColor
                {
                    Rank = (int)rank["Rank"],
                    Cost = (int)rank["Cost"],
                    Color = (string)rank["Color"]
                };
            reader.Close();
        }
        private void RankUp(int accountID, int costNeeded)
        {
            string sFilePath = Path.Combine(TShock.SavePath, "tshock.sqlite");
            using (SqliteConnection oSqlLiteConnection = new SqliteConnection("URI=file:" + sFilePath))
            {
                oSqlLiteConnection.Open();
                string sqlCommand2 = $"UPDATE Econ SET Econ = Econ - {costNeeded}, Rank = Rank + 1 where Account = {accountID}";
                SqliteCommand cmd = new SqliteCommand(sqlCommand2, oSqlLiteConnection);
                cmd.ExecuteNonQuery();
            }
        }
        internal static int QueryRank(int ID)
        {
            string sFilePath = Path.Combine(TShock.SavePath, "rank.sqlite");
            using (SqliteConnection oSqlLiteConnection = new SqliteConnection("URI=file:" + sFilePath))
            {
                oSqlLiteConnection.Open();
                string sqlCommand2 = $"SELECT Rank from Econ WHERE Account = {ID}";
                SqliteCommand cmd = new SqliteCommand(sqlCommand2, oSqlLiteConnection);
                int Rank = 0;
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        Rank = reader.GetInt32(0);
                        return Rank;
                    }
                    return Rank;
                }
            }
        }
        private void OnInitialize(EventArgs args)
        {
            if (!File.Exists(configPath))
            {
                File.Create(configPath);
            }
            CreateIDRankDictionary();
            CreateRankCostColorsIEnumable();
            CreateRankColorsDictionary();
            CreateRankCostDictionary();
            CreateMaxLevel();
        }
        private void OnReload(ReloadEventArgs args)
        {
            if (!File.Exists(configPath))
            {
                File.Create(configPath);
            }
            CreateIDRankDictionary();
            CreateRankCostColorsIEnumable();
            CreateRankColorsDictionary();
            CreateRankCostDictionary();
            CreateMaxLevel();
        }
    }
    class RankCostColor
    {
        public int Rank { get; set; }
        public int Cost { get; set; }
        public string Color { get; set; }
    }
}
