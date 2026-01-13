using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AlyxGamemode
{
    /// <summary>
    /// Packet types for Half-Life: Alyx Co-Op Mod communication.
    /// Packets are sent from the game (Lua/VScript) via print() statements with "HLVRCOOP" tag.
    /// </summary>
    public enum PacketType
    {
        None = 0,
        
        // ===== IMPLEMENTED PACKETS (Sent from Lua) =====
        /// <summary>Player body position, angles, and health. Sent every tick. Code: PLYR</summary>
        PlayerPosAng,
        /// <summary>Headset position and angles. Sent every tick. Code: HEAD</summary>
        HeadPosAng,
        /// <summary>Left and right hand positions and angles. Sent every tick. Code: HAND</summary>
        HandPosAng,
        /// <summary>Client initialization request. Sent when client joins. Code: INIT</summary>
        Initialization,
        /// <summary>Entities initialized confirmation. Sent after script setup. Code: IENT</summary>
        InitializedEntities,
        /// <summary>Physics object position and angles update. Sent when physics objects move. Code: PHYS</summary>
        PhysicsObjectPosAng,
        /// <summary>Map name and API version. Sent on map load. Code: MAPN</summary>
        MapName,
        /// <summary>Broken prop notification. Sent when prop breaks. Code: BRAK</summary>
        BrokenProp,
        /// <summary>Entity removed notification. Sent when entity is deleted. Code: EREM</summary>
        EntityRemoved,
        /// <summary>Entity output fired (trigger, button, etc.). Sent when entity outputs fire. Code: FIRE</summary>
        EntityFired,
        /// <summary>NPC health update. Sent when NPC health changes. Code: NPHP</summary>
        NPCHealth,
        /// <summary>Custom HLVRCoop command. Sent for chat commands. Code: CMND</summary>
        HLVRCOOPCommand,
        /// <summary>Teleport command. Sent when player teleports. Code: TELE</summary>
        Teleport,
        /// <summary>Template entity cache request. Sent for point_template entities. Code: TENT</summary>
        TemplateEntity,
        /// <summary>Entity spawn notification. Sent when item is picked up. Code: SPWN</summary>
        Spawn,
        
        // ===== UNUSED/LEGACY PACKETS (Not sent from Lua) =====
        // These packet types are defined but never sent by the game-side Lua scripts.
        // They appear to be legacy code or planned features that were never implemented.
        // Keeping them in the enum for backward compatibility, but handlers are empty.
        
        /// <summary>UNUSED: Right hand entity indexes. Not sent. Code: RHND</summary>
        RightHandIndexes,
        /// <summary>UNUSED: Left hand entity indexes. Not sent. Code: LHND</summary>
        LeftHandIndexes,
        /// <summary>UNUSED: Headset entity indexes. Not sent. Code: HSET</summary>
        HeadsetIndexes,
        /// <summary>UNUSED: Text entity indexes. Not sent. Code: TAGS</summary>
        TextIndexes,
        /// <summary>UNUSED: NPC collider indexes. Not sent. Code: NPCS</summary>
        ColliderIndexes,
        /// <summary>UNUSED: Unknown purpose. Not sent. Code: PRFX</summary>
        Prefix,
        /// <summary>UNUSED: Player alive state. Not sent (health tracked via PLYR). Code: ALIV</summary>
        Alive,
        /// <summary>UNUSED: Collider damage tracking. Not sent. Code: DMGE</summary>
        ColliderDamage,
        /// <summary>UNUSED: Physics object initial position. Not sent (handled via entity sync). Code: PROP</summary>
        PhysicsObjectIndexStartPos,
        /// <summary>UNUSED: Button initial position. Not sent. Code: BUTN</summary>
        ButtonIndexStartPos,
        /// <summary>UNUSED: Button press event. Not sent (button presses use FIRE packet). Code: BPRS</summary>
        ButtonPressIndex,
        /// <summary>UNUSED: Door initial position. Not sent (doors handled via PHYS packet). Code: DOOR</summary>
        DoorIndexStartPos,
        /// <summary>UNUSED: Trigger initial position. Not sent (handled via entity sync). Code: TRIG</summary>
        TriggerIndexStartPos,
        /// <summary>UNUSED: Trigger activation event. Not sent (trigger activations use FIRE packet). Code: TRGD</summary>
        TriggerActivateIndex,
    }
    public class Packet
    {
        public PacketType type = PacketType.None;
        public string[] args = Array.Empty<string>();
        public Packet()
        {
        }
        public Packet(string type)
        {
            this.type = ParseType(type);
        }
        public Packet(string type, string args)
        {
            this.type = ParseType(type);
            this.args = args.Split(" ");
        }
        public Packet(string type, string[] args)
        {
            this.type = ParseType(type);
            this.args = args;
        }
        public Packet(PacketType type)
        {
            this.type = type;
        }
        public Packet(PacketType type, string args)
        {
            this.type = type;
            this.args = args.Split(" ");
        }
        public Packet(PacketType type, string[] args)
        {
            this.type = type;
            this.args = args;
        }
        public bool IsValid()
        {
            return type switch
            {
                PacketType.None => false,
                _ => true,
            };
        }
        private static PacketType ParseType(string type)
        {
            return type.ToUpper() switch
            {
                "PLYR" => PacketType.PlayerPosAng,
                "HEAD" => PacketType.HeadPosAng,
                "HAND" => PacketType.HandPosAng,
                "INIT" => PacketType.Initialization,
                "IENT" => PacketType.InitializedEntities,
                "RHND" => PacketType.RightHandIndexes,
                "LHND" => PacketType.LeftHandIndexes,
                "HSET" => PacketType.HeadsetIndexes,
                "TAGS" => PacketType.TextIndexes,
                "NPCS" => PacketType.ColliderIndexes,
                "PRFX" => PacketType.Prefix,
                "ALIV" => PacketType.Alive,
                "DMGE" => PacketType.ColliderDamage,
                "PROP" => PacketType.PhysicsObjectIndexStartPos,
                "PHYS" => PacketType.PhysicsObjectPosAng,
                "MAPN" => PacketType.MapName,
                "BUTN" => PacketType.ButtonIndexStartPos,
                "BPRS" => PacketType.ButtonPressIndex,
                "DOOR" => PacketType.DoorIndexStartPos,
                "BRAK" => PacketType.BrokenProp,
                "TRIG" => PacketType.TriggerIndexStartPos,
                "TRGD" => PacketType.TriggerActivateIndex,
                "EREM" => PacketType.EntityRemoved,
                "FIRE" => PacketType.EntityFired,
                "NPHP" => PacketType.NPCHealth,
                "CMND" => PacketType.HLVRCOOPCommand,
                "TELE" => PacketType.Teleport,
                "TENT" => PacketType.TemplateEntity,
                "SPWN" => PacketType.Spawn,
                _ => PacketType.None,
            };
        }
        public override string ToString()
        {
            return type switch
            {
                PacketType.PlayerPosAng => "PLYR",
                PacketType.HeadPosAng => "HEAD",
                PacketType.HandPosAng => "HAND",
                PacketType.Initialization => "INIT",
                PacketType.InitializedEntities => "IENT",
                PacketType.RightHandIndexes => "RHND",
                PacketType.LeftHandIndexes => "LHND",
                PacketType.HeadsetIndexes => "HSET",
                PacketType.TextIndexes => "TAGS",
                PacketType.ColliderIndexes => "NPCS",
                PacketType.Prefix => "PRFX",
                PacketType.Alive => "ALIV",
                PacketType.ColliderDamage => "DMGE",
                PacketType.PhysicsObjectIndexStartPos => "PROP",
                PacketType.PhysicsObjectPosAng => "PHYS",
                PacketType.MapName => "MAPN",
                PacketType.ButtonIndexStartPos => "BUTN",
                PacketType.ButtonPressIndex => "BPRS",
                PacketType.DoorIndexStartPos => "DOOR",
                PacketType.BrokenProp => "BRAK",
                PacketType.TriggerIndexStartPos => "TRIG",
                PacketType.TriggerActivateIndex => "TRGD",
                PacketType.EntityRemoved => "EREM",
                PacketType.EntityFired => "FIRE",
                PacketType.NPCHealth => "NPHP",
                PacketType.HLVRCOOPCommand => "CMND",
                PacketType.Teleport => "TELE",
                PacketType.TemplateEntity => "TENT",
                PacketType.Spawn => "SPWN",
                _ => "NONE",
            };
        }
    }
}
