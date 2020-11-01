﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Core;
using System;
using Oxide.Core.Configuration;


namespace Oxide.Plugins
{
    [Info("LandLord", "bravoavo", "1.0.3")]
    [Description("Take control of the map")]

    class LandLord : RustPlugin
    {

        [PluginReference] Plugin ZoneManager;
        ConfigData configData;
        private DynamicConfigFile data;

        //Constant initialization
        readonly float size = 146.3f;
        readonly bool debug = true;
        readonly string zonenameprefix = "Zone";
        Color32 orange = new Color32(255, 157, 0, 1);
        readonly Color[] colorArray = new Color[] { Color.black, Color.white, Color.red, Color.blue, Color.green, Color.yellow, Color.cyan, Color.gray, new Color32(255, 157, 0, 1) };
        private Color colors;
        public Dictionary<ulong, HashSet<int[]>> flagi = new Dictionary<ulong, HashSet<int[]>>();
        public Dictionary<ulong, List<string>> quadrants = new Dictionary<ulong, List<string>>();
        public Dictionary<string, Vector3> poles = new Dictionary<string, Vector3>();
        public Dictionary<ulong, int> gatherMultiplier = new Dictionary<ulong, int>();
        public Dictionary<ulong, ulong> teamList = new Dictionary<ulong, ulong>();

        public Dictionary<string, MapMarkerGenericRadius> allmarkers = new Dictionary<string, MapMarkerGenericRadius>();
        public Dictionary<string, List<MapMarkerGenericRadius>> flagmarkers = new Dictionary<string, List<MapMarkerGenericRadius>>();



        #region ZoneInitialization
        private void MyZonesInit()
        {
            float mapsize = TerrainMeta.Size.x;
            int i = 1;
            for (var x = -mapsize / 2 + size / 2; x <= mapsize / 2; x = x + size)
            {
                for (var z = -mapsize / 2 + size / 2; z <= mapsize / 2; z = z + size)
                {
                    string[] array = new string[] { "name", "Main" + zonenameprefix + i, "size", "146.3 500 146.3", "location", x + " 0 " + z };
                    ZoneManager.Call("CreateOrUpdateZone", "10" + i, array);
                    i++;
                }
            }
            var InitFile = Interface.Oxide.DataFileSystem.GetFile("LandLord");
            InitFile.WriteObject("Zone init has been complited");
        }
        #endregion


        #region InitPlugin
        private void OnServerInitialized()
        {
            if (ZoneManager)
            {
                if (!Interface.Oxide.DataFileSystem.ExistsDatafile("LandLord"))
                {
                    MyZonesInit();
                }
            }
            if (Interface.Oxide.DataFileSystem.ExistsDatafile("LandLord.Data"))
            {
                data = Interface.Oxide.DataFileSystem.GetDatafile("LandLord.Data");
            }
            LoadData();
        }



        #endregion

        #region PlayerInit
        void OnPlayerSleepEnded(BasePlayer player)
        {
            LordTeamInit(player);
            foreach (var quadrat in quadrants)
            {
                foreach (var zone in quadrat.Value)
                {
                    allmarkers[zone].SendUpdate();
                    flagmarkers[zone][0].SendUpdate();
                    flagmarkers[zone][1].SendUpdate();
                    flagmarkers[zone][2].SendUpdate();

                }
            }
            if (!flagi.ContainsKey(player.userID)) GenarateFlag(player.userID);
        }

        private void GenarateFlag(ulong playerId)
        {
            if (flagi.Count < 900)
            {
                System.Random rnd = new System.Random();
                int[] colorsArrayTmp;
                flagi.Add(playerId, new HashSet<int[]>());
                do
                {
                    int color1 = rnd.Next(0, 8);
                    int color2 = rnd.Next(0, 8);
                    int color3 = rnd.Next(0, 8);
                    colorsArrayTmp = new int[] { color1, color2, color3 };
                } while (!flagi[playerId].Add(colorsArrayTmp));
            }
        }

        private void LordTeamInit(BasePlayer player)
        {
            if (!teamList.ContainsKey(player.userID))
            {
                if (player.currentTeam > 0)
                {
                    var teamelement = UnityEngine.Object.FindObjectOfType<RelationshipManager>();
                    var team = teamelement.FindTeam(player.currentTeam);
                    if (!teamList.ContainsKey(team.teamLeader))
                    {
                        teamList.Add(player.userID, team.teamLeader);
                        //configData.TeamListData.Add(player.userID, team.teamLeader);
                        if (debug) Puts("Has team now!");
                        //reload zone to new team
                        if (quadrants.ContainsKey(player.userID) && player.userID != team.teamLeader)
                        {
                            foreach (var zone in quadrants[player.userID])
                            {
                                DeleteMarkerFromMap(flagmarkers[zone][0]);
                                DeleteMarkerFromMap(flagmarkers[zone][1]);
                                DeleteMarkerFromMap(flagmarkers[zone][2]);
                                flagmarkers.Remove(zone);
                                if (!quadrants.ContainsKey(team.teamLeader))
                                {
                                    quadrants.Add(team.teamLeader, new List<string>());
                                    quadrants[team.teamLeader].Add(zone);
                                    //configData.QuadrantsData.Add(team.teamLeader, new List<string>());
                                    //configData.QuadrantsData[team.teamLeader].Add(zone);
                                }
                                else
                                {
                                    var match = quadrants[team.teamLeader].FirstOrDefault(stringToCheck => stringToCheck.Contains(zone));
                                    if (match != null) return;
                                    else
                                    {
                                        quadrants[team.teamLeader].Add(zone);
                                        //configData.QuadrantsData[team.teamLeader].Add(zone);
                                    }
                                }
                                //increase gather rate
                                if (!gatherMultiplier.ContainsKey(team.teamLeader)) gatherMultiplier.Add(team.teamLeader, 1);
                                else gatherMultiplier[team.teamLeader]++;
                                var zonelocation = (Vector3)ZoneManager.Call("GetZoneLocation", zone);
                                SpawnFlag(zonelocation, zone, team.teamLeader);
                            }
                            quadrants.Remove(player.userID);
                            gatherMultiplier.Remove(player.userID);
                            //configData.QuadrantsData.Remove(player.userID);
                            SaveData();
                        }
                        SaveData();
                        return;
                    }
                    else
                    {
                        //berem lidera u lidera :)
                        var currentLeader = teamList[team.teamLeader];
                        teamList.Add(player.userID, currentLeader);
                        //configData.TeamListData.Add(player.userID, currentLeader);
                        if (debug) Puts("Has team now!");
                        if (quadrants.ContainsKey(player.userID))
                        {
                            foreach (var zone in quadrants[player.userID])
                            {
                                DeleteMarkerFromMap(flagmarkers[zone][0]);
                                DeleteMarkerFromMap(flagmarkers[zone][1]);
                                DeleteMarkerFromMap(flagmarkers[zone][2]);
                                flagmarkers.Remove(zone);
                                if (!quadrants.ContainsKey(currentLeader))
                                {
                                    quadrants.Add(currentLeader, new List<string>());
                                    quadrants[currentLeader].Add(zone);
                                    //configData.QuadrantsData.Add(currentLeader, new List<string>());
                                    //configData.QuadrantsData[currentLeader].Add(zone);
                                }
                                else
                                {
                                    var match = quadrants[currentLeader].FirstOrDefault(stringToCheck => stringToCheck.Contains(zone));
                                    if (match != null) return;
                                    else
                                    {
                                        quadrants[currentLeader].Add(zone);
                                        //configData.QuadrantsData[currentLeader].Add(zone);
                                    }
                                }
                                //increase gather rate
                                if (!gatherMultiplier.ContainsKey(currentLeader)) gatherMultiplier.Add(currentLeader, 1);
                                else gatherMultiplier[currentLeader]++;
                                var zonelocation = (Vector3)ZoneManager.Call("GetZoneLocation", zone);
                                SpawnFlag(zonelocation, zone, currentLeader);
                            }
                            quadrants.Remove(player.userID);
                            gatherMultiplier.Remove(player.userID);
                            //configData.QuadrantsData.Remove(player.userID);
                            SaveData();
                        }
                        SaveData();
                        return;
                    }
                }
                else
                {
                    timer.In(5, () => LordTeamInit(player));
                    //Puts("Has no team");
                    return;
                }
            }
        }
        #endregion

        #region OxideHooks
        void OnEntityKill(BaseNetworkable entity)
        {
            BaseEntity ent = (BaseEntity)entity;
            if (ent.prefabID.ToString() == "3188315846")
            {
                if (debug) Puts("Entity kill - Prefab ID is " + ent.prefabID.ToString() + " Location " + ent.ServerPosition);
                var zoneid = poles.Where(x => x.Value == ent.ServerPosition).FirstOrDefault().Key;
                if (debug) Puts("Looking for zone id. Where function result is " + zoneid);
                if (zoneid != null)
                {
                    if (allmarkers.ContainsKey(zoneid))
                    {
                        DeleteMarkerFromMap(allmarkers[zoneid]);
                        DeleteMarkerFromMap(flagmarkers[zoneid][0]);
                        DeleteMarkerFromMap(flagmarkers[zoneid][1]);
                        DeleteMarkerFromMap(flagmarkers[zoneid][2]);
                        allmarkers.Remove(zoneid);
                        flagmarkers.Remove(zoneid);
                    }
                    if (debug) Puts("User ID " + ent.OwnerID);
                    ulong teampid;
                    if (teamList.ContainsKey(ent.OwnerID)) { teampid = teamList[ent.OwnerID]; }
                    else teampid = ent.OwnerID;
                    quadrants[teampid].Remove(zoneid);
                    poles.Remove(zoneid);
                    if (gatherMultiplier.ContainsKey(teampid))
                    {
                        if (gatherMultiplier[teampid] > 0) gatherMultiplier[teampid]--;
                    }
                    SaveData();
                }
            }
        }


        void OnEntityBuilt(Planner plan, GameObject go)
        {
            // Puts("Player init works " + quadrants.Count());
            BasePlayer player = plan.GetOwnerPlayer();
            string[] zids = (string[])ZoneManager.Call("GetPlayerZoneIDs", player);
            var entity = go.ToBaseEntity();
            if (debug) Puts("Entity build. Prefab ID is " + entity.prefabID.ToString() + " zone ID is " + zids[0]);
            //3188315846
            string curentZoneId = zids[0];
            if (entity.prefabID.ToString() == "3188315846" && curentZoneId != null)
            {
                if (debug) Puts("Prefab is found and zone exists");
                if (poles.ContainsKey(curentZoneId))
                {
                    player.ChatMessage("<color=orange>LANDLORD:</color> This Zone already has a pole! Destroy it before build a new one.");
                    //entity.Kill(BaseNetworkable.DestroyMode.Gib);      
                    return;
                }

                ulong teampid;
                if (teamList.ContainsKey(player.userID)) { teampid = teamList[player.userID]; }
                else teampid = player.userID;
                //ADDING ZONEID TO PLAYER
                if (!quadrants.ContainsKey(teampid))
                {
                    quadrants.Add(teampid, new List<string>());
                    quadrants[teampid].Add(curentZoneId);
                    //configData.QuadrantsData.Add(teampid, new List<string>());
                    //configData.QuadrantsData[teampid].Add(curentZoneId);

                }
                else
                {
                    var match = quadrants[teampid].FirstOrDefault(stringToCheck => stringToCheck.Contains(curentZoneId));
                    if (match != null) return;
                    else
                    {
                        quadrants[teampid].Add(curentZoneId);
                        //configData.QuadrantsData[teampid].Add(curentZoneId);
                    }
                }
                if (!gatherMultiplier.ContainsKey(teampid))
                {
                    gatherMultiplier.Add(teampid, 1);
                    //configData.GatherRateData.Add(teampid, 1);
                }
                else
                {
                    gatherMultiplier[teampid]++;
                    //configData.GatherRateData[teampid]++;
                }
                poles.Add(curentZoneId, entity.ServerPosition);
                if (configData.PolesLocationData.ContainsKey(curentZoneId)) Puts("Pole exists");
                // configData.PolesLocationData.Add(curentZoneId, entity.ServerPosition);

                if (debug) Puts("Server position " + entity.ServerPosition.ToString());
                var zonelocation = (Vector3)ZoneManager.Call("GetZoneLocation", curentZoneId);
                SpawnMarkerOnMap(zonelocation, curentZoneId);
                SpawnFlag(zonelocation, curentZoneId, teampid);
                player.ChatMessage("<color=orange>LANDLORD:</color> You deploy pole! Your gather rate increased!");
                SaveData();
            }
            else if (debug) Puts("Error! Not found zone ID or prefab");
        }

        void OnCollectiblePickup(Item item, BasePlayer player)
        {
            ulong teampid;
            if (teamList.ContainsKey(player.userID)) { teampid = teamList[player.userID]; }
            else teampid = player.userID;
            if (gatherMultiplier.ContainsKey(teampid))
            {
                if (gatherMultiplier[teampid] > 0)
                {
                    float multiplier = (float)gatherMultiplier[teampid] * 3 / 100;
                    item.amount = (int)(item.amount + 1 + item.amount * multiplier);
                }
            }
        }

        object OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
        {
            ulong teampid;
            BasePlayer player = entity.ToPlayer();
            if (teamList.ContainsKey(player.userID)) { teampid = teamList[player.userID]; }
            else teampid = player.userID;
            if (debug) Puts("Start amount" + item.amount);
            if (gatherMultiplier.ContainsKey(teampid))
            {
                if (gatherMultiplier[teampid] > 0)
                {
                    float multiplier = (float)gatherMultiplier[teampid] * 3 / 100;
                    if (debug) Puts("Multiplier is " + multiplier);
                    item.amount = (int)(item.amount + 5 + item.amount * multiplier);
                }
            }
            return null;
        }

        #endregion

        #region MarkersAndMap
        private void DeleteMarkerFromMap(MapMarkerGenericRadius marker)
        {
            marker.Kill();
            marker.SendUpdate();
        }


        private void SpawnMarkerOnMap(Vector3 position, string curentZoneId)
        {
            int mapsize = (int)TerrainMeta.Size.x;
            colors = Color.black;
            float zoneradius = (100000f / mapsize) * 0.02f;
            float markeralpha = 0.5f;
            var mapMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position) as MapMarkerGenericRadius;
            mapMarker.alpha = markeralpha;
            mapMarker.color1 = colors;
            mapMarker.color2 = colors;
            mapMarker.radius = (float)Math.Round(zoneradius, 2);
            mapMarker.Spawn();
            mapMarker.SendUpdate();
            if (!allmarkers.ContainsKey(curentZoneId))
            {
                allmarkers.Add(curentZoneId, mapMarker);
            }
            else
            {
                if (debug) Puts("Marker already in dictionary");
            }

        }

        private void SpawnFlag(Vector3 position, string curentZoneId, ulong playerId)
        {
            if (!flagi.ContainsKey(playerId)) GenarateFlag(playerId);
            HashSet<int[]> array = flagi[playerId];
            List<int> listik = new List<int>();
            foreach (var ids in array)
            {
                listik.Add(ids[0]);
                listik.Add(ids[1]);
                listik.Add(ids[2]);
            }
            float mapsize = TerrainMeta.Size.x;
            Color color1 = colorArray[listik[0]];
            Color color2 = colorArray[listik[1]];
            Color color3 = colorArray[listik[2]];
            Vector3 position1;
            Vector3 position2;
            Vector3 position3;
            float zoneradius = (100000f/mapsize)*0.002f; 
            float markeralpha = 0.7f;
            position1.x = position.x - 63; position1.y = position.y; position1.z = position.z - 63;
            position2.x = position.x - 48; position2.y = position.y; position2.z = position.z - 63;
            position3.x = position.x - 33; position3.y = position.y; position3.z = position.z - 63;
            var mapMarker1 = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position1) as MapMarkerGenericRadius;
            mapMarker1.alpha = markeralpha; mapMarker1.color1 = color1; mapMarker1.color2 = color1; mapMarker1.radius = (float)Math.Round(zoneradius, 2);
            var mapMarker2 = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position2) as MapMarkerGenericRadius;
            mapMarker2.alpha = markeralpha; mapMarker2.color1 = color2; mapMarker2.color2 = color2; mapMarker2.radius = (float)Math.Round(zoneradius, 2);
            var mapMarker3 = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position3) as MapMarkerGenericRadius;
            mapMarker3.alpha = markeralpha; mapMarker3.color1 = color3; mapMarker3.color2 = color3; mapMarker3.radius = (float)Math.Round(zoneradius, 2);

            mapMarker1.Spawn(); mapMarker1.SendUpdate();
            mapMarker2.Spawn(); mapMarker2.SendUpdate();
            mapMarker3.Spawn(); mapMarker3.SendUpdate();

            if (!flagmarkers.ContainsKey(curentZoneId))
            {
                flagmarkers.Add(curentZoneId, new List<MapMarkerGenericRadius>());
                flagmarkers[curentZoneId].Add(mapMarker1);
                flagmarkers[curentZoneId].Add(mapMarker2);
                flagmarkers[curentZoneId].Add(mapMarker3);
            }
            else
            {
                if (debug) Puts("Flag marker already in dictionary");
            }
        }
        #endregion

        #region DataManagement

        class ConfigData
        {
            public Dictionary<ulong, HashSet<int[]>> FlagiData = new Dictionary<ulong, HashSet<int[]>>();
            public Dictionary<ulong, List<string>> QuadrantsData = new Dictionary<ulong, List<string>>();
            public Dictionary<string, Vector3> PolesLocationData = new Dictionary<string, Vector3>();
            public Dictionary<ulong, int> GatherRateData = new Dictionary<ulong, int>();
            public Dictionary<ulong, ulong> TeamListData = new Dictionary<ulong, ulong>();
        }

        private void LoadData()
        {
            quadrants.Clear();
            flagi.Clear();
            gatherMultiplier.Clear();
            teamList.Clear();
            poles.Clear();
            try
            {
                if (debug) Puts("A try to use existing configuration");
                configData = data.ReadObject<ConfigData>();
                quadrants = configData.QuadrantsData;
                flagi = configData.FlagiData;
                gatherMultiplier = configData.GatherRateData;
                teamList = configData.TeamListData;
                poles = configData.PolesLocationData;
                if (quadrants.Count > 0)
                {
                    foreach (var marker in UnityEngine.Object.FindObjectsOfType<MapMarkerGenericRadius>())
                    {
                        DeleteMarkerFromMap(marker);
                    }
                    foreach (var quadrat in quadrants)
                    {
                        foreach (var zone in quadrat.Value)
                        {
                            var zonelocation = (Vector3)ZoneManager.Call("GetZoneLocation", zone);
                            SpawnMarkerOnMap(zonelocation, zone);
                            SpawnFlag(zonelocation, zone, quadrat.Key);
                        }
                    }

                }
            }
            catch
            {
                if (debug) Puts("Existing configuration not found. Create a new configuration.");
                configData = new ConfigData();
                quadrants = configData.QuadrantsData;
                flagi = configData.FlagiData;
                gatherMultiplier = configData.GatherRateData;
                teamList = configData.TeamListData;
                poles = configData.PolesLocationData;
            }

        }

        private void SaveData()
        {
            data = Interface.Oxide.DataFileSystem.GetDatafile("LandLord.Data");
            data.WriteObject(configData);
        }

        #endregion

        #region ChatCommands
        [ChatCommand("lord")]
        private void CmdChatLordStats(BasePlayer player, string command, string[] args)
        {
            ulong teampid;
            if (teamList.ContainsKey(player.userID))
            {
                teampid = teamList[player.userID];
            }
            else teampid = player.userID;

            if (gatherMultiplier.ContainsKey(teampid) && gatherMultiplier[teampid] > 0)
            {
                player.ChatMessage("<color=orange>LANDLORD:</color> Grounds seized: " + gatherMultiplier[teampid] + " TeamID is: " + teampid);
            }
            else player.ChatMessage("<color=orange>LANDLORD:</color> Grounds seized: 0 TeamID is: " + teampid);
        }
        [ChatCommand("lordclr")]
        private void CmdChatLordCls(BasePlayer player, string command, string[] args)
        {
            if (teamList.ContainsKey(player.userID))
            {
                teamList.Remove(player.userID);
                player.ChatMessage("<color=orange>LANDLORD:</color> Team clear: Success!");
                LordTeamInit(player);
            }
            else
            {
                player.ChatMessage("<color=orange>LANDLORD:</color> You are have no team!");
            }
        }
        #endregion
    }
}