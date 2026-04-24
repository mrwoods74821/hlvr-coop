using Fleck;
using HLVRCoOpModCore;
using HLVRCoOpMod;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Drawing;
using System.Threading;

namespace AlyxGamemode
{
    public class AlyxGamemode : CoreGamemode
    {
        private const int APIVersion = 5;
        private static readonly Dictionary<Guid, uint> clientSequenceTracker = new();
        private static readonly object seqLock = new();

        private static void SyncLog(string message)
        {
            try
            {
                ErrorLogger.LogSync(message);
            }
            catch
            {
                // Best-effort diagnostics; never break gameplay flow
            }
        }

        private static void SendBootstrap(IWebSocketConnection socket, string username)
        {
            Response bootstrapCommand = new("command", "ent_remove hlvrcoop_script;sv_cheats 1;ent_create logic_script {targetname hlvrcoop_script vscripts hlvrcoop_interval};ent_create logic_timer {targetname hlvrcoop_timer refiretime 0.01};echo IENT HLVRCOOP");
            Response bootstrapStatus = new("status", "Initializing co-op...");
            socket.Send(JsonConvert.SerializeObject(bootstrapStatus));
            socket.Send(JsonConvert.SerializeObject(bootstrapCommand));
            AlyxGlobalData.instance.UpsertSession(socket.ConnectionInfo.Id, username, AlyxGlobalData.ConnectionState.Initializing);
            SyncLog($"{username}: script bootstrap sent");
        }

        private static void BroadcastToOthers(List<IndexedClient> connections, IWebSocketConnection originator, Response message)
        {
            string serialized = JsonConvert.SerializeObject(message);
            foreach (IndexedClient client in connections)
            {
                Player? player = AlyxGlobalData.instance.GetPlayer(client.Session.ConnectionInfo.Id);
                if (player != null && client.Session.ConnectionInfo.Id != originator.ConnectionInfo.Id)
                {
                    client.Session.Send(serialized);
                }
            }
        }

        private static bool IsMovementPacket(PacketType type)
        {
            return type == PacketType.HeadPosAng
                || type == PacketType.HandPosAng
                || type == PacketType.PlayerPosAng
                || type == PacketType.PhysicsObjectPosAng;
        }

        private static bool ShouldProcessPacket(Packet packet, IWebSocketConnection socket)
        {
            if (!IsMovementPacket(packet.type) || !packet.SequenceId.HasValue)
                return true;

            lock (seqLock)
            {
                if (!clientSequenceTracker.TryGetValue(socket.ConnectionInfo.Id, out uint lastSeq))
                {
                    clientSequenceTracker[socket.ConnectionInfo.Id] = packet.SequenceId.Value;
                    return true;
                }

                if (packet.SequenceId.Value <= lastSeq)
                    return false;

                clientSequenceTracker[socket.ConnectionInfo.Id] = packet.SequenceId.Value;
                return true;
            }
        }

        public AlyxGamemode() : base()
        {
            Author = "Dayflare";
            Name = "Half-Life: Alyx";
            Description = "Play Half-Life: Alyx with up to 16 players!";
        }
        public AlyxGamemode(GamemodeHandleType type, params object[]? vs)
        {
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
                            IndexedClient? openingClient = openConnections.Find(c => c.Session.ConnectionInfo.Id == openSocket.ConnectionInfo.Id);
                            string openingUsername = openingClient?.Username ?? "unknown";
                            AlyxGlobalData.instance.UpsertSession(openSocket.ConnectionInfo.Id, openingUsername, AlyxGlobalData.ConnectionState.Connected);

                            // Enable the Workshop addon first
                            Response enableAddon = new("command", "addon_enable hlvr_coop");
                            openSocket.Send(JsonConvert.SerializeObject(enableAddon));
                            AlyxGlobalData.instance.UpsertSession(openSocket.ConnectionInfo.Id, openingUsername, AlyxGlobalData.ConnectionState.Authenticated);
                            SyncLog($"{openingUsername}: addon enabled command sent");

                            // Send initialization command after a delay to allow addon to load
                            // Remote clients don't send "has joined the game" message, so we need to:
                            // 1. Add the player to the game state
                            // 2. Send initialization command to trigger game-side script
                            Task.Run(async () =>
                            {
                                await Task.Delay(2000); // Wait 2 seconds for addon to enable

                                // Check if player still doesn't exist (connection not closed)
                                if (AlyxGlobalData.instance.GetPlayer(openSocket.ConnectionInfo.Id) == null)
                                {
                                    // Find the client in connections list
                                    IndexedClient? clientToAdd = null;
                                    foreach (IndexedClient client in openConnections)
                                    {
                                        if (client.Session.ConnectionInfo.Id == openSocket.ConnectionInfo.Id)
                                        {
                                            clientToAdd = client;
                                            break;
                                        }
                                    }

                                    if (clientToAdd != null)
                                    {
                                        // Add player to game state (normally done when "has joined the game" is received)
                                        Player? player = AlyxGlobalData.instance.AddPlayer(clientToAdd);
                                        if (player != null)
                                        {
                                            // CRITICAL: Remote clients can't send INIT packet until script entities exist
                                            // Create script entities immediately (normally done when INIT packet is received)
                                            // The script will then send INIT packet automatically when it loads
                                            SendBootstrap(openSocket, clientToAdd.Username);

                                            SyncLog($"{clientToAdd.Username}: waiting for first HLVRCOOP packet before starting readiness watchdog");

                                            // Start the timer after a delay (script entities need time to create)
                                            Task.Run(async () =>
                                            {
                                                await Task.Delay(2500); // Wait for script entities to create and initialize
                                                Response vconsoleInput5 = new("command", "buddha 1;unpause;ent_fire hlvrcoop_timer addoutput OnTimer>hlvrcoop_script>CallScriptFunction>HLVRCoOpMod>0>-1");
                                                Response output4 = new("status", "Co-op initialized!");
                                                openSocket.Send(JsonConvert.SerializeObject(output4));
                                                openSocket.Send(JsonConvert.SerializeObject(vconsoleInput5));
                                            });

                                            // Notify other clients that player joined
                                            Response output2 = new("status", "♩ " + clientToAdd.Username + " joined as index " + player.Index);
                                            Response blip = new("command", "play hlvrcoop/blip" + rnd.Next(1, 4));
                                            openConnections.ForEach(c =>
                                            {
                                                if (c.Session.ConnectionInfo.Id != clientToAdd.Session.ConnectionInfo.Id)
                                                {
                                                    c.Session.Send(JsonConvert.SerializeObject(blip));
                                                    c.Session.Send(JsonConvert.SerializeObject(output2));
                                                }
                                            });
                                            SyncLog($"{clientToAdd.Username}: player object added with index {player.Index}");
                                        }
                                        else
                                        {
                                            // Server is full - disconnect client
                                            Response output2 = new("status", "The server is currently at maximum capacity, please try again later.");
                                            openSocket.Send(JsonConvert.SerializeObject(output2));
                                            openSocket.Close();
                                        }
                                    }
                                }
                            });
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
                            lock (seqLock)
                            {
                                clientSequenceTracker.Remove(closeSocket.ConnectionInfo.Id);
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
                                                AlyxGlobalData.SessionState observedState = AlyxGlobalData.instance.MarkPacketObserved(socket.ConnectionInfo.Id);

                                                if (observedState.FirstPacketUtc.HasValue && AlyxGlobalData.instance.TryStartWatchdog(socket.ConnectionInfo.Id))
                                                {
                                                    string watchdogUsername = string.IsNullOrWhiteSpace(observedState.Username) ? socket.ConnectionInfo.Id.ToString() : observedState.Username;
                                                    SyncLog($"{watchdogUsername}: first HLVRCOOP packet observed, starting readiness watchdog");

                                                    Task.Run(async () =>
                                                    {
                                                        await Task.Delay(12000);
                                                        AlyxGlobalData.SessionState? phaseState = AlyxGlobalData.instance.GetSession(socket.ConnectionInfo.Id);
                                                        if (phaseState != null && phaseState.Phase != AlyxGlobalData.ConnectionState.Ready)
                                                        {
                                                            string retryUsername = string.IsNullOrWhiteSpace(phaseState.Username) ? watchdogUsername : phaseState.Username;
                                                            AlyxGlobalData.SessionState retryState = AlyxGlobalData.instance.IncrementBootstrapRetry(socket.ConnectionInfo.Id);
                                                            if (retryState.BootstrapRetryCount <= 1)
                                                            {
                                                                SyncLog($"[RETRY] {retryUsername}: readiness timeout at phase {phaseState.Phase}, retrying bootstrap once");
                                                                SendBootstrap(socket, retryUsername);

                                                                await Task.Delay(10000);
                                                                AlyxGlobalData.SessionState? phaseStateAfterRetry = AlyxGlobalData.instance.GetSession(socket.ConnectionInfo.Id);
                                                                if (phaseStateAfterRetry != null && phaseStateAfterRetry.Phase != AlyxGlobalData.ConnectionState.Ready)
                                                                {
                                                                    SyncLog($"[FAIL] {retryUsername}: not ready after retry, phase={phaseStateAfterRetry.Phase}, dropped={phaseStateAfterRetry.DroppedPacketCount}, reason={phaseStateAfterRetry.LastDropReason}");
                                                                    Response failStatus = new("status", "Co-op initialization is taking too long. Please reconnect if synchronization does not start.");
                                                                    socket.Send(JsonConvert.SerializeObject(failStatus));
                                                                }
                                                            }
                                                        }
                                                    });
                                                }

                                                Player? pair = AlyxGlobalData.instance.GetPlayer(socket.ConnectionInfo.Id);
                                                if (pair != null)
                                                {
                                                    Player player = pair;
                                                    if (ShouldProcessPacket(packet, socket))
                                                    {
                                                        switch (packet.type)
                                                        {
                                                            case PacketType.Spawn:
                                                                HandleSpawnPacket(player, packet, socket, connections);
                                                                break;
                                                            case PacketType.Respawn:
                                                                HandleRespawnPacket(player, packet, socket, connections);
                                                                break;
                                                            case PacketType.Teleport:
                                                                HandleTeleportPacket(player, packet, socket, connections);
                                                                break;
                                                            case PacketType.HeadPosAng:
                                                                HandleHeadPosAngPacket(player, packet, socket, connections);
                                                                break;
                                                            case PacketType.HandPosAng:
                                                                HandleHandPosAngPacket(player, packet, socket, connections);
                                                                break;
                                                            case PacketType.PlayerPosAng:
                                                                HandlePlayerPosAngPacket(player, packet, socket, connections);
                                                                break;
                                                            case PacketType.Initialization:
                                                                HandleInitializationPacket(player, packet, socket, connections);
                                                                break;
                                                            case PacketType.InitializedEntities:
                                                                HandleInitializedEntitiesPacket(player, packet, socket, connections);
                                                                break;
                                                            case PacketType.RightHandIndexes:
                                                            case PacketType.LeftHandIndexes:
                                                            case PacketType.HeadsetIndexes:
                                                            case PacketType.TextIndexes:
                                                            case PacketType.ColliderIndexes:
                                                            case PacketType.Prefix:
                                                            case PacketType.Alive:
                                                            case PacketType.ColliderDamage:
                                                            case PacketType.PhysicsObjectIndexStartPos:
                                                            case PacketType.ButtonIndexStartPos:
                                                            case PacketType.ButtonPressIndex:
                                                            case PacketType.DoorIndexStartPos:
                                                            case PacketType.TriggerIndexStartPos:
                                                            case PacketType.TriggerActivateIndex:
                                                                // No-op: These packets are never sent, so no handling is needed
                                                                break;
                                                            case PacketType.PhysicsObjectPosAng:
                                                                HandlePhysicsObjectPosAngPacket(player, packet, socket, connections);
                                                                break;
                                                            case PacketType.MapName:
                                                                HandleMapNamePacket(player, packet, socket, connections);
                                                                break;
                                                            case PacketType.BrokenProp:
                                                                HandleBrokenPropPacket(player, packet, socket, connections);
                                                                break;
                                                            case PacketType.EntityRemoved:
                                                                HandleEntityRemovedPacket(player, packet, socket, connections);
                                                                break;
                                                            case PacketType.EntityFired:
                                                                HandleEntityFiredPacket(player, packet, socket, connections);
                                                                break;
                                                            case PacketType.NPCHealth:
                                                                HandleNPCHealthPacket(player, packet, socket, connections);
                                                                break;
                                                            case PacketType.HLVRCOOPCommand:
                                                                overrideState = HandleHLVRCOOPCommandPacket(player, packet, socket, connections, ref response);
                                                                break;
                                                            case PacketType.TemplateEntity:
                                                                HandleTemplateEntityPacket(player, packet, socket, connections);
                                                                break;
                                                            case PacketType.Crafting:
                                                                HandleCraftingPacket(player, packet, socket, connections);
                                                                break;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    string unknownName = observedState.Username;
                                                    AlyxGlobalData.SessionState dropped = AlyxGlobalData.instance.MarkPacketDropped(socket.ConnectionInfo.Id, "Player object not initialized when HLVRCOOP packet arrived");
                                                    if (dropped.DroppedPacketCount == 1 || dropped.DroppedPacketCount % 25 == 0)
                                                    {
                                                        SyncLog($"[DROP] {(string.IsNullOrWhiteSpace(unknownName) ? socket.ConnectionInfo.Id.ToString() : unknownName)} dropped={dropped.DroppedPacketCount} reason={dropped.LastDropReason}");
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

        #region Packet Handlers

        private static void HandleSpawnPacket(Player player, Packet packet, IWebSocketConnection socket, List<IndexedClient> connections)
        {
            if (packet.args.Length >= 5)
            {
                try
                {
                    Response spawn = new("command", "ent_create " + packet.args[0] + " {targetname \"" + packet.args[1] + "\" origin \"" + packet.args[2] + " " + packet.args[3] + " " + packet.args[4] + "\"}");
                    BroadcastToOthers(connections, socket, spawn);
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("AlyxGamemode.Spawn", ex);
                }
            }
        }

        private static void HandleRespawnPacket(Player player, Packet packet, IWebSocketConnection socket, List<IndexedClient> connections)
        {
            if (packet.args.Length >= 3)
            {
                try
                {
                    Player? respawnedPlayer = AlyxGlobalData.instance.GetPlayer(socket.ConnectionInfo.Id);
                    if (respawnedPlayer != null)
                    {
                        respawnedPlayer.Health = 100;
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("AlyxGamemode.Respawn", ex);
                }
            }
        }

        private static void HandleTeleportPacket(Player player, Packet packet, IWebSocketConnection socket, List<IndexedClient> connections)
        {
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
                    BroadcastToOthers(connections, socket, teleport);
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("AlyxGamemode.Teleport", ex);
                }
            }
        }

        private static void HandleHeadPosAngPacket(Player player, Packet packet, IWebSocketConnection socket, List<IndexedClient> connections)
        {
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
                    // Consolidated: head entity + text entity position + text message in one command
                    Response headCommand = new("command", "hlvrcoop_setlocation_head " + player.Index + " " + HeadOrigin.X + " " + HeadOrigin.Y + " " + HeadOrigin.Z + " " + HeadAngles.Pitch + " " + HeadAngles.Yaw + " " + HeadAngles.Roll + " " + HatOrigin.X + " " + HatOrigin.Y + " " + HatOrigin.Z + " |" + player.Client.Username);
                    BroadcastToOthers(connections, socket, headCommand);
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("AlyxGamemode.HeadPosAng", ex);
                }
            }
        }

        private static void HandleHandPosAngPacket(Player player, Packet packet, IWebSocketConnection socket, List<IndexedClient> connections)
        {
            if (packet.args.Length >= 12)
            {
                try
                {
                    // Consolidated: left + right hand in one command
                    Response handsCommand = new("command", "hlvrcoop_setlocation_hands " + player.Index + " " + packet.args[0] + " " + packet.args[1] + " " + packet.args[2] + " " + packet.args[3] + " " + packet.args[4] + " " + packet.args[5] + " " + packet.args[6] + " " + packet.args[7] + " " + packet.args[8] + " " + packet.args[9] + " " + packet.args[10] + " " + packet.args[11]);
                    BroadcastToOthers(connections, socket, handsCommand);
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("AlyxGamemode.HandPosAng", ex);
                }
            }
        }

        private static void HandlePlayerPosAngPacket(Player player, Packet packet, IWebSocketConnection socket, List<IndexedClient> connections)
        {
            if (packet.args.Length >= 7)
            {
                try
                {
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

                    player.Origin = playerOrigin;
                    player.Angles = playerAngles;
                    player.Health = playerHealth;
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("AlyxGamemode.PlayerPosAng", ex);
                }
            }
        }

        private static void HandleInitializationPacket(Player player, Packet packet, IWebSocketConnection socket, List<IndexedClient> connections)
        {
            AlyxGlobalData.instance.UpsertSession(socket.ConnectionInfo.Id, player.Client.Username, AlyxGlobalData.ConnectionState.Initializing);
            SyncLog($"{player.Client.Username}: INIT packet received");
            Response vconsoleInput2 = new("command", "ent_create logic_script {targetname hlvrcoop_script vscripts hlvrcoop_interval};ent_create logic_timer {targetname hlvrcoop_timer refiretime 0.01};echo IENT HLVRCOOP");
            Response output3 = new("status", "Initializing co-op...");
            socket.Send(JsonConvert.SerializeObject(output3));
            socket.Send(JsonConvert.SerializeObject(vconsoleInput2));
        }

        private static void HandleInitializedEntitiesPacket(Player player, Packet packet, IWebSocketConnection socket, List<IndexedClient> connections)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(2500);
                    if (socket.IsAvailable)
                    {
                        Response vconsoleInput5 = new("command", "buddha 1;unpause;ent_fire hlvrcoop_timer addoutput OnTimer>hlvrcoop_script>CallScriptFunction>HLVRCoOpMod>0>-1");
                        Response output4 = new("status", "Co-op initialized!");
                        socket.Send(JsonConvert.SerializeObject(output4));
                        socket.Send(JsonConvert.SerializeObject(vconsoleInput5));
                    }
                }
                catch { }
            });
            AlyxGlobalData.instance.UpsertSession(socket.ConnectionInfo.Id, player.Client.Username, AlyxGlobalData.ConnectionState.Ready);
            SyncLog($"{player.Client.Username}: session ready");
        }

        private static void HandlePhysicsObjectPosAngPacket(Player player, Packet packet, IWebSocketConnection socket, List<IndexedClient> connections)
        {
            if (packet.args.Length >= 7)
            {
                try
                {
                    Entity entity = new(packet.args[0], float.Parse(packet.args[1], CultureInfo.InvariantCulture.NumberFormat), float.Parse(packet.args[2], CultureInfo.InvariantCulture.NumberFormat), float.Parse(packet.args[3], CultureInfo.InvariantCulture.NumberFormat), float.Parse(packet.args[4], CultureInfo.InvariantCulture.NumberFormat), float.Parse(packet.args[5], CultureInfo.InvariantCulture.NumberFormat), float.Parse(packet.args[6], CultureInfo.InvariantCulture.NumberFormat));
                    if (AlyxGlobalData.instance.AddManipulatedEntity(entity, socket.ConnectionInfo.Id, connections))
                    {
                        Response output4 = new("command", "hlvrcoop_setlocation " + packet.args[0] + " " + packet.args[1] + " " + packet.args[2] + " " + packet.args[3] + " " + packet.args[4] + " " + packet.args[5] + " " + packet.args[6]);
                        BroadcastToOthers(connections, socket, output4);
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("AlyxGamemode.PhysicsObjectPosAng", ex);
                }
            }
        }

        private static void HandleMapNamePacket(Player player, Packet packet, IWebSocketConnection socket, List<IndexedClient> connections)
        {
            if (packet.args.Length >= 2)
            {
                try
                {
                    string suffix = "";
                    /*
                    if (packet.args[0] != player.Client.Map)
                    {
                        Response mapOutput = new("command", "addon_play " + packet.args[0] + "; addon_tools_map " + packet.args[0]);
                        BroadcastToOthers(connections, socket, mapOutput);
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
                        Response versionOutput = new("status", "Version mismatch! Client API version " + gamemodeAPIVersion + " != server API version " + APIVersion + ". Disconnecting. Please update the client and Workshop addons.");
                        socket.Send(JsonConvert.SerializeObject(versionOutput));
                        socket.Close();
                        SyncLog($"{player.Client.Username}: disconnected due to API version mismatch (client={gamemodeAPIVersion}, server={APIVersion})");
                    }
                    Map.map = packet.args[0];
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("AlyxGamemode.MapName", ex);
                }
            }
        }

        private static void HandleBrokenPropPacket(Player player, Packet packet, IWebSocketConnection socket, List<IndexedClient> connections)
        {
            if (packet.args.Length >= 1)
            {
                try
                {
                    AlyxGlobalData.instance.RemoveManipulatedEntity(packet.args[0]);
                    Response removeBreak = new("command", "hlvrcoop_break " + packet.args[0]);
                    Response breakprop = new("status", "Broken prop: " + packet.args[0]);
                    string serializedRemove = JsonConvert.SerializeObject(removeBreak);
                    string serializedBreak = JsonConvert.SerializeObject(breakprop);
                    foreach (IndexedClient broadcast in connections)
                    {
                        Player? keyValuePair = AlyxGlobalData.instance.GetPlayer(broadcast.Session.ConnectionInfo.Id);
                        if (keyValuePair != null && broadcast.Session.ConnectionInfo.Id != socket.ConnectionInfo.Id)
                        {
                            broadcast.Session.Send(serializedRemove);
                            broadcast.Session.Send(serializedBreak);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("AlyxGamemode.BrokenProp", ex);
                }
            }
        }

        private static void HandleEntityRemovedPacket(Player player, Packet packet, IWebSocketConnection socket, List<IndexedClient> connections)
        {
            if (packet.args.Length >= 1)
            {
                try
                {
                    AlyxGlobalData.instance.RemoveManipulatedEntity(packet.args[0]);
                    Response remove = new("command", "ent_remove " + packet.args[0]);
                    BroadcastToOthers(connections, socket, remove);
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("AlyxGamemode.EntityRemoved", ex);
                }
            }
        }

        private static void HandleEntityFiredPacket(Player player, Packet packet, IWebSocketConnection socket, List<IndexedClient> connections)
        {
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
        }

        private static void HandleNPCHealthPacket(Player player, Packet packet, IWebSocketConnection socket, List<IndexedClient> connections)
        {
            if (packet.args.Length >= 2)
            {
                try
                {
                    Response p = new("command", "hlvrcoop_npc_sethealth " + packet.args[0] + " " + packet.args[1]);
                    foreach (IndexedClient broadcast in connections)
                    {
                        Player? keyValuePair = AlyxGlobalData.instance.GetPlayer(broadcast.Session.ConnectionInfo.Id);
                        if (keyValuePair != null && broadcast.Session.ConnectionInfo.Id != socket.ConnectionInfo.Id)
                        {
                            broadcast.Session.Send(JsonConvert.SerializeObject(p));
                        }
                    }
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("AlyxGamemode.NPCHealth", ex);
                }
            }
        }

        private static bool HandleHLVRCOOPCommandPacket(Player player, Packet packet, IWebSocketConnection socket, List<IndexedClient> connections, ref Response response)
        {
            try
            {
                response.type = "chat";
                response.data = "/" + string.Join(" ", packet.args.Take(packet.args.Length - 1));
                return true; // overrideState = true, State stays Continue
            }
            catch (Exception ex)
            {
                ErrorLogger.LogError("AlyxGamemode.HLVRCOOPCommand", ex);
                return false;
            }
        }

        private static void HandleTemplateEntityPacket(Player player, Packet packet, IWebSocketConnection socket, List<IndexedClient> connections)
        {
            if (packet.args.Length >= 1)
            {
                try
                {
                    Response templateCache = new("command", "hlvrcoop_cache_entity " + packet.args[0]);
                    BroadcastToOthers(connections, socket, templateCache);
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("AlyxGamemode.TemplateEntity", ex);
                }
            }
        }

        private static void HandleCraftingPacket(Player player, Packet packet, IWebSocketConnection socket, List<IndexedClient> connections)
        {
            if (packet.args.Length >= 1)
            {
                try
                {
                    Response craftSync = new("command", "hlvrcoop_setresin " + player.Index + " " + packet.args[0]);
                    BroadcastToOthers(connections, socket, craftSync);
                }
                catch (Exception ex)
                {
                    ErrorLogger.LogError("AlyxGamemode.Crafting", ex);
                }
            }
        }

        #endregion
    }
}
