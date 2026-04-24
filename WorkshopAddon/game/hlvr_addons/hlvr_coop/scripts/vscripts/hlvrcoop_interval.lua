require "util.utilinit"

local Player = Entities:GetLocalPlayer();

HLVRCOOP_USE_UUIDS = true;
HLVRCOOP_API_VERSION = 5; -- this value will change if breaking changes are pushed to workshop
HLVRCOOP_ACTIVE = false;
HLVRCOOP_ENTCACHE = {};
HLVRCOOP_SEQ = 0;

-- Respawn system variables
local previousHealth = 100;
local respawnPosition = nil;
local respawnAngles = nil;
local respawnCooldown = 0; -- Prevent rapid respawn spam
local respawnScheduled = false; -- Track if respawn is already scheduled
local respawnThinkName = "HLVRCOOP_RespawnThink"; -- Name for SetThink to track respawn

print("HLVRCOOP Enabled!");

function Precache(context)
    PrecacheModel("models/props/choreo_office/headset_prop.vmdl", context)
    PrecacheModel("models/hands/alyx_glove_left.vmdl", context)
    PrecacheModel("models/hands/alyx_glove_right.vmdl", context)
end

-- Respawn system functions (defined globally so they're always available)
function HLVRCOOP_FindSpawnPosition()
    -- Try to find info_player_start by name first
    local spawnEntity = Entities:FindByName(nil, "info_player_start");
    if not spawnEntity then
        -- Try to find by classname
        spawnEntity = Entities:FindByClassname(nil, "info_player_start");
    end
    
    if spawnEntity and IsValidEntity(spawnEntity) then
        local pos = spawnEntity:GetAbsOrigin();
        local ang = spawnEntity:GetAnglesAsVector();
        
        -- Validate position and angles
        if pos and ang then
            respawnPosition = pos;
            respawnAngles = ang;
            print("HLVRCOOP: Found spawn position at " .. respawnPosition[1] .. " " .. respawnPosition[2] .. " " .. respawnPosition[3]);
        else
            -- Fallback if GetAbsOrigin or GetAnglesAsVector returns nil
            respawnPosition = Vector(0, 0, 0);
            respawnAngles = Vector(0, 0, 0);
            print("HLVRCOOP: Spawn entity found but position invalid, using origin (0, 0, 0)");
        end
    else
        -- Fallback to origin (0, 0, 0) if no spawn point found
        respawnPosition = Vector(0, 0, 0);
        respawnAngles = Vector(0, 0, 0);
        print("HLVRCOOP: No spawn point found, using origin (0, 0, 0)");
    end
end

-- Teleport player to spawn (called 1 second after death, after health is restored)
function HLVRCOOP_RespawnTeleport()
    if not Player or not IsValidEntity(Player) then
        respawnScheduled = false;
        return nil;
    end
    
    -- Stop the think
    Player:StopThink(respawnThinkName);
    
    -- Ensure spawn position is found
    if not respawnPosition then
        HLVRCOOP_FindSpawnPosition();
    end
    
    -- Verify respawn position is valid (should always be set by FindSpawnPosition, but check anyway)
    if not respawnPosition then
        print("HLVRCOOP: ERROR - Could not find respawn position!");
        respawnScheduled = false;
        return nil;
    end
    
    -- Teleport to spawn position
    local anchor = Player:GetHMDAnchor();
    if anchor then
        if Player:GetHMDAvatar() then
            anchor:SetAbsOrigin(respawnPosition);
            if respawnAngles then
                anchor:SetAbsAngles(respawnAngles[1], respawnAngles[2], respawnAngles[3]);
            end
        else
            Player:SetAbsOrigin(respawnPosition);
            if respawnAngles then
                Player:SetAbsAngles(respawnAngles[1], respawnAngles[2], respawnAngles[3]);
            end
        end
    else
        -- Fallback if anchor is nil
        Player:SetAbsOrigin(respawnPosition);
        if respawnAngles then
            Player:SetAbsAngles(respawnAngles[1], respawnAngles[2], respawnAngles[3]);
        end
    end
    
    -- Reset flags and cooldown
    respawnScheduled = false;
    respawnCooldown = 60; -- 1 second cooldown (60 ticks at ~60fps)
    previousHealth = 100; -- Reset health tracking
    
    -- Send respawn notification to server (optional, for tracking)
    print("HLVRCOOP: Respawned player at " .. respawnPosition[1] .. " " .. respawnPosition[2] .. " " .. respawnPosition[3]);
    HLVRCOOP_SEQ = HLVRCOOP_SEQ + 1;
    print("RESP " .. respawnPosition[1] .. " " .. respawnPosition[2] .. " " .. respawnPosition[3] .. " " .. HLVRCOOP_SEQ .. " " .. GameRules:GetGameTime() .. " HLVRCOOP");
    
    return nil; -- Return nil to stop the think (one-time execution)
end

-- Check for death and schedule respawn if needed
function HLVRCOOP_CheckDeath()
    if not Player or not IsValidEntity(Player) then
        return;
    end
    
    -- Safely get health (handle potential nil/invalid values)
    local currentHealth = Player:GetHealth();
    if currentHealth == nil or type(currentHealth) ~= "number" then
        return; -- Invalid health value, skip this frame
    end
    
    -- If respawn is already scheduled, don't check for death again
    -- Wait for the scheduled respawn to complete
    if respawnScheduled then
        -- Update previousHealth to track current state during respawn delay
        previousHealth = currentHealth;
        return;
    end
    
    -- Update cooldown if active
    if respawnCooldown > 0 then
        respawnCooldown = respawnCooldown - 1;
        -- Still update previousHealth during cooldown to track health changes
        previousHealth = currentHealth;
        return;
    end
    
    -- Detect death (health dropped to 0 or below)
    if previousHealth > 0 and currentHealth <= 0 then
        -- Player just died - immediately restore health to prevent death screen
        -- Then schedule teleport after 1 second
        -- Set flag immediately to prevent race conditions with event listener
        if not respawnScheduled then
            respawnScheduled = true;
            
            -- Immediately restore health to prevent death screen from appearing
            -- This allows the red screen to show but prevents the "Load Save File" UI
            -- Verify Player is still valid before operations
            if IsValidEntity(Player) then
                Player:SetHealth(100);
                previousHealth = 100; -- Update immediately to prevent re-triggering
                
                -- Stop any existing respawn think first (in case event listener also triggered)
                Player:StopThink(respawnThinkName);
                
                -- Schedule teleport 1 second from now using SetThink
                -- SetThink(func, name, delay) - delay is in seconds (1.0 = 1 second)
                Player:SetThink(HLVRCOOP_RespawnTeleport, respawnThinkName, 1.0);
                print("HLVRCOOP: Player died, health restored. Teleporting to spawn in 1 second");
            else
                -- Player became invalid, reset flag
                respawnScheduled = false;
            end
        else
            -- Respawn already scheduled (likely by event listener), just update health tracking
            previousHealth = currentHealth;
        end
    else
        -- Update previousHealth for next check
        previousHealth = currentHealth;
    end
end

function HLVRCoOpMod()
    if HLVRCOOP_ACTIVE then
        -- Check for death every frame (not just when sending PLYR packet)
        -- This ensures we can restore health before the death screen appears
        HLVRCOOP_CheckDeath();

        -- Interpolate remote player entities every tick
        HLVRCOOP_Interpolate();

        hlvrcoop_tick = hlvrcoop_tick + 1;

        local playerOrigin = Player:GetOrigin();
        local playerCenter = Player:GetCenter();
        local playerAngles = Player:GetAnglesAsVector();
        local playerHealth = Player:GetHealth();
        local head = Player:GetHMDAvatar()

        -- Variable-rate send: HEAD at 10Hz (tick%10==0), HAND at 10Hz offset (tick%10==5), PLYR at 5Hz (tick%20==0)
        if head then
            local leftController = head:GetVRHand(0)
            local rightController = head:GetVRHand(1)
            local leftHand = GetHandFromController(leftController)
            local rightHand = GetHandFromController(rightController)

            local playerHead = head:GetAbsOrigin();
            local playerHeadAng = head:GetAnglesAsVector();
            local playerLeftHand = leftHand:GetAbsOrigin();
            local playerRightHand = rightHand:GetAbsOrigin();
            local playerLeftHandAngles = leftHand:GetAnglesAsVector();
            local playerRightHandAngles = rightHand:GetAnglesAsVector();

            if hlvrcoop_tick % 10 == 0 then
                HLVRCOOP_SEQ = HLVRCOOP_SEQ + 1;
                local ts = GameRules:GetGameTime();
                print("HEAD "..playerHead[1].." "..playerHead[2].." "..playerHead[3].." "..playerHeadAng[1].." "..playerHeadAng[2].." "..playerHeadAng[3].." "..HLVRCOOP_SEQ.." "..ts.." HLVRCOOP");
            end

            if hlvrcoop_tick % 10 == 5 then
                HLVRCOOP_SEQ = HLVRCOOP_SEQ + 1;
                local ts = GameRules:GetGameTime();
                print("HAND "..playerLeftHand[1].." "..playerLeftHand[2].." "..playerLeftHand[3].." "..playerLeftHandAngles[1].." "..playerLeftHandAngles[2].." "..playerLeftHandAngles[3].." "..playerRightHand[1].." "..playerRightHand[2].." "..playerRightHand[3].." "..playerRightHandAngles[1].." "..playerRightHandAngles[2].." "..playerRightHandAngles[3].." "..HLVRCOOP_SEQ.." "..ts.." HLVRCOOP");
            end
        else
            if hlvrcoop_tick % 10 == 0 then
                HLVRCOOP_SEQ = HLVRCOOP_SEQ + 1;
                local ts = GameRules:GetGameTime();
                print("HEAD "..playerCenter[1].." "..playerCenter[2].." "..(playerCenter[3]+30).." "..playerAngles[1].." "..playerAngles[2].." "..playerAngles[3].." "..HLVRCOOP_SEQ.." "..ts.." HLVRCOOP");
            end
        end

        if hlvrcoop_tick % 20 == 0 then
            HLVRCOOP_SEQ = HLVRCOOP_SEQ + 1;
            local ts = GameRules:GetGameTime();
            print("PLYR "..playerOrigin[1].." "..playerOrigin[2].." "..playerOrigin[3].." "..playerAngles[1].." "..playerAngles[2].." "..playerAngles[3].." "..playerHealth.." "..HLVRCOOP_SEQ.." "..ts.." HLVRCOOP");
        end

        HLVRCOOP_CacheSync();
    else
        function uuid(name, x, y, z, classname)
            -- this system ensures that entities always have a unique ID
            -- it brings the most unique attributes of an entity together
            return classname.."+"..name.."+"..math.floor(x).."+"..math.floor(y).."+"..math.floor(z);
        end

        function HLVRCOOP_CacheSync()
            for i, object in pairs(HLVRCOOP_ENTCACHE) do
                local entity = object.entity;
                if IsValidEntity(entity) then
                    if not string.find(object.class, "trigger_") and not string.find(object.class, "func_") then
                        local origin = entity:GetAbsOrigin();
                        local angles = entity:GetAnglesAsVector();
                        if object.class == "prop_door_rotating_physics" and (math.floor(angles[1]) ~= math.floor(object.angles[1])) or (math.floor(angles[2]) ~= math.floor(object.angles[2])) or (math.floor(angles[3]) ~= math.floor(object.angles[3])) then
                            HLVRCOOP_SEQ = HLVRCOOP_SEQ + 1;
                            print("PHYS "..object.name.." "..origin[1].." "..origin[2].." "..origin[3].." "..angles[1].." "..angles[2].." "..angles[3].." "..HLVRCOOP_SEQ.." "..GameRules:GetGameTime().." HLVRCOOP");
                            object.angles = angles;
                        elseif (math.floor(origin[1]) ~= math.floor(object.origin[1])) or (math.floor(origin[2]) ~= math.floor(object.origin[2])) or (math.floor(origin[3]) ~= math.floor(object.origin[3])) then
                            HLVRCOOP_SEQ = HLVRCOOP_SEQ + 1;
                            print("PHYS "..object.name.." "..origin[1].." "..origin[2].." "..origin[3].." "..angles[1].." "..angles[2].." "..angles[3].." "..HLVRCOOP_SEQ.." "..GameRules:GetGameTime().." HLVRCOOP");
                            object.origin = origin;
                            object.angles = angles;
                        end
                        if string.find(object.class, "npc_") or object.class == "generic_actor" then
                            if object.class ~= "npc_furniture" then
                                if object.dead then
                                    -- Already reported dead, skip
                                else
                                    local health = entity:GetHealth();
                                    if health <= 0 then
                                        object.dead = true;
                                        HLVRCOOP_SEQ = HLVRCOOP_SEQ + 1;
                                        print("NPHP "..object.name.." 0 "..HLVRCOOP_SEQ.." "..GameRules:GetGameTime().." HLVRCOOP");
                                    elseif object.health == nil or object.health > health then
                                        local prevHealth = object.health;
                                        HLVRCOOP_SEQ = HLVRCOOP_SEQ + 1;
                                        print("NPHP "..object.name.." "..health.." "..HLVRCOOP_SEQ.." "..GameRules:GetGameTime().." HLVRCOOP");
                                        if prevHealth ~= nil then
                                            HLVRCOOP_SEQ = HLVRCOOP_SEQ + 1;
                                            print("DMGE "..object.name.." "..(prevHealth - health).." "..HLVRCOOP_SEQ.." "..GameRules:GetGameTime().." HLVRCOOP");
                                        end
                                        object.health = health;
                                    end
                                end
                                -- Send NPCS once on first detection
                                if object.collider_sent == nil then
                                    local origin = entity:GetAbsOrigin();
                                    HLVRCOOP_SEQ = HLVRCOOP_SEQ + 1;
                                    print("NPCS "..object.name.." "..math.floor(origin[1]).." "..math.floor(origin[2]).." "..math.floor(origin[3]).." "..HLVRCOOP_SEQ.." "..GameRules:GetGameTime().." HLVRCOOP");
                                    object.collider_sent = true;
                                end
                            end
                        end
                    end
                else
                    if object.name ~= "" then
                        if string.find(object.class, "npc_") or object.class == "generic_actor" then
                            -- bruteforce npc death
                            if object.health == nil or object.health > 0 then
                                object.health = 0
                                object.dead = true
                                HLVRCOOP_SEQ = HLVRCOOP_SEQ + 1;
                                print("NPHP "..object.name.." 0 "..HLVRCOOP_SEQ.." "..GameRules:GetGameTime().." HLVRCOOP");
                            end
                        elseif string.find(object.class, "prop_") or string.find(object.class, "item_") then
                            HLVRCOOP_SEQ = HLVRCOOP_SEQ + 1;
                            print("BRAK "..object.name.." "..HLVRCOOP_SEQ.." "..GameRules:GetGameTime().." HLVRCOOP");
                        else
                            HLVRCOOP_SEQ = HLVRCOOP_SEQ + 1;
                            print("EREM "..object.name.." "..HLVRCOOP_SEQ.." "..GameRules:GetGameTime().." HLVRCOOP");
                        end
                    end
                    object = nil;
                end
                HLVRCOOP_ENTCACHE[i] = object;
            end
        end

        function HLVRCOOP_Teleport()
            local origin = Player:GetAbsOrigin();
            local angles = Player:GetAnglesAsVector();
            HLVRCOOP_SEQ = HLVRCOOP_SEQ + 1;
            print("TELE "..origin[1].." "..origin[2].." "..origin[3].." "..angles[1].." "..angles[2].." "..angles[3].." "..HLVRCOOP_SEQ.." "..GameRules:GetGameTime().." HLVRCOOP");
        end

        function HLVRCOOP_EntitySyncSpecific(entity)
            local precached = {}

            for _, object in ipairs(HLVRCOOP_ENTCACHE) do
                if IsValidEntity(object.entity) then
                    precached[object.entity:GetEntityIndex()] = true;
                end
            end

            if precached[entity:GetEntityIndex()] then
                return -- already precached
            end

            local object = {};
            object.name = entity:GetName();
            object.origin = entity:GetAbsOrigin();
            object.angles = entity:GetAnglesAsVector();
            object.class = entity:GetClassname();
            object.model = entity:GetModelName();
            object.entity = entity; --entity:GetEntityIndex();

            
            for _, output in pairs(hlvrcoop_outputs) do
                entity:RedirectOutput(output, "HLVRCOOP_"..output, thisEntity);
            end

            if string.find(object.class, "door") then
                DoEntFireByInstanceHandle(entity, "Unlock", "", 0, nil, nil);
                DoEntFireByInstanceHandle(entity, "DisableLatch", "", 0, nil, nil);
            end

            if object.class == "trigger_teleport" then
                entity:RedirectOutput("OnStartTouch", "HLVRCOOP_Teleport", thisEntity);
            end

            if object.name == "" then
                object.name = "hlvrcoop_kcoords_"..math.floor(object.origin[1]).."_"..math.floor(object.origin[2]).."_"..math.floor(object.origin[3]);
            end

            entity:SetEntityName(object.name);
            if HLVRCOOP_USE_UUIDS then
                local uu = uuid(object.name, object.origin[1], object.origin[2], object.origin[3], object.class);
                object.name = uu;
                HLVRCOOP_ENTCACHE[uu] = object;
            else
                HLVRCOOP_ENTCACHE[#HLVRCOOP_ENTCACHE+1] = object;
            end
        end

        function HLVRCOOP_Templated(tempent, ents)
            for entkey, entity in pairs(ents) do
                local name = entity:GetName();
                local origin = entity:GetAbsOrigin();
                if name == "" then
                    name = "hlvrcoop_kcoords_"..math.floor(origin[1]).."_"..math.floor(origin[2]).."_"..math.floor(origin[3]);
                end
                SendToConsole("hlvrcoop_cache_entity_nonuuid "..name);
            end
        end

        function HLVRCOOP_EntitySync(first)
            for _, template in pairs(Entities:FindAllByClassname("point_template")) do
                template:SetSpawnCallback(HLVRCOOP_Templated, thisEntity);
            end
            for prop, _ in pairs(hlvrcoop_trackers) do
                local props = Entities:FindAllByClassname(prop);
                for _, entity in pairs(props) do
                    if entity:GetName() ~= "hlvrcoop_timer" then -- logic_timer
                        HLVRCOOP_EntitySyncSpecific(entity);
                    end
                end
            end
        end

        -- thank you Epic#4527 from the source 2 modding discord!
        -- https://discord.com/channels/692784980304330853/713548145358929990/715966997103509578
        function GetHandFromController(controller)
            for k, child in ipairs(controller:GetChildren()) do
                if (child:GetClassname() == "hlvr_prop_renderable_glove") then
                    return child
                end
            end
            return controller
        end

        hlvrcoop_trackers = {
            -- triggers
            ["trigger_multiple"] = true,
            ["trigger_once"] = true, -- special case
            ["trigger_look"] = true,
            ["trigger_proximity"] = true,
            ["trigger_teleport"] = true,
            ["trigger_detect_bullet_fire"] = true,
            ["trigger_detect_explosion"] = true,
            ["trigger_blind_zombie_crash"] = true,
            ["trigger_blind_zombie_sound_area"] = true,
            ["trigger_blind_zombie_wander_area"] = true,
            ["trigger_foliage_interaction"] = true,
            ["trigger_gravity"] = true,

            -- logic
            ["logic_relay"] = true,
            ["logic_timer"] = true,
            ["math_counter"] = true,

            -- buttons
            ["func_button"] = true,
            ["func_physical_button"] = true,

            -- props
            ["prop_physics"] = true,
            ["prop_physics_interactive"] = true,
            ["prop_physics_override"] = true,
            ["prop_dry_erase_marker"] = true,
            ["prop_animinteractable"] = true,
            ["prop_door_rotating_physics"] = true,
            ["prop_handpose"] = false, -- experimental
            ["prop_ragdoll"] = true,
            ["prop_animating_breakable"] = true,
            ["item_item_crate"] = true,

            -- gameplay
            ["item_healthvial"] = true, -- health injection pen
            ["item_hlvr_clip_energygun"] = true, -- single pistol clip
            ["item_hlvr_clip_energygun_multiple"] = true, -- four pistol clips
            ["item_hlvr_clip_rapidfire"] = true, -- single combine power cell
            ["item_hlvr_clip_shotgun_single"] = true, -- single shotgun shell
            ["item_hlvr_clip_shotgun_multiple"] = true, -- box of shotgun shells
            ["item_hlvr_health_station_vial"] = true, -- antlion grub (vial) for health station
            ["item_hlvr_prop_battery"] = true, -- combine power station battery
            ["item_hlvr_weapon_tripmine"] = true, -- tripmines
            ["info_hlvr_holo_hacking_plug"] = true, -- hologram puzzles
            ["item_hlvr_crafting_currency_large"] = true, -- large resin
            ["item_hlvr_crafting_currency_small"] = true, -- small resin
            ["item_hlvr_grenade_bomb"] = true, -- grenade bomb
            ["item_hlvr_grenade_frag"] = true, -- grenade frag
            ["item_hlvr_grenade_remote_sticky"] = true, -- sticky grenade
            ["item_hlvr_headcrab_gland"] = true, -- headcrab heart

            -- npcs
            ["npc_furniture"] = true,
            ["npc_headcrab_armored"] = true,
            ["npc_headcrab_runner"] = true,
            ["npc_headcrab_black"] = true,
            ["npc_zombie"] = true,
            ["npc_zombie_blind"] = true, -- jeff
            ["npc_headcrab"] = true,
            ["npc_combine_s"] = true,
            ["npc_antlion"] = true,
            ["npc_strider"] = true,
            ["generic_actor"] = true,
            ["npc_manhack"] = true,
            ["npc_barnacle"] = true,
        };
        hlvrcoop_toggletypes = {
            ["trigger_multiple"] = {"Disable", "Enable"},
            ["trigger_once"] = {"Disable", "Enable"},
            ["trigger_look"] = {"Disable", "Enable"},
            ["trigger_proximity"] = {"Disable", "Enable"},
            ["trigger_teleport"] = {"Disable", "Enable"},
            ["trigger_detect_bullet_fire"] = {"Disable", "Enable"},
            ["trigger_detect_explosion"] = {"Disable", "Enable"},
            ["trigger_blind_zombie_crash"] = {"Disable", "Enable"},
            ["trigger_blind_zombie_sound_area"] = {"Disable", "Enable"},
            ["trigger_blind_zombie_wander_area"] = {"Disable", "Enable"},
            ["trigger_foliage_interaction"] = {"Disable", "Enable"},
            ["trigger_gravity"] = {"Disable", "Enable"},

            ["logic_relay"] = {"Disable", "Enable"},
            ["logic_timer"] = {"Disable", "Enable"},
            ["math_counter"] = {"Disable", "Enable"},

            ["func_button"] = {"Lock", "Unlock"},
            ["func_physical_button"] = {"Lock", "Unlock"},

            ["prop_physics"] = {"DisableMotion", "EnableMotion"},
            ["prop_physics_interactive"] = {"DisableMotion", "EnableMotion"},
            ["prop_physics_override"] = {"DisableMotion", "EnableMotion"},
            ["prop_dry_erase_marker"] = {"DisableMotion", "EnableMotion"},
            ["prop_animinteractable"] = {"DisableInteraction", "EnableInteraction"},
            ["prop_door_rotating_physics"] = {"DisableMotion", "EnableMotion"},
            ["prop_animating_breakable"] = {"DisableMotion", "EnableMotion"},
            ["prop_ragdoll"] = {"DisableMotion", "EnableMotion"},
            --["prop_handpose"] = {"Disable", "Enable"},

            ["item_healthvial"] = {"DisableMotion", "EnableMotion"},
            ["item_hlvr_clip_energygun"] = {"DisableMotion", "EnableMotion"},
            ["item_hlvr_clip_energygun_multiple"] = {"DisableMotion", "EnableMotion"},
            ["item_hlvr_clip_rapidfire"] = {"DisableMotion", "EnableMotion"},
            ["item_hlvr_clip_shotgun_single"] = {"DisableMotion", "EnableMotion"},
            ["item_hlvr_clip_shotgun_multiple"] = {"DisableMotion", "EnableMotion"},
            ["item_hlvr_health_station_vial"] = {"DisableMotion", "EnableMotion"},
            ["item_hlvr_prop_battery"] = {"DisableMotion", "EnableMotion"},
            ["item_hlvr_weapon_tripmine"] = {"DisableMotion", "EnableMotion"},
            ["item_hlvr_crafting_currency_large"] = {"DisableMotion", "EnableMotion"},
            ["item_hlvr_crafting_currency_small"] = {"DisableMotion", "EnableMotion"},
            ["item_hlvr_grenade_bomb"] = {"DisableMotion", "EnableMotion"},
            ["item_hlvr_grenade_frag"] = {"DisableMotion", "EnableMotion"},
            ["item_hlvr_grenade_remote_sticky"] = {"DisableMotion", "EnableMotion"},
            ["item_hlvr_headcrab_gland"] = {"DisableMotion", "EnableMotion"},

            -- npcs are loose on purpose as they continuously move
        };

        local function HLVRCOOP_PickupSync(data)
            local handId = util.GetHandIdFromTip(data.vr_tip_attachment)
            local hand = Player.Hands[handId + 1]
            local ent_held = util.EstimateNearestEntity(data.item_name, data.item, hand:GetOrigin())
            if IsValidEntity(ent_held) then
                HLVRCOOP_EntitySyncSpecific(ent_held)
                local name = ent_held:GetName()
                local origin = ent_held:GetOrigin()
                HLVRCOOP_SEQ = HLVRCOOP_SEQ + 1;
                print("SPWN " .. data.item .. " " .. name .. " " .. origin[1] .. " " .. origin[2] .. " " .. origin[3] .. " " .. HLVRCOOP_SEQ .. " " .. GameRules:GetGameTime() .. " HLVRCOOP")
            end
        end
        ListenToGameEvent("item_pickup", HLVRCOOP_PickupSync, nil)

        local function HLVRCOOP_ResinSync(data)
            HLVRCOOP_SEQ = HLVRCOOP_SEQ + 1;
            print("CRFT " .. Player.Items.resin_found .. " " .. HLVRCOOP_SEQ .. " " .. GameRules:GetGameTime() .. " HLVRCOOP");
        end
        ListenToGameEvent("player_drop_resin_in_backpack", HLVRCOOP_ResinSync, nil)

        function fireit(params, output)
            local ent = params.caller;
            if not IsValidEntity(ent) then
                return -- the entity must have been deleted?
            end
            local name = ent:GetName();
            local class = ent:GetClassname();
            local firedKey = name;

            if HLVRCOOP_USE_UUIDS then
                local origin = ent:GetOrigin();
                firedKey = uuid(name, origin[1], origin[2], origin[3], class);
            end

            if class == "trigger_once" then
                if HLVRCOOP_FIRED_TRIGGERS[firedKey] then return end
                HLVRCOOP_FIRED_TRIGGERS[firedKey] = true;
            end

            if not HLVRCOOP_USE_UUIDS then
                if name ~= "" then
                    HLVRCOOP_SEQ = HLVRCOOP_SEQ + 1;
                    print("FIRE "..name.." "..output.." "..HLVRCOOP_SEQ.." "..GameRules:GetGameTime().." HLVRCOOP");
                end
            else
                HLVRCOOP_SEQ = HLVRCOOP_SEQ + 1;
                print("FIRE "..firedKey.." "..output.." "..HLVRCOOP_SEQ.." "..GameRules:GetGameTime().." HLVRCOOP");
            end
        end

        HLVRCOOP_OnTrigger = function(params) fireit(params, "OnTrigger") end
        HLVRCOOP_OnStartTouch = function(params) fireit(params, "OnStartTouch") end
        HLVRCOOP_OnEndTouch = function(params) fireit(params, "OnEndTouch") end
        HLVRCOOP_OnStartLook = function(params) fireit(params, "OnStartLook") end
        HLVRCOOP_OnEndLook = function(params) fireit(params, "OnEndLook") end
        HLVRCOOP_OnIn = function(params) fireit(params, "OnIn") end
        HLVRCOOP_OnPressed = function(params) fireit(params, "OnPressed") end
        HLVRCOOP_OnOut = function(params) fireit(params, "OnOut") end
        HLVRCOOP_OnPlayerUse = function(params) fireit(params, "OnPlayerUse") end
        HLVRCOOP_OnPlayerPickup = function(params) fireit(params, "OnPlayerPickup") end
        HLVRCOOP_OnGlovePulled = function(params) fireit(params, "OnGlovePulled") end
        HLVRCOOP_OnAwakened = function(params) fireit(params, "OnAwakened") end
        HLVRCOOP_OnMotionEnabled = function(params) fireit(params, "OnMotionEnabled") end
        HLVRCOOP_OnAwake = function(params) fireit(params, "OnAwake") end
        HLVRCOOP_OnAsleep = function(params) fireit(params, "OnAsleep") end
        HLVRCOOP_OnOutOfWorld = function(params) fireit(params, "OnOutOfWorld") end
        HLVRCOOP_OnHealthChanged = function(params) fireit(params, "OnHealthChanged") end
        HLVRCOOP_OnBreak = function(params) fireit(params, "OnBreak") end
        HLVRCOOP_OnTakeDamage = function(params) fireit(params, "OnTakeDamage") end
        HLVRCOOP_OnIgnite = function(params) fireit(params, "OnIgnite") end
        HLVRCOOP_OnUser1 = function(params) fireit(params, "OnUser1") end
        HLVRCOOP_OnUser2 = function(params) fireit(params, "OnUser2") end
        HLVRCOOP_OnUser3 = function(params) fireit(params, "OnUser3") end
        HLVRCOOP_OnUser4 = function(params) fireit(params, "OnUser4") end
        HLVRCOOP_OnKilled = function(params) fireit(params, "OnKilled") end
        HLVRCOOP_OnInteractStart = function(params) fireit(params, "OnInteractStart") end
        HLVRCOOP_OnInteractStop = function(params) fireit(params, "OnInteractStop") end
        HLVRCOOP_OnCompletionA = function(params) fireit(params, "OnCompletionA") end
        HLVRCOOP_OnCompletionB = function(params) fireit(params, "OnCompletionB") end
        HLVRCOOP_OnCompletionC = function(params) fireit(params, "OnCompletionC") end
        HLVRCOOP_OnCompletionD = function(params) fireit(params, "OnCompletionD") end
        HLVRCOOP_OnCompletionE = function(params) fireit(params, "OnCompletionE") end
        HLVRCOOP_OnCompletionF = function(params) fireit(params, "OnCompletionF") end
        HLVRCOOP_OnCompletionA_Forward = function(params) fireit(params, "OnCompletionA_Forward") end
        HLVRCOOP_OnCompletionB_Forward = function(params) fireit(params, "OnCompletionB_Forward") end
        HLVRCOOP_OnCompletionC_Forward = function(params) fireit(params, "OnCompletionC_Forward") end
        HLVRCOOP_OnCompletionD_Forward = function(params) fireit(params, "OnCompletionD_Forward") end
        HLVRCOOP_OnCompletionE_Forward = function(params) fireit(params, "OnCompletionE_Forward") end
        HLVRCOOP_OnCompletionF_Forward = function(params) fireit(params, "OnCompletionF_Forward") end
        HLVRCOOP_OnCompletionA_Backward = function(params) fireit(params, "OnCompletionA_Backward") end
        HLVRCOOP_OnCompletionB_Backward = function(params) fireit(params, "OnCompletionB_Backward") end
        HLVRCOOP_OnCompletionC_Backward = function(params) fireit(params, "OnCompletionC_Backward") end
        HLVRCOOP_OnCompletionD_Backward = function(params) fireit(params, "OnCompletionD_Backward") end
        HLVRCOOP_OnCompletionE_Backward = function(params) fireit(params, "OnCompletionE_Backward") end
        HLVRCOOP_OnCompletionF_Backward = function(params) fireit(params, "OnCompletionF_Backward") end
        HLVRCOOP_OnCompletionExitA = function(params) fireit(params, "OnCompletionExitA") end
        HLVRCOOP_OnCompletionExitB = function(params) fireit(params, "OnCompletionExitB") end
        HLVRCOOP_OnCompletionExitC = function(params) fireit(params, "OnCompletionExitC") end
        HLVRCOOP_OnCompletionExitD = function(params) fireit(params, "OnCompletionExitD") end
        HLVRCOOP_OnCompletionExitE = function(params) fireit(params, "OnCompletionExitE") end
        HLVRCOOP_OnCompletionExitF = function(params) fireit(params, "OnCompletionExitF") end
        HLVRCOOP_OnReturnToCompletion = function(params) fireit(params, "OnReturnToCompletion") end
        HLVRCOOP_Position = function(params) fireit(params, "Position") end
        HLVRCOOP_PositionInverted = function(params) fireit(params, "PositionInverted") end
        HLVRCOOP_PositionRaw = function(params) fireit(params, "PositionRaw") end
        HLVRCOOP_Velocity = function(params) fireit(params, "Velocity") end
        HLVRCOOP_PositionInitialLimitsRemap = function(params) fireit(params, "PositionInitialLimitsRemap") end
        HLVRCOOP_OnGravityGunPull = function(params) fireit(params, "OnGravityGunPull") end
        HLVRCOOP_OnHandPosed = function(params) fireit(params, "OnHandPosed") end
        HLVRCOOP_OnHandUnPosed = function(params) fireit(params, "OnHandUnPosed") end
        HLVRCOOP_OnExplode = function(params) fireit(params, "OnExplode") end
        HLVRCOOP_OnHackStarted = function(params) fireit(params, "OnHackStarted") end
        HLVRCOOP_OnHackStopped = function(params) fireit(params, "OnHackStopped") end
        HLVRCOOP_OnHackSuccess = function(params) fireit(params, "OnHackSuccess") end
        HLVRCOOP_OnHackFailed = function(params) fireit(params, "OnHackFailed") end
        HLVRCOOP_OnHackSuccessAnimationComplete = function(params) fireit(params, "OnHackSuccessAnimationComplete") end
        HLVRCOOP_OnPuzzleCompleted = function(params) fireit(params, "OnPuzzleCompleted") end
        HLVRCOOP_OnPuzzleSuccess = function(params) fireit(params, "OnPuzzleSuccess") end
        HLVRCOOP_OnPuzzleFailed = function(params) fireit(params, "OnPuzzleFailed") end
        HLVRCOOP_OnEntitySpawned = function(params) fireit(params, "OnEntitySpawned") end
        HLVRCOOP_OnTimer = function(params) fireit(params, "OnTimer") end
        HLVRCOOP_OnTimerHigh = function(params) fireit(params, "OnTimerHigh") end
        HLVRCOOP_OnTimerLow = function(params) fireit(params, "OnTimerLow") end
        HLVRCOOP_OnTrue = function(params) fireit(params, "OnTrue") end
        HLVRCOOP_OnFalse = function(params) fireit(params, "OnFalse") end
        HLVRCOOP_OnCase01 = function(params) fireit(params, "OnCase01") end
        HLVRCOOP_OnCase02 = function(params) fireit(params, "OnCase02") end
        HLVRCOOP_OnCase03 = function(params) fireit(params, "OnCase03") end
        HLVRCOOP_OnCase04 = function(params) fireit(params, "OnCase04") end
        HLVRCOOP_OnCase05 = function(params) fireit(params, "OnCase05") end
        HLVRCOOP_OnCase06 = function(params) fireit(params, "OnCase06") end
        HLVRCOOP_OnCase07 = function(params) fireit(params, "OnCase07") end
        HLVRCOOP_OnCase08 = function(params) fireit(params, "OnCase08") end
        HLVRCOOP_OnCase09 = function(params) fireit(params, "OnCase09") end
        HLVRCOOP_OnCase10 = function(params) fireit(params, "OnCase10") end
        HLVRCOOP_OnCase11 = function(params) fireit(params, "OnCase11") end
        HLVRCOOP_OnCase12 = function(params) fireit(params, "OnCase12") end
        HLVRCOOP_OnCase13 = function(params) fireit(params, "OnCase13") end
        HLVRCOOP_OnCase14 = function(params) fireit(params, "OnCase14") end
        HLVRCOOP_OnCase15 = function(params) fireit(params, "OnCase15") end
        HLVRCOOP_OnCase16 = function(params) fireit(params, "OnCase16") end
        --HLVRCOOP_OutValue = function(params) fireit(params, "OutValue") end
        HLVRCOOP_OnHitMin = function(params) fireit(params, "OnHitMin") end
        HLVRCOOP_OnHitMax = function(params) fireit(params, "OnHitMax") end
        HLVRCOOP_OnGetValue = function(params) fireit(params, "OnGetValue") end
        HLVRCOOP_OnChangedFromMin = function(params) fireit(params, "OnChangedFromMin") end
        HLVRCOOP_OnChangedFromMax = function(params) fireit(params, "OnChangedFromMax") end
        HLVRCOOP_OnDetectedBulletFire = function(params) fireit(params, "OnDetectedBulletFire") end
        HLVRCOOP_OnDetectedExplosion = function(params) fireit(params, "OnDetectedExplosion") end
        HLVRCOOP_OnSoundGeneratedInside = function(params) fireit(params, "OnSoundGeneratedInside") end
        
        hlvrcoop_outputs = {
            "OnTrigger",
            "OnStartTouch",
            "OnEndTouch",
            "OnStartLook",
            "OnEndLook",
            "OnIn",
            "OnPressed",
            "OnOut",
            "OnPlayerUse",
            "OnPlayerPickup",
            "OnGlovePulled",
            "OnAwakened",
            "OnMotionEnabled",
            "OnAwake",
            "OnAsleep",
            "OnOutOfWorld",
            "OnHealthChanged",
            "OnBreak",
            "OnTakeDamage",
            "OnIgnite",
            "OnUser1",
            "OnUser2",
            "OnUser3",
            "OnUser4",
            "OnKilled",
            "OnInteractStart",
            "OnInteractStop",
            "OnCompletionA",
            "OnCompletionB",
            "OnCompletionC",
            "OnCompletionD",
            "OnCompletionE",
            "OnCompletionF",
            "OnCompletionA_Forward",
            "OnCompletionB_Forward",
            "OnCompletionC_Forward",
            "OnCompletionD_Forward",
            "OnCompletionE_Forward",
            "OnCompletionF_Forward",
            "OnCompletionA_Backward",
            "OnCompletionB_Backward",
            "OnCompletionC_Backward",
            "OnCompletionD_Backward",
            "OnCompletionE_Backward",
            "OnCompletionF_Backward",
            "OnCompletionExitA",
            "OnCompletionExitB",
            "OnCompletionExitC",
            "OnCompletionExitD",
            "OnCompletionExitE",
            "OnCompletionExitF",
            "OnReturnToCompletion",
            "Position",
            "PositionInverted",
            "PositionRaw",
            "Velocity",
            "PositionInitialLimitsRemap",
            "OnGravityGunPull",
            "OnHandPosed",
            "OnHandUnPosed",
            "OnExplode",
            "OnHackStarted",
            "OnHackStopped",
            "OnHackSuccess",
            "OnHackFailed",
            "OnHackSuccessAnimationComplete",
            "OnPuzzleCompleted",
            "OnPuzzleSuccess",
            "OnPuzzleFailed",
            "OnEntitySpawned",
            "OnTimer",
            "OnTimerHigh",
            "OnTimerLow",
            "OnTrue",
            "OnFalse",
            "OnCase01",
            "OnCase02",
            "OnCase03",
            "OnCase04",
            "OnCase05",
            "OnCase06",
            "OnCase07",
            "OnCase08",
            "OnCase09",
            "OnCase10",
            "OnCase11",
            "OnCase12",
            "OnCase13",
            "OnCase14",
            "OnCase15",
            "OnCase16",
            "OnHitMin",
            "OnHitMax",
            "OnGetValue",
            "OnChangedFromMin",
            "OnChangedFromMax",
        };

        HLVRCOOP_ACTIVE = true;
        hlvrcoop_heads = {};
        hlvrcoop_lefthands = {};
        hlvrcoop_righthands = {};
        hlvrcoop_text = {};
        HLVRCOOP_FIRED_TRIGGERS = {};
        hlvrcoop_player_count = 16;
        hlvrcoop_inbetween = 0;
        hlvrcoop_tick = 0;

        -- Initialize interpolation state arrays
        hlvrcoop_interp = {}
        for i = 1, hlvrcoop_player_count do
            hlvrcoop_interp[i] = {
                head = { prev_pos = nil, prev_ang = nil, target_pos = nil, target_ang = nil, last_update = 0 },
                lefthand = { prev_pos = nil, prev_ang = nil, target_pos = nil, target_ang = nil, last_update = 0 },
                righthand = { prev_pos = nil, prev_ang = nil, target_pos = nil, target_ang = nil, last_update = 0 },
            }
        end

        SpawnEntityFromTableSynchronous("point_worldtext", {
            origin = "16128 16128 16128",
            targetname = "hlvrcoop_hud",
            fullbright = "1",
            color = "255 255 255 255",
            world_units_per_pixel = "0.005",
            font_size = "128",
            justify_horizontal = "1",
            justify_vertical = "1",
            message = "",
            font_name = "Arial Black",
            reorient_mode = "0",
            depth_render_offset = "0.125",
            rendercolor = "255 255 255 255",
            enabled = "1",
        });

        for i = 0, hlvrcoop_player_count - 1 do
            table.insert(hlvrcoop_heads, SpawnEntityFromTableSynchronous("prop_dynamic", {
                origin = "16128 16128 16128",
                targetname = "hlvrcoop_head_" .. i,
                model = "models/props/choreo_office/headset_prop.vmdl",
                solid = "0",
            }));
            table.insert(hlvrcoop_lefthands, SpawnEntityFromTableSynchronous("prop_dynamic", {
                origin = "16128 16128 16128",
                targetname = "hlvrcoop_lefthand_" .. i,
                model = "models/hands/alyx_glove_left.vmdl",
                solid = "0",
            }));
            table.insert(hlvrcoop_righthands, SpawnEntityFromTableSynchronous("prop_dynamic", {
                origin = "16128 16128 16128",
                targetname = "hlvrcoop_righthand_" .. i,
                model = "models/hands/alyx_glove_right.vmdl",
                solid = "0",
            }));
            table.insert(hlvrcoop_text, SpawnEntityFromTableSynchronous("point_worldtext", {
                origin = "16128 16128 16128",
                targetname = "hlvrcoop_text_" .. i,
                fullbright = "1",
                color = "255 255 255 255",
                world_units_per_pixel = "0.05",
                font_size = "128",
                justify_horizontal = "1",
                justify_vertical = "1",
                message = "Placeholder",
                font_name = "Arial Black",
                reorient_mode = "0",
                depth_render_offset = "0.125",
                rendercolor = "255 255 255 255",
                enabled = "1",
            }));
        end

        -- Interpolation helper function
        function InterpolateEntity(interp_state, entity, current_time)
            if not interp_state or not entity or not IsValidEntity(entity) then
                return
            end

            -- Skip if no network update received yet
            if interp_state.target_pos == nil or interp_state.target_ang == nil then return end

            -- First update - snap immediately
            if interp_state.prev_pos == nil then
                entity:SetAbsOrigin(interp_state.target_pos)
                entity:SetAbsAngles(interp_state.target_ang[1], interp_state.target_ang[2], interp_state.target_ang[3])
                interp_state.prev_pos = interp_state.target_pos
                interp_state.prev_ang = interp_state.target_ang
                return
            end

            -- Check if we haven't received updates for too long (>500ms)
            local time_since_update = current_time - interp_state.last_update
            if time_since_update > 0.5 then
                -- Snap to target and stop interpolating
                entity:SetAbsOrigin(interp_state.target_pos)
                entity:SetAbsAngles(interp_state.target_ang[1], interp_state.target_ang[2], interp_state.target_ang[3])
                interp_state.prev_pos = interp_state.target_pos
                interp_state.prev_ang = interp_state.target_ang
                return
            end

            -- Calculate interpolation factor t
            -- Network updates come at 10Hz (100ms window), so we use 0.1 as the base
            local t = time_since_update / 0.1
            -- Clamp t to [0, 1.2] for slight extrapolation
            if t < 0 then t = 0 end
            if t > 1.2 then t = 1.2 end

            -- Lerp position
            local lerp_pos = Vector(
                interp_state.prev_pos[1] + (interp_state.target_pos[1] - interp_state.prev_pos[1]) * t,
                interp_state.prev_pos[2] + (interp_state.target_pos[2] - interp_state.prev_pos[2]) * t,
                interp_state.prev_pos[3] + (interp_state.target_pos[3] - interp_state.prev_pos[3]) * t
            )

            -- Lerp angles
            local lerp_ang = Vector(
                interp_state.prev_ang[1] + (interp_state.target_ang[1] - interp_state.prev_ang[1]) * t,
                interp_state.prev_ang[2] + (interp_state.target_ang[2] - interp_state.prev_ang[2]) * t,
                interp_state.prev_ang[3] + (interp_state.target_ang[3] - interp_state.prev_ang[3]) * t
            )

            entity:SetAbsOrigin(lerp_pos)
            entity:SetAbsAngles(lerp_ang[1], lerp_ang[2], lerp_ang[3])
        end

        -- Main interpolation function called every tick
        function HLVRCOOP_Interpolate()
            local current_time = GameRules:GetGameTime()

            for i = 1, hlvrcoop_player_count do
                local idx = i
                local interp = hlvrcoop_interp[i]

                if interp then
                    -- Interpolate head
                    if hlvrcoop_heads[idx] and IsValidEntity(hlvrcoop_heads[idx]) then
                        InterpolateEntity(interp.head, hlvrcoop_heads[idx], current_time)
                    end

                    -- Interpolate left hand
                    if hlvrcoop_lefthands[idx] and IsValidEntity(hlvrcoop_lefthands[idx]) then
                        InterpolateEntity(interp.lefthand, hlvrcoop_lefthands[idx], current_time)
                    end

                    -- Interpolate right hand
                    if hlvrcoop_righthands[idx] and IsValidEntity(hlvrcoop_righthands[idx]) then
                        InterpolateEntity(interp.righthand, hlvrcoop_righthands[idx], current_time)
                    end
                end
            end
        end

        Convars:RegisterCommand("hlvrcoop_teleport", function(command, x, y, z)
            if Player then
                local anchor = Player:GetHMDAnchor();
                if Player:GetHMDAvatar() then
                    anchor:SetAbsOrigin(Vector(tonumber(x), tonumber(y), tonumber(z)));
                else
                    Player:SetAbsOrigin(Vector(tonumber(x), tonumber(y), tonumber(z)));
                end
            end
        end, "HLVR Co-Op Mod", 0);

        Convars:RegisterCommand("hlvrcoop_teleportangles", function(command, x, y, z, pitch, yaw, roll)
            if Player then
                local anchor = Player:GetHMDAnchor();
                if Player:GetHMDAvatar() then
                    anchor:SetAbsOrigin(Vector(tonumber(x), tonumber(y), tonumber(z)));
                    anchor:SetAbsAngles(tonumber(pitch), tonumber(yaw), tonumber(roll));
                else
                    Player:SetAbsOrigin(Vector(tonumber(x), tonumber(y), tonumber(z)));
                    Player:SetAbsAngles(tonumber(pitch), tonumber(yaw), tonumber(roll));
                end
            end
        end, "HLVR Co-Op Mod", 0);

        Convars:RegisterCommand("hlvrcoop_setlocation", function(command, name, x, y, z, pitch, yaw, roll)
            if not HLVRCOOP_USE_UUIDS then
                local entities = Entities:FindAllByName(name);
                if entities ~= nil then
                    for _, entity in pairs(entities) do
                        local class = entity:GetClassname();
                        if hlvrcoop_toggletypes[class] ~= nil then
                            DoEntFireByInstanceHandle(entity, hlvrcoop_toggletypes[class][1], "", 0, nil, nil);
                        end
                        entity:SetAbsOrigin(Vector(tonumber(x), tonumber(y), tonumber(z)));
                        entity:SetAbsAngles(tonumber(pitch), tonumber(yaw), tonumber(roll));
                    end
                end
            else
                local object = HLVRCOOP_ENTCACHE[name];
                if not object then return end
                local entity = object.entity;
                if not IsValidEntity(entity) then return end
                if hlvrcoop_toggletypes[object.class] ~= nil then
                    DoEntFireByInstanceHandle(entity, hlvrcoop_toggletypes[object.class][1], "", 0, nil, nil);
                end
                entity:SetAbsOrigin(Vector(tonumber(x), tonumber(y), tonumber(z)));
                entity:SetAbsAngles(tonumber(pitch), tonumber(yaw), tonumber(roll));
            end
        end, "HLVR Co-Op Mod", 0);

        Convars:RegisterCommand("hlvrcoop_setlocation_nonuuid", function(command, name, x, y, z, pitch, yaw, roll)
            local entities = Entities:FindAllByName(name);
            if entities ~= nil then
                for _, entity in pairs(entities) do
                    local class = entity:GetClassname();
                    if hlvrcoop_toggletypes[class] ~= nil then
                        DoEntFireByInstanceHandle(entity, hlvrcoop_toggletypes[class][1], "", 0, nil, nil);
                    end
                    entity:SetAbsOrigin(Vector(tonumber(x), tonumber(y), tonumber(z)));
                    entity:SetAbsAngles(tonumber(pitch), tonumber(yaw), tonumber(roll));
                end
            end
        end, "HLVR Co-Op Mod", 0);

        -- Consolidated head + text + name command
        -- Format: hlvrcoop_setlocation_head <index> <hx> <hy> <hz> <hp> <hyaw> <hr> <tx> <ty> <tz> |<name>
        Convars:RegisterCommand("hlvrcoop_setlocation_head", function(command, index, hx, hy, hz, hp, hyaw, hr, tx, ty, tz, nameRaw)
            local i = tonumber(index)
            local idx = i + 1
            local current_time = GameRules:GetGameTime()

            -- Parse name: | delimiter separates last coordinate from the display name
            -- nameRaw may contain the name after |, or if Convars splits on spaces,
            -- the caller uses |prefix to capture the rest
            local displayName = nameRaw or ""
            -- Strip leading | if present
            if string.sub(displayName, 1, 1) == "|" then
                displayName = string.sub(displayName, 2)
            end

            -- Update head interpolation state
            if hlvrcoop_interp[idx] and hlvrcoop_heads[idx] and IsValidEntity(hlvrcoop_heads[idx]) then
                local new_pos = Vector(tonumber(hx), tonumber(hy), tonumber(hz))
                local new_ang = Vector(tonumber(hp), tonumber(hyaw), tonumber(hr))

                -- Store current target as previous if we have one
                if hlvrcoop_interp[idx].head.target_pos ~= nil then
                    hlvrcoop_interp[idx].head.prev_pos = hlvrcoop_interp[idx].head.target_pos
                    hlvrcoop_interp[idx].head.prev_ang = hlvrcoop_interp[idx].head.target_ang
                end

                -- Store new values as target
                hlvrcoop_interp[idx].head.target_pos = new_pos
                hlvrcoop_interp[idx].head.target_ang = new_ang
                hlvrcoop_interp[idx].head.last_update = current_time

                -- First update - snap immediately (don't interpolate from 16128,16128,16128)
                if hlvrcoop_interp[idx].head.prev_pos == nil then
                    hlvrcoop_heads[idx]:SetAbsOrigin(new_pos)
                    hlvrcoop_heads[idx]:SetAbsAngles(tonumber(hp), tonumber(hyaw), tonumber(hr))
                    hlvrcoop_interp[idx].head.prev_pos = new_pos
                    hlvrcoop_interp[idx].head.prev_ang = new_ang
                end
            end

            -- Update text entity position (no interpolation needed for text)
            if hlvrcoop_text[idx] and IsValidEntity(hlvrcoop_text[idx]) then
                hlvrcoop_text[idx]:SetAbsOrigin(Vector(tonumber(tx), tonumber(ty), tonumber(tz)));
                -- Update the text message via DoEntFireByInstanceHandle
                if displayName ~= "" then
                    DoEntFireByInstanceHandle(hlvrcoop_text[idx], "SetMessage", displayName, 0, nil, nil);
                end
            end
        end, "HLVR Co-Op Mod", 0);

        -- Consolidated hands command
        -- Format: hlvrcoop_setlocation_hands <index> <lx> <ly> <lz> <lp> <lyaw> <lr> <rx> <ry> <rz> <rp> <ryaw> <rr>
        Convars:RegisterCommand("hlvrcoop_setlocation_hands", function(command, index, lx, ly, lz, lp, lyaw, lr, rx, ry, rz, rp, ryaw, rr)
            local i = tonumber(index)
            local idx = i + 1
            local current_time = GameRules:GetGameTime()

            -- Update left hand interpolation state
            if hlvrcoop_interp[idx] and hlvrcoop_lefthands[idx] and IsValidEntity(hlvrcoop_lefthands[idx]) then
                local new_pos = Vector(tonumber(lx), tonumber(ly), tonumber(lz))
                local new_ang = Vector(tonumber(lp), tonumber(lyaw), tonumber(lr))

                -- Store current target as previous if we have one
                if hlvrcoop_interp[idx].lefthand.target_pos ~= nil then
                    hlvrcoop_interp[idx].lefthand.prev_pos = hlvrcoop_interp[idx].lefthand.target_pos
                    hlvrcoop_interp[idx].lefthand.prev_ang = hlvrcoop_interp[idx].lefthand.target_ang
                end

                -- Store new values as target
                hlvrcoop_interp[idx].lefthand.target_pos = new_pos
                hlvrcoop_interp[idx].lefthand.target_ang = new_ang
                hlvrcoop_interp[idx].lefthand.last_update = current_time

                -- First update - snap immediately
                if hlvrcoop_interp[idx].lefthand.prev_pos == nil then
                    hlvrcoop_lefthands[idx]:SetAbsOrigin(new_pos)
                    hlvrcoop_lefthands[idx]:SetAbsAngles(tonumber(lp), tonumber(lyaw), tonumber(lr))
                    hlvrcoop_interp[idx].lefthand.prev_pos = new_pos
                    hlvrcoop_interp[idx].lefthand.prev_ang = new_ang
                end
            end

            -- Update right hand interpolation state
            if hlvrcoop_interp[idx] and hlvrcoop_righthands[idx] and IsValidEntity(hlvrcoop_righthands[idx]) then
                local new_pos = Vector(tonumber(rx), tonumber(ry), tonumber(rz))
                local new_ang = Vector(tonumber(rp), tonumber(ryaw), tonumber(rr))

                -- Store current target as previous if we have one
                if hlvrcoop_interp[idx].righthand.target_pos ~= nil then
                    hlvrcoop_interp[idx].righthand.prev_pos = hlvrcoop_interp[idx].righthand.target_pos
                    hlvrcoop_interp[idx].righthand.prev_ang = hlvrcoop_interp[idx].righthand.target_ang
                end

                -- Store new values as target
                hlvrcoop_interp[idx].righthand.target_pos = new_pos
                hlvrcoop_interp[idx].righthand.target_ang = new_ang
                hlvrcoop_interp[idx].righthand.last_update = current_time

                -- First update - snap immediately
                if hlvrcoop_interp[idx].righthand.prev_pos == nil then
                    hlvrcoop_righthands[idx]:SetAbsOrigin(new_pos)
                    hlvrcoop_righthands[idx]:SetAbsAngles(tonumber(rp), tonumber(ryaw), tonumber(rr))
                    hlvrcoop_interp[idx].righthand.prev_pos = new_pos
                    hlvrcoop_interp[idx].righthand.prev_ang = new_ang
                end
            end
        end, "HLVR Co-Op Mod", 0);

        Convars:RegisterCommand("hlvrcoop_fireoutput", function(command, name, type)
            if not HLVRCOOP_USE_UUIDS then
                local entities = Entities:FindAllByName(name);
                if entities ~= nil then
                    for _, entity in pairs(entities) do
                        local class = entity:GetClassname();
                        if class == "trigger_once" and HLVRCOOP_FIRED_TRIGGERS[name] then
                            DoEntFireByInstanceHandle(entity, "Disable", "", 0, nil, nil);
                        else
                            entity:FireOutput(type, Player, Player, {}, 0);
                            if class == "trigger_once" then
                                HLVRCOOP_FIRED_TRIGGERS[name] = true;
                                DoEntFireByInstanceHandle(entity, "Disable", "", 0, nil, nil);
                            end
                        end
                    end
                end
            else
                local object = HLVRCOOP_ENTCACHE[name];
                if not object then return end
                local entity = object.entity;
                if not IsValidEntity(entity) then return end
                if object.class == "trigger_once" and HLVRCOOP_FIRED_TRIGGERS[name] then
                    DoEntFireByInstanceHandle(entity, "Disable", "", 0, nil, nil);
                else
                    entity:FireOutput(type, Player, Player, {}, 0);
                    if object.class == "trigger_once" then
                        HLVRCOOP_FIRED_TRIGGERS[name] = true;
                        DoEntFireByInstanceHandle(entity, "Disable", "", 0, nil, nil);
                    end
                end
            end
        end, "HLVR Co-Op Mod", 0);

        Convars:RegisterCommand("hlvrcoop_grace", function(command, name, type)
            if not HLVRCOOP_USE_UUIDS then
                local entities = Entities:FindAllByName(name);
                if entities ~= nil then
                    for _, entity in pairs(entities) do
                        local class = entity:GetClassname();
                        if hlvrcoop_toggletypes[class] ~= nil then
                            DoEntFireByInstanceHandle(entity, hlvrcoop_toggletypes[class][2], "", 0, nil, nil);
                        end
                    end
                end
            else
                local object = HLVRCOOP_ENTCACHE[name];
                if not object then return end
                local entity = object.entity;
                if not IsValidEntity(entity) then return end
                if hlvrcoop_toggletypes[object.class] ~= nil then
                    DoEntFireByInstanceHandle(entity, hlvrcoop_toggletypes[object.class][2], "", 0, nil, nil);
                end
            end
        end, "HLVR Co-Op Mod", 0);

        Convars:RegisterCommand("hlvrcoop_npc_sethealth", function(command, name, health)
            if not HLVRCOOP_USE_UUIDS then
                local entities = Entities:FindAllByName(name);
                if entities ~= nil then
                    for _, entity in pairs(entities) do
                        local curhealth = entity:GetHealth();
                        if curhealth <= 0 then return end
                        if health < curhealth then
                            entity:SetHealth(tonumber(health));
                            local newhealth = entity:GetHealth();
                            if newhealth <= 0 then
                                entity:SetHealth(0);
                                StartSoundEvent("Combat.PlayerKilledNPC", Entities:GetLocalPlayer());
                                DoEntFireByInstanceHandle(entity, "BecomeRagdoll", "", 0, nil, nil);
                            end
                        end
                    end
                    --DoEntFireByInstanceHandle(entity, "StopTemporaryRagdoll", "", 0, nil, nil);
                    --DoEntFireByInstanceHandle(entity, "BecomeTemporaryRagdoll", "", 0, nil, nil);
                end
            else
                local object = HLVRCOOP_ENTCACHE[name];
                if not object then return end
                local entity = object.entity;
                if not IsValidEntity(entity) then return end
                local curhealth = entity:GetHealth();
                if curhealth <= 0 then return end
                entity:SetHealth(tonumber(health));
                local newhealth = entity:GetHealth();
                if newhealth < curhealth then
                    StartSoundEvent("DamageNPC.Bullet", Entities:GetLocalPlayer());
                end
                if newhealth <= 0 then
                    entity:SetHealth(0);
                    StartSoundEvent("Combat.PlayerKilledNPC", Entities:GetLocalPlayer());
                    DoEntFireByInstanceHandle(entity, "BecomeRagdoll", "", 0, nil, nil);
                end
            end
        end, "HLVR Co-Op Mod", 0);

        Convars:RegisterCommand("hlvrcoop_break", function(command, name)
            if not HLVRCOOP_USE_UUIDS then
                local entities = Entities:FindAllByName(name);
                if entities ~= nil then
                    for _, entity in pairs(entities) do
                        DoEntFireByInstanceHandle(entity, "Break", "", 0, nil, nil);
                    end
                end
            else
                local object = HLVRCOOP_ENTCACHE[name];
                if not object then return end
                local entity = object.entity;
                if not IsValidEntity(entity) then return end
                DoEntFireByInstanceHandle(entity, "Break", "", 0, nil, nil);
            end
        end, "HLVR Co-Op Mod", 0);

        Convars:RegisterCommand("hlvrcoop_sethealth", function(command, health)
            if Player then
                Player:SetHealth(tonumber(health));
            end
        end, "HLVR Co-Op Mod", 0);

        Convars:RegisterCommand("hlvrcoop_command", function(command, hlvrcoopcommand)
            HLVRCOOP_SEQ = HLVRCOOP_SEQ + 1;
            print("CMND "..hlvrcoopcommand.." "..HLVRCOOP_SEQ.." "..GameRules:GetGameTime().." HLVRCOOP");
        end, "HLVR Co-Op Mod", 0);

        Convars:RegisterCommand("hlvrcoop_cache_entity", function(command, cacheent)
            if not HLVRCOOP_USE_UUIDS then
                local entities = Entities:FindAllByName(cacheent);
                if entities ~= nil then
                    for _, entity in pairs(entities) do
                        if IsValidEntity(entity) then
                            HLVRCOOP_EntitySyncSpecific(entity);
                        end
                    end
                end
            else
                local object = HLVRCOOP_ENTCACHE[cacheent];
                if not object then return end
                local entity = object.entity;
                if not IsValidEntity(entity) then return end
                HLVRCOOP_EntitySyncSpecific(entity);
            end
        end, "HLVR Co-Op Mod", 0);

        Convars:RegisterCommand("hlvrcoop_cache_entity_nonuuid", function(command, cacheent)
            print("Templated: "..cacheent);
            local entities = Entities:FindAllByName(cacheent);
            if entities ~= nil then
                for _, entity in pairs(entities) do
                    if IsValidEntity(entity) then
                        HLVRCOOP_EntitySyncSpecific(entity);
                    end
                end
            end
        end, "HLVR Co-Op Mod", 0);

        Convars:RegisterCommand("hlvrcoop_cache_all_entities", function(command)
            HLVRCOOP_EntitySync(false)
        end, "HLVR Co-Op Mod", 0);

        -- Phase 3: Physics ownership protocol
        HLVRCOOP_OWNED_ENTITIES = {};
        Convars:RegisterCommand("hlvrcoop_ownership", function(command, action, entityName, ownerIndex)
            if action == "grant" then
                HLVRCOOP_OWNED_ENTITIES[entityName] = true;
            elseif action == "deny" then
                HLVRCOOP_OWNED_ENTITIES[entityName] = nil;
            elseif action == "lock" then
                HLVRCOOP_OWNED_ENTITIES[entityName] = true;
            elseif action == "release" then
                HLVRCOOP_OWNED_ENTITIES[entityName] = nil;
            end
        end, "HLVR Co-Op Mod", 0);

        -- Phase 3: NPC collider sync (received from other clients)
        Convars:RegisterCommand("hlvrcoop_npc_collider", function(command, name, x, y, z)
            -- Store collider info for late-join sync awareness
        end, "HLVR Co-Op Mod", 0);

        -- Phase 3: Damage sync (received from other clients)
        Convars:RegisterCommand("hlvrcoop_damage", function(command, target, damage, attackerIndex)
            if not HLVRCOOP_USE_UUIDS then
                local entities = Entities:FindAllByName(target);
                if entities ~= nil then
                    for _, entity in pairs(entities) do
                        local health = entity:GetHealth();
                        if health > 0 then
                            entity:SetHealth(math.max(0, health - tonumber(damage)));
                            if entity:GetHealth() <= 0 then
                                DoEntFireByInstanceHandle(entity, "BecomeRagdoll", "", 0, nil, nil);
                            end
                        end
                    end
                end
            else
                local object = HLVRCOOP_ENTCACHE[target];
                if not object then return end
                local entity = object.entity;
                if not IsValidEntity(entity) then return end
                local health = entity:GetHealth();
                if health > 0 then
                    entity:SetHealth(math.max(0, health - tonumber(damage)));
                    if entity:GetHealth() <= 0 then
                        DoEntFireByInstanceHandle(entity, "BecomeRagdoll", "", 0, nil, nil);
                    end
                end
            end
        end, "HLVR Co-Op Mod", 0);

        -- Phase 3: Resin sync (received from other clients)
        Convars:RegisterCommand("hlvrcoop_setresin", function(command, playerIndex, resinCount)
            -- Resin count sync - other player's resin deposited
        end, "HLVR Co-Op Mod", 0);

        -- Phase 4: Player body tracking from other clients
        Convars:RegisterCommand("hlvrcoop_setbody", function(command, index, x, y, z, pitch, yaw, roll, health)
            local idx = tonumber(index) + 1
            if idx >= 1 and idx <= hlvrcoop_player_count then
                hlvrcoop_player_body = hlvrcoop_player_body or {}
                hlvrcoop_player_body[idx] = {
                    origin = Vector(tonumber(x), tonumber(y), tonumber(z)),
                    angles = Vector(tonumber(pitch), tonumber(yaw), tonumber(roll)),
                    health = tonumber(health)
                }
            end
        end, "HLVR Co-Op Mod", 0);

        HLVRCOOP_SEQ = HLVRCOOP_SEQ + 1;
        print("MAPN "..GetMapName().." "..HLVRCOOP_API_VERSION.." "..HLVRCOOP_SEQ.." "..GameRules:GetGameTime().." HLVRCOOP");
        HLVRCOOP_EntitySync(true);
        
        -- Initialize spawn position on map load
        HLVRCOOP_FindSpawnPosition();
        previousHealth = 100; -- Reset health tracking on map load
        respawnCooldown = 0; -- Reset cooldown on map load
        respawnScheduled = false; -- Reset respawn flag on map load
        
        -- Also listen for player_death event as a backup detection method
        -- This is registered during initialization and will persist
        local function HLVRCOOP_OnPlayerDeathEvent(eventData)
            -- Event fires when player dies - immediately restore health and schedule teleport
            -- Use a more robust check to prevent race conditions
            if Player and IsValidEntity(Player) and not respawnScheduled then
                -- Set flag immediately to prevent race conditions
                respawnScheduled = true;
                
                -- Immediately restore health to prevent death screen
                -- Double-check Player is still valid (defensive programming)
                if IsValidEntity(Player) then
                    Player:SetHealth(100);
                    previousHealth = 100;
                    
                    -- Stop any existing respawn think first (in case health check also triggered)
                    Player:StopThink(respawnThinkName);
                    
                    -- Schedule teleport 1 second from now
                    Player:SetThink(HLVRCOOP_RespawnTeleport, respawnThinkName, 1.0);
                    print("HLVRCOOP: Player death event detected, health restored. Teleporting to spawn in 1 second");
                else
                    -- Player became invalid, reset flag
                    respawnScheduled = false;
                end
            end
        end
        ListenToGameEvent("player_death", HLVRCOOP_OnPlayerDeathEvent, nil);
    end
end