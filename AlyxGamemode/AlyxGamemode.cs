using Fleck;
using HLVRCoOpModCore;
using HLVRCoOpMod;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace AlyxGamemode
{
    public class AlyxGamemode : CoreGamemode
    {
        public AlyxGamemode() : base()
        {
            Author = "Dayshower";
            Name = "Half-Life: Alyx";
            Description = "Play Half-Life: Alyx with up to 16 players!";
        }
        public AlyxGamemode(GamemodeHandleType type, params object[]? vs)
        {
            int APIVersion = 4;
            State = HandleState.Continue;
            bool overrideState = false;
            Random rnd = new Random();
            try
            {
                switch (type)
                {
                    case GamemodeHandleType.ClientOpen:
                        if (vs != null)
                        {
                            List<IndexedClient> openConnections = (List<IndexedClient>)vs[0];
                            IWebSocketConnection openSocket = (IWebSocketConnection)vs[1];
                            Player? player2 = AlyxGlobalData.instance.GetPlayer(openSocket.ConnectionInfo.Id);
                            Response enableAddon = new("command", "addon_enable hlvr_coop");
                            openSocket.Send(JsonConvert.SerializeObject(enableAddon));
                        }
                        break;
                    case GamemodeHandleType.ClientClose:
                        if (vs != null)
                        {
                            List<IndexedClient> closeConnections = (List<IndexedClient>)vs[0];
                            IWebSocketConnection closeSocket = (IWebSocketConnection)vs[1];
                            Player? player = AlyxGlobalData.instance.GetPlayer(closeSocket.ConnectionInfo.Id);
                            if (player != null)
                            {
                                AlyxGlobalData.instance.RemovePlayer(closeSocket.ConnectionInfo.Id);
                            }
                        }
                        break;
                    case GamemodeHandleType.PreResponse:
                        if (vs != null)
                        {
                            Response response = (Response)vs[0];
                            List<IndexedClient> connections = (List<IndexedClient>)vs[1];
                            IWebSocketConnection socket = (IWebSocketConnection)vs[2];
                            string map = (string)vs[3];
                            switch (response.type)
                            {
                                case "print":
                                    if (response.data != null)
                                    {
                                        if (response.data.Contains("has joined the game"))
                                        {
                                            Response vconsoleInput = new("command", "ent_remove hlvrcoop_script;sv_cheats 1;echo INIT HLVRCOOP");
                                            socket.Send(JsonConvert.SerializeObject(vconsoleInput));
                                            if (AlyxGlobalData.instance.GetPlayer(socket.ConnectionInfo.Id) == null)
                                            {
                                                foreach (IndexedClient client in connections)
                                                {
                                                    if (client.Session.ConnectionInfo.Id == socket.ConnectionInfo.Id)
                                                    {
                                                        Player? player = AlyxGlobalData.instance.AddPlayer(client);
                                                        if (player != null)
                                                        {
                                                            Response output2 = new("status", "♩ " + client.Username + " joined as index " + player.Index);
                                                            Response blip = new("command", "play hlvrcoop/blip" + rnd.Next(1, 4));
                                                            connections.ForEach(c =>
                                                            {
                                                                if (c.Session.ConnectionInfo.Id != client.Session.ConnectionInfo.Id)
                                                                {
                                                                    c.Session.Send(JsonConvert.SerializeObject(blip));
                                                                    c.Session.Send(JsonConvert.SerializeObject(output2));
                                                                }
                                                            });
                                                        }
                                                        else
                                                        {
                                                            Response output2 = new("status", "The server is currently at maximum capacity, please try again later.");
                                                            socket.Send(JsonConvert.SerializeObject(output2));
                                                            socket.Close();
                                                        }
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                        else if (response.data.Contains("HLVRCOOP"))
                                        {
                                            List<string> packetList = response.data.Split(" ").ToList();
                                            string packetType = packetList[0];
                                            packetList.Remove(packetType);
                                            Packet packet = new(packetType, packetList.ToArray());
                                            if (packet.IsValid())
                                            {
                                                Player? pair = AlyxGlobalData.instance.GetPlayer(socket.ConnectionInfo.Id);
                                                if (pair != null)
                                                {
                                                    Player player = pair;
                                                    // Packet processing switch statement
                                                    // All packets are sent from game-side Lua scripts via print() with "HLVRCOOP" tag
                                                    // See Packet.cs for packet type definitions and documentation
                                                    switch (packet.type)
                                                    {
                                                        case PacketType.Spawn:
                                                            // Entity spawn notification (e.g., item pickup)
                                                            // Format: SPWN <classname> <name> <x> <y> <z> HLVRCOOP
                                                            if (packet.args.Length >= 5)
                                                            {
                                                                try
                                                                {
                                                                    Response spawn = new("command", "ent_create " + packet.args[0] + " {targetname \"" + packet.args[1] + "\" origin \"" + packet.args[2] + " " + packet.args[3] + " " + packet.args[4] + "\"}");
                                                                    foreach (IndexedClient broadcastClient2 in connections)
                                                                    {
                                                                        Player? keyValuePair = AlyxGlobalData.instance.GetPlayer(broadcastClient2.Session.ConnectionInfo.Id);
                                                                        if (keyValuePair != null && broadcastClient2.Session.ConnectionInfo.Id != socket.ConnectionInfo.Id)
                                                                        {
                                                                            broadcastClient2.Session.Send(JsonConvert.SerializeObject(spawn));
                                                                        }
                                                                    }
                                                                }
                                                                catch (Exception ex)
                                                                {
                                                                    ErrorLogger.LogError("AlyxGamemode.Spawn", ex);
                                                                }
                                                            }
                                                            break;
                                                        case PacketType.Teleport:
                                                            // Teleport command (trigger_teleport output)
                                                            // Format: TELE <x> <y> <z> <pitch> <yaw> <roll> HLVRCOOP
                                                            if (packet.args.Length >= 6)
                                                            {
                                                                try
                                                                {
                                                                    Vector TeleOrigin = new(
                                                                        float.Parse(packet.args[0], CultureInfo.InvariantCulture.NumberFormat),
                                                                        float.Parse(packet.args[1], CultureInfo.InvariantCulture.NumberFormat),
                                                                        float.Parse(packet.args[2], CultureInfo.InvariantCulture.NumberFormat)
                                                                    );
                                                                    Angle TeleAngles = new(
                                                                        float.Parse(packet.args[3], CultureInfo.InvariantCulture.NumberFormat),
                                                                        float.Parse(packet.args[4], CultureInfo.InvariantCulture.NumberFormat),
                                                                        float.Parse(packet.args[5], CultureInfo.InvariantCulture.NumberFormat)
                                                                    );
                                                                    Response teleport = new("command", "hlvrcoop_teleportangles " + TeleOrigin.X + " " + TeleOrigin.Y + " " + TeleOrigin.Z + " " + TeleAngles.Pitch + " " + TeleAngles.Yaw + " " + TeleAngles.Roll);
                                                                    foreach (IndexedClient broadcastClient2 in connections)
                                                                    {
                                                                        Player? keyValuePair = AlyxGlobalData.instance.GetPlayer(broadcastClient2.Session.ConnectionInfo.Id);
                                                                        if (keyValuePair != null && broadcastClient2.Session.ConnectionInfo.Id != socket.ConnectionInfo.Id)
                                                                        {
                                                                            broadcastClient2.Session.Send(JsonConvert.SerializeObject(teleport));
                                                                        }
                                                                    }
                                                                }
                                                                catch (Exception ex)
                                                                {
                                                                    ErrorLogger.LogError("AlyxGamemode.Teleport", ex);
                                                                }
                                                            }
                                                            break;
                                                        case PacketType.HeadPosAng:
                                                            // Headset position and angles (sent every tick)
                                                            // Format: HEAD <x> <y> <z> <pitch> <yaw> <roll> HLVRCOOP
                                                            if (packet.args.Length >= 6)
                                                            {
                                                                try
                                                                {
                                                                    Vector HeadOrigin = new(
                                                                        float.Parse(packet.args[0], CultureInfo.InvariantCulture.NumberFormat),
                                                                        float.Parse(packet.args[1], CultureInfo.InvariantCulture.NumberFormat),
                                                                        float.Parse(packet.args[2], CultureInfo.InvariantCulture.NumberFormat)
                                                                    );
                                                                    Angle HeadAngles = new(
                                                                        float.Parse(packet.args[3], CultureInfo.InvariantCulture.NumberFormat),
                                                                        float.Parse(packet.args[4], CultureInfo.InvariantCulture.NumberFormat),
                                                                        float.Parse(packet.args[5], CultureInfo.InvariantCulture.NumberFormat)
                                                                    );
                                                                    Vector HatOrigin = new(
                                                                        HeadOrigin.X,
                                                                        HeadOrigin.Y,
                                                                        HeadOrigin.Z + 10
                                                                    );
                                                                    Vector HatAngles = new(
                                                                        0,
                                                                        HeadAngles.Yaw + 90,
                                                                        90
                                                                    );
                                                                    Response movement = new("command", "hlvrcoop_setlocation_nonuuid hlvrcoop_head_" + player.Index + " " + HeadOrigin + " " + HeadAngles);
                                                                    Response hatMovement = new("command", "hlvrcoop_setlocation_nonuuid hlvrcoop_text_" + player.Index + " " + HatOrigin + " " + HatAngles);
                                                                    Response headText = new("command", "ent_fire hlvrcoop_text_" + player.Index + " setmessage \"" + player.Client.Username + "\"");
                                                                    foreach (IndexedClient broadcastClient2 in connections)
                                                                    {
                                                                        Player? keyValuePair = AlyxGlobalData.instance.GetPlayer(broadcastClient2.Session.ConnectionInfo.Id);
                                                                        if (keyValuePair != null && broadcastClient2.Session.ConnectionInfo.Id != socket.ConnectionInfo.Id)
                                                                        {
                                                                            broadcastClient2.Session.Send(JsonConvert.SerializeObject(movement));
                                                                            broadcastClient2.Session.Send(JsonConvert.SerializeObject(hatMovement));
                                                                            broadcastClient2.Session.Send(JsonConvert.SerializeObject(headText));
                                                                        }
                                                                    }
                                                                }
                                                                catch (Exception ex)
                                                                {
                                                                    ErrorLogger.LogError("AlyxGamemode.HeadPosAng", ex);
                                                                }
                                                            }
                                                            break;
                                                        case PacketType.HandPosAng:
                                                            // Left and right hand positions and angles (sent every tick)
                                                            // Format: HAND <lx> <ly> <lz> <lpitch> <lyaw> <lroll> <rx> <ry> <rz> <rpitch> <ryaw> <rroll> HLVRCOOP
                                                            if (packet.args.Length >= 12)
                                                            {
                                                                try
                                                                {
                                                                    Vector LeftHandOrigin = new(
                                                                        float.Parse(packet.args[0], CultureInfo.InvariantCulture.NumberFormat),
                                                                        float.Parse(packet.args[1], CultureInfo.InvariantCulture.NumberFormat),
                                                                        float.Parse(packet.args[2], CultureInfo.InvariantCulture.NumberFormat)
                                                                    );
                                                                    Angle LeftHandAngles = new(
                                                                        float.Parse(packet.args[3], CultureInfo.InvariantCulture.NumberFormat),
                                                                        float.Parse(packet.args[4], CultureInfo.InvariantCulture.NumberFormat),
                                                                        float.Parse(packet.args[5], CultureInfo.InvariantCulture.NumberFormat)
                                                                    );
                                                                    Vector RightHandOrigin = new(
                                                                        float.Parse(packet.args[6], CultureInfo.InvariantCulture.NumberFormat),
                                                                        float.Parse(packet.args[7], CultureInfo.InvariantCulture.NumberFormat),
                                                                        float.Parse(packet.args[8], CultureInfo.InvariantCulture.NumberFormat)
                                                                    );
                                                                    Angle RightHandAngles = new(
                                                                        float.Parse(packet.args[9], CultureInfo.InvariantCulture.NumberFormat),
                                                                        float.Parse(packet.args[10], CultureInfo.InvariantCulture.NumberFormat),
                                                                        float.Parse(packet.args[11], CultureInfo.InvariantCulture.NumberFormat)
                                                                    );
                                                                    Response movementLeftHand = new("command", "hlvrcoop_setlocation_nonuuid hlvrcoop_lefthand_" + player.Index + " " + LeftHandOrigin + " " + LeftHandAngles);
                                                                    Response movementRightHand = new("command", "hlvrcoop_setlocation_nonuuid hlvrcoop_righthand_" + player.Index + " " + RightHandOrigin + " " + RightHandAngles);
                                                                    foreach (IndexedClient broadcastClient2 in connections)
                                                                    {
                                                                        Player? keyValuePair = AlyxGlobalData.instance.GetPlayer(broadcastClient2.Session.ConnectionInfo.Id);
                                                                        if (keyValuePair != null && broadcastClient2.Session.ConnectionInfo.Id != socket.ConnectionInfo.Id)
                                                                        {
                                                                            broadcastClient2.Session.Send(JsonConvert.SerializeObject(movementLeftHand));
                                                                            broadcastClient2.Session.Send(JsonConvert.SerializeObject(movementRightHand));
                                                                        }
                                                                    }
                                                                }
                                                                catch (Exception ex)
                                                                {
                                                                    ErrorLogger.LogError("AlyxGamemode.HandPosAng", ex);
                                                                }
                                                            }
                                                            break;
                                                        case PacketType.PlayerPosAng:
                                                            if (packet.args.Length >= 7)
                                                            {
                                                                try
                                                                {
                                                                    // Parse player body position and angles
                                                                    Vector playerOrigin = new(
                                                                        float.Parse(packet.args[0], CultureInfo.InvariantCulture.NumberFormat),
                                                                        float.Parse(packet.args[1], CultureInfo.InvariantCulture.NumberFormat),
                                                                        float.Parse(packet.args[2], CultureInfo.InvariantCulture.NumberFormat)
                                                                    );
                                                                    Angle playerAngles = new(
                                                                        float.Parse(packet.args[3], CultureInfo.InvariantCulture.NumberFormat),
                                                                        float.Parse(packet.args[4], CultureInfo.InvariantCulture.NumberFormat),
                                                                        float.Parse(packet.args[5], CultureInfo.InvariantCulture.NumberFormat)
                                                                    );
                                                                    int playerHealth = int.Parse(packet.args[6]);
                                                                    
                                                                    // Update player state
                                                                    player.Origin = playerOrigin;
                                                                    player.Angles = playerAngles;
                                                                    
                                                                    // Note: Body position is tracked for internal use (respawn, checkpoints, etc.)
                                                                    // Body position is not broadcasted to other clients as head/hand positions
                                                                    // are already synchronized and visible
                                                                    // Health is tracked for potential use in respawn logic, checkpoints, etc.
                                                                }
                                                                catch (Exception ex)
                                                                {
                                                                    ErrorLogger.LogError("AlyxGamemode.PlayerPosAng", ex);
                                                                }
                                                            }
                                                            break;
                                                        case PacketType.Initialization:
                                                            Response vconsoleInput2 = new("command", "ent_create logic_script {targetname hlvrcoop_script vscripts hlvrcoop_interval};ent_create logic_timer {targetname hlvrcoop_timer refiretime 0.01};echo IENT HLVRCOOP");
                                                            Response output3 = new("status", "Initializing co-op...");
                                                            socket.Send(JsonConvert.SerializeObject(output3));
                                                            socket.Send(JsonConvert.SerializeObject(vconsoleInput2));
                                                            break;
                                                        case PacketType.InitializedEntities:
                                                            Thread thr = new(new ThreadStart(() =>
                                                            {
                                                                Thread.Sleep(2500);
                                                                Response vconsoleInput5 = new("command", "buddha 1;unpause;ent_fire hlvrcoop_timer addoutput OnTimer>hlvrcoop_script>CallScriptFunction>HLVRCoOpMod>0>-1");
                                                                Response output4 = new("status", "Co-op initialized!");
                                                                socket.Send(JsonConvert.SerializeObject(output4));
                                                                socket.Send(JsonConvert.SerializeObject(vconsoleInput5));
                                                            }));
                                                            thr.Start();
                                                            break;
                                                        // ===== UNUSED/LEGACY PACKET TYPES =====
                                                        // These packet types are defined but never sent by the game-side Lua scripts.
                                                        // They appear to be legacy code or planned features that were never implemented.
                                                        // Keeping empty handlers for backward compatibility.
                                                        case PacketType.RightHandIndexes:      // UNUSED: Not sent (entities use fixed names)
                                                        case PacketType.LeftHandIndexes:        // UNUSED: Not sent (entities use fixed names)
                                                        case PacketType.HeadsetIndexes:         // UNUSED: Not sent (entities use fixed names)
                                                        case PacketType.TextIndexes:            // UNUSED: Not sent (entities use fixed names)
                                                        case PacketType.ColliderIndexes:        // UNUSED: Not sent
                                                        case PacketType.Prefix:                 // UNUSED: Not sent (unknown purpose)
                                                        case PacketType.Alive:                  // UNUSED: Not sent (health tracked via PLYR)
                                                        case PacketType.ColliderDamage:         // UNUSED: Not sent
                                                        case PacketType.PhysicsObjectIndexStartPos: // UNUSED: Not sent (handled via entity sync)
                                                            // No-op: These packets are never sent, so no handling is needed
                                                            break;
                                                        case PacketType.PhysicsObjectPosAng:
                                                            // Physics object position and angles update
                                                            // Format: PHYS <name> <x> <y> <z> <pitch> <yaw> <roll> HLVRCOOP
                                                            if (packet.args.Length >= 7)
                                                            {
                                                                try
                                                                {
                                                                    Entity entity = new(packet.args[0], float.Parse(packet.args[1], CultureInfo.InvariantCulture.NumberFormat), float.Parse(packet.args[2], CultureInfo.InvariantCulture.NumberFormat), float.Parse(packet.args[3], CultureInfo.InvariantCulture.NumberFormat), float.Parse(packet.args[4], CultureInfo.InvariantCulture.NumberFormat), float.Parse(packet.args[5], CultureInfo.InvariantCulture.NumberFormat), float.Parse(packet.args[6], CultureInfo.InvariantCulture.NumberFormat));
                                                                    if (AlyxGlobalData.instance.AddManipulatedEntity(entity, socket.ConnectionInfo.Id, connections))
                                                                    {
                                                                        Response output4 = new("command", "hlvrcoop_setlocation " + packet.args[0] + " " + packet.args[1] + " " + packet.args[2] + " " + packet.args[3] + " " + packet.args[4] + " " + packet.args[5] + " " + packet.args[6]);
                                                                        foreach (IndexedClient broadcastClient2 in connections)
                                                                        {
                                                                            Player? keyValuePair = AlyxGlobalData.instance.GetPlayer(broadcastClient2.Session.ConnectionInfo.Id);
                                                                            if (keyValuePair != null && broadcastClient2.Session.ConnectionInfo.Id != socket.ConnectionInfo.Id)
                                                                            {
                                                                                broadcastClient2.Session.Send(JsonConvert.SerializeObject(output4));
                                                                            }
                                                                        }
                                                                    }
                                                                }
                                                                catch (Exception ex)
                                                                {
                                                                    ErrorLogger.LogError("AlyxGamemode.PhysicsObjectPosAng", ex);
                                                                }
                                                            }
                                                            break;
                                                        case PacketType.MapName:
                                                            // Map name and API version detection
                                                            // Format: MAPN <mapname> <apiversion> HLVRCOOP
                                                            if (packet.args.Length >= 2)
                                                            {
                                                                try
                                                                {
                                                                    string suffix = "";
                                                                    /*
                                                                    if (packet.args[0] != player.Client.Map)
                                                                    {
                                                                        Response mapOutput = new("command", "addon_play " + packet.args[0] + "; addon_tools_map " + packet.args[0]);
                                                                        foreach (IndexedClient broadcast in connections)
                                                                        {
                                                                            Player? keyValuePair = AlyxGlobalData.instance.GetPlayer(broadcast.Session.ConnectionInfo.Id);
                                                                            if (keyValuePair != null && broadcast.Session.ConnectionInfo.Id != socket.ConnectionInfo.Id)
                                                                            {
                                                                                broadcast.Session.Send(JsonConvert.SerializeObject(mapOutput));
                                                                            }
                                                                        }
                                                                        suffix = " - Telling all clients to switch...";
                                                                    }
                                                                    */
                                                                    Response jingle = new("command", "play sounds/hlvrcoop/jingle_up2.vsnd");
                                                                    Response output2 = new("status", "♫ Detected map: " + packet.args[0] + suffix);
                                                                    socket.Send(JsonConvert.SerializeObject(jingle));
                                                                    socket.Send(JsonConvert.SerializeObject(output2));
                                                                    int gamemodeAPIVersion = int.Parse(packet.args[1]);
                                                                    if (gamemodeAPIVersion != APIVersion)
                                                                    {
                                                                        Response versionOutput = new("status", "Version mismatch! Client reports API version " + gamemodeAPIVersion + ", server expects API version " + APIVersion + ". Please update the client and respective Workshop addons.");
                                                                        socket.Send(JsonConvert.SerializeObject(versionOutput));
                                                                    }
                                                                    Map.map = packet.args[0];
                                                                }
                                                                catch (Exception ex)
                                                                {
                                                                    ErrorLogger.LogError("AlyxGamemode.MapName", ex);
                                                                }
                                                            }
                                                            break;
                                                        case PacketType.ButtonIndexStartPos:     // UNUSED: Not sent
                                                        case PacketType.ButtonPressIndex:       // UNUSED: Not sent (button presses use FIRE packet)
                                                        case PacketType.DoorIndexStartPos:      // UNUSED: Not sent (doors handled via PHYS packet)
                                                        case PacketType.TriggerIndexStartPos:    // UNUSED: Not sent (handled via entity sync)
                                                        case PacketType.TriggerActivateIndex:    // UNUSED: Not sent (trigger activations use FIRE packet)
                                                            // No-op: These packets are never sent, so no handling is needed
                                                            break;
                                                        case PacketType.BrokenProp:
                                                            // Broken prop notification
                                                            // Format: BRAK <name> HLVRCOOP
                                                            if (packet.args.Length >= 1)
                                                            {
                                                                try
                                                                {
                                                                    Response removeBreak = new("command", "hlvrcoop_break " + packet.args[0]);
                                                                    foreach (IndexedClient broadcast in connections)
                                                                    {
                                                                        Player? keyValuePair = AlyxGlobalData.instance.GetPlayer(broadcast.Session.ConnectionInfo.Id);
                                                                        if (keyValuePair != null && broadcast.Session.ConnectionInfo.Id != socket.ConnectionInfo.Id)
                                                                        {
                                                                            Response breakprop = new("status", "Broken prop: " + packet.args[0]);
                                                                            broadcast.Session.Send(JsonConvert.SerializeObject(removeBreak));
                                                                        }
                                                                    }
                                                                }
                                                                catch (Exception ex)
                                                                {
                                                                    ErrorLogger.LogError("AlyxGamemode.BrokenProp", ex);
                                                                }
                                                            }
                                                            break;
                                                        case PacketType.EntityRemoved:
                                                            // Entity removed notification
                                                            // Format: EREM <name> HLVRCOOP
                                                            if (packet.args.Length >= 1)
                                                            {
                                                                try
                                                                {
                                                                    Response remove = new("command", "ent_remove " + packet.args[0]);
                                                                    foreach (IndexedClient broadcast in connections)
                                                                    {
                                                                        Player? keyValuePair = AlyxGlobalData.instance.GetPlayer(broadcast.Session.ConnectionInfo.Id);
                                                                        if (keyValuePair != null && broadcast.Session.ConnectionInfo.Id != socket.ConnectionInfo.Id)
                                                                        {
                                                                            broadcast.Session.Send(JsonConvert.SerializeObject(remove));
                                                                        }
                                                                    }
                                                                }
                                                                catch (Exception ex)
                                                                {
                                                                    ErrorLogger.LogError("AlyxGamemode.EntityRemoved", ex);
                                                                }
                                                            }
                                                            break;
                                                        case PacketType.EntityFired:
                                                            // Entity output fired (trigger, button, etc.)
                                                            // Format: FIRE <name> <output> HLVRCOOP
                                                            if (packet.args.Length >= 2)
                                                            {
                                                                try
                                                                {
                                                                    Entity entityfired = new(packet.args[0]);
                                                                    Response fire = new("command", "hlvrcoop_fireoutput " + packet.args[0] + " " + packet.args[1]);
                                                                    foreach (IndexedClient broadcast in connections)
                                                                    {
                                                                        Player? keyValuePair = AlyxGlobalData.instance.GetPlayer(broadcast.Session.ConnectionInfo.Id);
                                                                        if (keyValuePair != null && broadcast.Session.ConnectionInfo.Id != socket.ConnectionInfo.Id)
                                                                        {
                                                                            broadcast.Session.Send(JsonConvert.SerializeObject(fire));
                                                                        }
                                                                    }
                                                                }
                                                                catch (Exception ex)
                                                                {
                                                                    ErrorLogger.LogError("AlyxGamemode.EntityFired", ex);
                                                                }
                                                            }
                                                            break;
                                                        case PacketType.NPCHealth:
                                                            // NPC health update
                                                            // Format: NPHP <name> <health> HLVRCOOP
                                                            if (packet.args.Length >= 2)
                                                            {
                                                                try
                                                                {
                                                                    Response p = new("command", "hlvrcoop_npc_sethealth " + packet.args[0] + " " + packet.args[1]);
                                                                    connections.ForEach(c => c.Session.Send(JsonConvert.SerializeObject(p)));
                                                                }
                                                                catch (Exception ex)
                                                                {
                                                                    ErrorLogger.LogError("AlyxGamemode.NPCHealth", ex);
                                                                }
                                                            }
                                                            break;
                                                        case PacketType.HLVRCOOPCommand:
                                                            // Custom HLVRCoop command (converted to chat)
                                                            // Format: CMND <command> HLVRCOOP
                                                            try
                                                            {
                                                                response.type = "chat";
                                                                response.data = "/" + string.Join(" ", packet.args.Take(packet.args.Length - 1));
                                                                State = HandleState.Continue;
                                                                overrideState = true;
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                ErrorLogger.LogError("AlyxGamemode.HLVRCOOPCommand", ex);
                                                            }
                                                            break;
                                                        case PacketType.TemplateEntity:
                                                            // Template entity cache request (point_template)
                                                            // Format: TENT <name> HLVRCOOP
                                                            if (packet.args.Length >= 1)
                                                            {
                                                                try
                                                                {
                                                                    Response templateCache = new("command", "hlvrcoop_cache_entity " + packet.args[0]);
                                                                    foreach (IndexedClient broadcast in connections)
                                                                    {
                                                                        Player? keyValuePair = AlyxGlobalData.instance.GetPlayer(broadcast.Session.ConnectionInfo.Id);
                                                                        if (keyValuePair != null && broadcast.Session.ConnectionInfo.Id != socket.ConnectionInfo.Id)
                                                                        {
                                                                            broadcast.Session.Send(JsonConvert.SerializeObject(templateCache));
                                                                        }
                                                                    }
                                                                }
                                                                catch (Exception ex)
                                                                {
                                                                    ErrorLogger.LogError("AlyxGamemode.TemplateEntity", ex);
                                                                }
                                                            }
                                                            break;
                                                    }
                                                }
                                                if(!overrideState)
                                                    State = HandleState.Handled;
                                            }
                                        }
                                    }
                                    break;
                            }
                        }
                        break;
                    case GamemodeHandleType.PostResponse:
                        if (vs != null)
                        {
                            Response response = (Response)vs[0];
                            List<IndexedClient> connections = (List<IndexedClient>)vs[1];
                            IWebSocketConnection socket = (IWebSocketConnection)vs[2];
                            switch (response.type)
                            {
                                case "chat":
                                    if (AlyxGlobalData.instance.GetPlayer(socket.ConnectionInfo.Id) != null)
                                    {
                                        if (response.data != null)
                                        {
                                            if (!response.data.StartsWith("/"))
                                            {
                                                Response output = new("command", "play sounds/ui/hint.vsnd");
                                                connections.ForEach(c => c.Session.Send(JsonConvert.SerializeObject(output)));
                                            }
                                        }
                                    }
                                    break;
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("AlyxGamemode", ex);
            }
        }
    }
}