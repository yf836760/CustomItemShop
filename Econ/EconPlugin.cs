using System;
using System.Reflection;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using OTAPI;
using Mono.Data.Sqlite;
using Terraria.ID;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Econ
{
    [ApiVersion(2, 1)]
    public class EconPlugin : TerrariaPlugin
    {
        public override string Author => "AK copy from POBC";
        public override string Description => "Econ Plugin";
        public override string Name => "Econ";
        public override Version Version => Assembly.GetExecutingAssembly().GetName().Version;

        public EconPlugin(Main game) : base(game)
        {
        }
        public EconPlayer econPlayer;
        public static EconPlayer[] econPlayers = new EconPlayer[Main.maxPlayers];
        public override void Initialize()
        {

            Commands.ChatCommands.Add(new Command(
                //permissions: new List<string> { "rpg.ak", "rpg", },
                cmd: this.Cmd,
                "request", "req"));
            Commands.ChatCommands.Add(new Command(
                permissions: new List<string> { "econ.admin" },
                cmd: this.Cmd2,
                "econ"));
            ServerApi.Hooks.NpcStrike.Register(this, OnNpcStrike);
            ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            CreateTable();
        }
        private void Cmd2(CommandArgs args)
        {
            if (!args.Player.HasPermission("econ.admin"))
            {
                args.Player.SendErrorMessage("You don't have 'econ.admin' perm");
                return;
            }
            else if (args.Parameters.Count != 1 && args.Parameters.Count != 3)
            {
                args.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}econ reset\tor{0}econ add <player name/all> <econ amount>", ShopPlugin.Specifier);
                return;
            }
            else
            {
                string firstParameter = args.Parameters.First<string>();
                
                switch (firstParameter)
                {
                    case "reset":
                        ResetEcon();
                        TSPlayer.All.SendSuccessMessage($"{args.Player.Name}succeed in taking everyone back to the stone age");
                        break;
                    case "add":
                        string secondParameter = args.Parameters.ElementAt(1);
                        string thirdParameter = args.Parameters.Last();
                        bool isThirdParameterInt = int.TryParse(thirdParameter, out int result);
                        if (secondParameter == "all")
                        {
                            if (isThirdParameterInt)
                            {
                                AddEconToAll(result);
                            }
                            else
                            {
                                args.Player.SendErrorMessage("cannot parse arguments into integer value，Proper syntax，e.g. econ add all 5000");
                            }
                        }
                        else
                        {
                            var players = TSPlayer.FindByNameOrID(secondParameter);
                            TSPlayer player;
                            if (players.Count == 0)
                                args.Player.SendErrorMessage("Invalid player!");
                            else if (players.Count > 1)
                                args.Player.SendMultipleMatchError(players.Select(p => p.Name));
                            else
                            {
                                player = players.First();
                                int accountID = players.First().Account.ID;
                                AddEconToPlayer(player, result);
                            }
                        }
                        break;
                    default:
                        args.Player.SendErrorMessage("Invalid syntax! Proper syntax: {0}econ reset\t或者{0}econ add <player name/all> <econ amount>", ShopPlugin.Specifier);
                        break;
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                //Hooks.Npc.Strike -= OnNpcStrike;
                ServerApi.Hooks.NpcStrike.Deregister(this, OnNpcStrike);
                ServerApi.Hooks.ServerJoin.Deregister(this, OnJoin);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
            }
            base.Dispose(disposing);
        }
        private void Cmd(CommandArgs args)
        {
            var player = econPlayers[args.Player.Index];
            if (args.Player.IsLoggedIn)
            {
                UpdateEcon(player.Econ, player.Account);
                player.Econ = 0;
                int Econ = QueryEcon(player.Account);
                args.Player.SendSuccessMessage($"Total econ amount: {Econ}");
            }
            else { args.Player.SendErrorMessage("Not logged in!"); }
        }

        private void OnJoin(JoinEventArgs args)
        {
            econPlayers[args.Who] = new EconPlayer(args.Who);
        }
        private void OnLeave(LeaveEventArgs args)
        {
            var player = econPlayers[args.Who];
            if (player == null) return;
            UpdateEcon(player.Econ, player.Account);
            player.Econ = 0;
            econPlayers[args.Who] = null;
        }
        private void AddEconToPlayer(TSPlayer player, int econAmount)
        {
            int accountID = player.Account.ID;
            string sFilePath = Path.Combine(TShock.SavePath, "tshock.sqlite");
            using (SqliteConnection oSqlLiteConnection = new SqliteConnection("URI=file:" + sFilePath))
            {
                oSqlLiteConnection.Open();
                string sqlCommand2 = $"UPDATE Econ SET Econ = Econ + {econAmount} where Account = {accountID}";
                SqliteCommand cmd = new SqliteCommand(sqlCommand2, oSqlLiteConnection);
                cmd.ExecuteNonQuery();
            }
            player.SendSuccessMessage($"Gift from server，econ+{econAmount}");
        }
        private void AddEconToAll(int econAmount)
        {
            string sFilePath = Path.Combine(TShock.SavePath, "tshock.sqlite");
            using (SqliteConnection oSqlLiteConnection = new SqliteConnection("URI=file:" + sFilePath))
            {
                oSqlLiteConnection.Open();
                string sqlCommand2 = $"UPDATE Econ SET Econ = Econ + {econAmount} ";
                SqliteCommand cmd = new SqliteCommand(sqlCommand2, oSqlLiteConnection);
                cmd.ExecuteNonQuery();
            }
            TSPlayer.All.SendSuccessMessage($"Gift from server，econ+{econAmount}");
        }
        private void ResetEcon()
        {
            string sFilePath = Path.Combine(TShock.SavePath, "tshock.sqlite");
            using (SqliteConnection oSqlLiteConnection = new SqliteConnection("URI=file:" + sFilePath))
            {
                oSqlLiteConnection.Open();
                string sqlCommand2 = $"UPDATE Econ SET Econ = 0 WHERE Account > 0";
                SqliteCommand cmd = new SqliteCommand(sqlCommand2, oSqlLiteConnection);
                cmd.ExecuteNonQuery();
            }
        }
        private void OnNpcStrike(NpcStrikeEventArgs args)
        {
            econPlayers[args.Player.whoAmI]?.OnNpcStrike(args);
        }
        internal static int QueryEcon(int ID)
        {
            string sFilePath = Path.Combine(TShock.SavePath, "tshock.sqlite");
            using (SqliteConnection oSqlLiteConnection = new SqliteConnection("URI=file:" + sFilePath))
            {
                oSqlLiteConnection.Open();
                string sqlCommand2 = $"SELECT Econ from Econ WHERE Account = {ID}";
                SqliteCommand cmd = new SqliteCommand(sqlCommand2, oSqlLiteConnection);
                int Econ = 0;
                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {

                        Econ = reader.GetInt32(0);
                        return Econ;
                    }
                    return Econ;
                }
            }
        }

        
        internal static void UpdateEcon(int econ, int ID)
        {
            string sFilePath = Path.Combine(TShock.SavePath, "tshock.sqlite");
            using (SqliteConnection oSqlLiteConnection = new SqliteConnection("URI=file:" + sFilePath))
            {
                oSqlLiteConnection.Open();
                string sqlCommand2 = $"INSERT INTO Econ (Account ,Econ) VALUES ({ID},{econ}) ON CONFLICT(Account) DO UPDATE SET Econ = Econ + {econ} where Account = {ID}";
                SqliteCommand cmd = new SqliteCommand(sqlCommand2, oSqlLiteConnection);
                cmd.ExecuteNonQuery();
            }
        }
        private void CreateTable()
        {
            string sFilePath = Path.Combine(TShock.SavePath, "tshock.sqlite");

            using (SqliteConnection oSqlLiteConnection = new SqliteConnection("URI=file:" + sFilePath))
            {
                oSqlLiteConnection.Open();
                SqliteCommand cmd = new SqliteCommand("create table if not exists Econ (Account INTEGER NOT NULL UNIQUE REFERENCES tsCharacter (Account) ON DELETE RESTRICT ON UPDATE CASCADE, Econ INTEGER DEFAULT 0, Rank INTEGER DEFAULT 0)", oSqlLiteConnection);
                cmd.ExecuteNonQuery();

                /*
                cmd = new SqliteCommand("alter table Users add Econ INTEGER NOT NULL", oSqlLiteConnection);
                cmd.ExecuteNonQuery();
                */
            }
            using (SqliteConnection oSqlLiteConnection = new SqliteConnection("URI=file:" + sFilePath))
            {
                oSqlLiteConnection.Open();
                string sqlCommand2 = $"PRAGMA table_info(Econ)";
                SqliteCommand cmd = new SqliteCommand(sqlCommand2, oSqlLiteConnection);
                
                using (var reader = cmd.ExecuteReader())
                {
                    bool hasRankColumn = false;
                    
                    while (reader.Read())
                    {
                        if (reader["name"].ToString() == "Rank")
                        {
                            hasRankColumn = true;
                        }
                    }
                    if (!hasRankColumn)
                    {
                        sqlCommand2 = $"alter table Econ add column Rank INTEGER DEFAULT 0";
                        cmd = new SqliteCommand(sqlCommand2, oSqlLiteConnection);
                        cmd.ExecuteNonQuery();
                    }
                }
                using (var reader = cmd.ExecuteReader())
                {
                    
                    bool hasItemsColumn = false;
                    while (reader.Read())
                    {
                        if (reader["name"].ToString() == "Items")
                        {
                            hasItemsColumn = true;
                        }
                    }
                    if (!hasItemsColumn)
                    {
                        sqlCommand2 = $"alter table Econ add column Items TEXT DEFAULT ''";
                        cmd = new SqliteCommand(sqlCommand2, oSqlLiteConnection);
                        cmd.ExecuteNonQuery();
                    }
                }
            }

            /*
            cmd = new SqliteCommand("alter table Users add Econ INTEGER NOT NULL", oSqlLiteConnection);
            cmd.ExecuteNonQuery();
            */

        }
    }

    public class EconPlayer
    {
        public int Index { get; }
        public int Econ { get; set; } = 0;
        public TSPlayer tsplayer => TShock.Players[Index];
        public int Account => tsplayer.Account.ID;
        public EconPlayer(int index)
        {
            Index = index;
        }
        public List<int> itemsBought;
        public void OnNpcStrike(NpcStrikeEventArgs args)
        {
            var realDamage = (int)Main.CalculateDamageNPCsTake(args.Damage, args.Npc.defense);
            var realNPC = args.Npc.realLife > 0 ? Main.npc[args.Npc.realLife] : args.Npc;
            var totalDamage = (int)(Math.Min(realDamage, realNPC.life));
            if (args.Npc.type != NPCID.TargetDummy && totalDamage > 0 && !args.Npc.SpawnedFromStatue)
            {
                Econ += totalDamage / 2;
            }
        }
    }
}

