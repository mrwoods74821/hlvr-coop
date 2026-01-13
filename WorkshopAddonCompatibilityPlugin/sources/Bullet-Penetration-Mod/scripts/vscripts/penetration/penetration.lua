-- ======================== BULLET PENETRATION SCRIPT BY CEMENTSTAIRS ========================

require("penetration/penetration_util")

-- =========== GLOBAL SETTINGS ===========

PENETRATE_MAX = 5
PENETRATE_INCREMENT = 1
PENETRATE_OFFSET = 0.12
PENETRATE_THICKNESS = 32
PENETRATE_THICKNESS_TOTAL = 1024
PENETRATE_MAX_DISTANCE = 2000
PENETRATE_PARTICLE_OFFSET = 10

PENETRATE_MASK = 4097

PENETRATE_PROP_OFFSET = 1
PENETRATE_BACKSHOOT_CHECK = 0.09

PENETRATE_HULL_TOLERANCE_MAX = 0.01
PENETRATE_HULL_TOLERANCE_MIN = 0.15


PENETRATE_BLACKLIST_OFFSET = 1
PENETRATE_BLACKLIST_INCREMENT = 0.5

PENETRATE_HIT_PUSH = 0
PENETRATE_HIT_DMG = 8
PENETRATE_BULLETS = 1
PENETRATE_SPREAD = 0

PENETRATE_ENTITY_WHITELIST_BLOCK = {"^npc_.*$", "^prop_ragdoll.*$", "item_hlvr_grenade_frag","item_hlvr_grenade_xen", "generic_actor", "player", "xen_foliage_bloater"}
PENETRATE_ENTITY_WHITELIST_PENETRATABLE = {"worldent", "^prop_.*$", "^item_hlvr_.*$", "func_brush", "^func_.*$"}
PENETRATE_ENTITY_BLACKLIST_BACKSHOOTING = {"prop_physics", "prop_physics_override", "prop_animinteractable", "prop_door_rotating_physics", "func_physbox", "func_movelinear", "func_tracktrain", "^func_door.*$", "func_rotating", "^item_hlvr_.*$"}

PENETRATE_ENTITY_WHITELIST_HURTABLE = {"^npc_.*$", "^prop_.*$",  "func_physbox", "^item_hlvr_.*$", "xen_foliage_bloater"}

TANK_MAX_NUMBER = 30
TANK_QUEUE_SPEED = 0.01
currentTankIndex = 1

tankQueueTable = {}
tankQueueTable_back = {}
tankEntTable = {}

-- =========== WEAPON SPECIFIC SETTINGS ===========

-- == PISTOL ==
WEAPON_PISTOL_PENETRATE_MAX = 4
WEAPON_PISTOL_PENETRATE_THICKNESS = 10
WEAPON_PISTOL_PENETRATE_THICKNESS_TOTAL = 192
WEAPON_PISTOL_PENETRATE_MAX_DISTANCE = 3000
WEAPON_PISTOL_PENETRATE_HIT_PUSH = 0
WEAPON_PISTOL_PENETRATE_HIT_DMG = Convars:GetFloat("vr_energygun_dmg") or 8
WEAPON_PISTOL_PENETRATE_SPREAD = 0
WEAPON_PISTOL_PENETRATE_BULLETS = 1

-- == SMG ==
WEAPON_SMG_PENETRATE_MAX = 3
WEAPON_SMG_PENETRATE_THICKNESS = 12
WEAPON_SMG_PENETRATE_THICKNESS_TOTAL = 192
WEAPON_SMG_PENETRATE_MAX_DISTANCE = 3000
WEAPON_SMG_PENETRATE_HIT_PUSH = 0
WEAPON_SMG_PENETRATE_HIT_DMG = Convars:GetFloat("vr_rapidfire_dmg") or 4
WEAPON_SMG_PENETRATE_SPREAD = 0
WEAPON_SMG_PENETRATE_BULLETS = 1

-- == SHOTGUN ==
WEAPON_SHOTGUN_PENETRATE_MAX = 3
WEAPON_SHOTGUN_PENETRATE_THICKNESS = 16
WEAPON_SHOTGUN_PENETRATE_THICKNESS_TOTAL = 256
WEAPON_SHOTGUN_PENETRATE_MAX_DISTANCE = 1000
WEAPON_SHOTGUN_PENETRATE_HIT_PUSH = 0
WEAPON_SHOTGUN_PENETRATE_HIT_DMG = Convars:GetFloat("sk_npc_dmg_buckshot") or 8
WEAPON_SHOTGUN_PENETRATE_SPREAD = 0.1
WEAPON_SHOTGUN_PENETRATE_BULLETS = 7

-- == CUSTOM ==
WEAPON_CUSTOM_PENETRATE_MAX = 4
WEAPON_CUSTOM_PENETRATE_THICKNESS = 10
WEAPON_CUSTOM_PENETRATE_THICKNESS_TOTAL = 192
WEAPON_CUSTOM_PENETRATE_MAX_DISTANCE = 3000
WEAPON_CUSTOM_PENETRATE_HIT_PUSH = 0
WEAPON_CUSTOM_PENETRATE_HIT_DMG = Convars:GetFloat("vr_energygun_dmg") or 8
WEAPON_CUSTOM_PENETRATE_SPREAD = 0
WEAPON_CUSTOM_PENETRATE_BULLETS = 1


-- =========== INITIALIZATION ===========

NoVR = GlobalSys:CommandLineCheck('-novr')

function Activate()
    print("Bullet Penetration Script is Running...")
    setUpTank()

    if NoVR then
        Convars:RegisterCommand("+holdingM1", createPenetration , "", 0)
        SendToServerConsole([[bind MOUSE1 "+holdingM1"]])
    else
        listenShoot = ListenToGameEvent("player_shoot_weapon", createPenetration, nil)
    end
end

function Precache(context)
    context:AddResource("models/penetration_tank.vmdl")
end

-- =========== PENETRATION CODE ===========

function createPenetration()
    -- Initial penetration function; Mainly decides whether the first entity hit blocks shots, and thus not starting the more complex penetration function

    local fromEntity = determineEnt()
    local startWeaponInfo = getWeaponInfo(fromEntity)
    local ignoreEnt = fromEntity
    local startVectorForward = fromEntity:GetAttachmentForward(fromEntity:ScriptLookupAttachment('muzzle')) or fromEntity:GetForwardVector()
    local startVector

    if NoVR then
        novrStopFiring()
        startVector = Entities:GetLocalPlayer():EyePosition()
    else
        startVector = fromEntity:GetAttachmentOrigin(fromEntity:ScriptLookupAttachment('muzzle')) or Entities:GetLocalPlayer():EyePosition()
    end 

    local traceTable =
            {
                startpos = startVector;
                endpos = startVector + moveAlongForward(startVectorForward, startWeaponInfo.weapon_maxdistance);
                ignore = fromEntity;
                mask = PENETRATE_MASK;
            }

    TraceLine(traceTable)

    if traceTable.hit and not hasClassname(traceTable.enthit, PENETRATE_ENTITY_WHITELIST_BLOCK) and hasClassname(traceTable.enthit, PENETRATE_ENTITY_WHITELIST_PENETRATABLE) then

        local startThickInfo = thicknessCheck(traceTable.pos+moveAlongForward(startVectorForward, startWeaponInfo.weapon_thickness), startVectorForward, fromEntity, startWeaponInfo, fromEntity)

        if startThickInfo.success then
            if not hasClassname(traceTable.enthit, "worldent") then
                if startThickInfo.inProp then
                    print("------------------------------\nPENETRATION NOT STARTED: Prop too thick...")
                    return
                else
                    ignoreEnt = traceTable.enthit
                end 
            else
                if traceTable.startsolid then
                    traceTable.pos = startThickInfo.thickPos
                end
            end

            print("\nPENETRATION STARTED\n------------------------------")
            penetrate(1,traceTable.fraction, traceTable.pos+moveAlongForward(startVectorForward, PENETRATE_OFFSET), fromEntity, startVectorForward, startThickInfo, ignoreEnt, startThickInfo.thickTotalFrac, startWeaponInfo, traceTable.startsolid, traceTable.enthit)
        else
            print("\nPENETRATION NOT STARTED: Wall too thick...\n------------------------------")
        end
    else
        print("\nPENETRATION NOT STARTED: Nothing to hit/Entity in the way...\n------------------------------")
    end
end

function penetrate(penetrate_times, fraction, startVector, fromEntity, forwardVector, thickInfo, ignoreEnt, totalThickFraction, weaponInfo, lastSolid, lastEnt)
    if fraction < 1 then
        if totalThickFraction < 1 then
            if penetrate_times <= weaponInfo.weapon_penetratemax then
                print("Penetrating for " .. penetrate_times .. ". time")

                local traceTable =
                {
                    startpos = startVector;
                    endpos = startVector + moveAlongForward(forwardVector, weaponInfo.weapon_maxdistance);
                    ignore = ignoreEnt;
                    mask = PENETRATE_MASK;
                }

                TraceLine(traceTable)

                if traceTable.hit then
                   -- DebugDrawLine(traceTable.startpos, traceTable.pos, 0, penetrate_times*30+100,penetrate_times*75,false,3)

                    fraction = fraction + traceTable.fraction

                    local moveTankBy
                    if hasClassname(thickInfo.thickEnt, PENETRATE_ENTITY_BLACKLIST_BACKSHOOTING) then
                        moveTankBy = PENETRATE_PROP_OFFSET
                    else
                        moveTankBy = PENETRATE_OFFSET
                    end

                    local tblTank = {origin = thickInfo.thickPos+moveAlongForward(forwardVector, moveTankBy); shootPos = thickInfo.thickPos+moveAlongForward(forwardVector, moveTankBy*2); dmg = RemapValClamped(fraction, 0, 1, weaponInfo.weapon_dmg, 0); shootSpread = weaponInfo.weapon_spread;shootEnt = lastEnt; shootLastEnt = traceTable.enthit;forwardVec = forwardVector;}
                    local tblTank_back = {origin = thickInfo.thickPos+moveAlongForward(forwardVector, moveTankBy); shootPos = thickInfo.thickPos; dmg = RemapValClamped(fraction, 0, 1, weaponInfo.weapon_dmg, 0); shootSpread = weaponInfo.weapon_spread; shootEnt = lastEnt; shootLastEnt = traceTable.enthit;forwardVec = forwardVector;}
                
                    if hasClassname(traceTable.enthit, PENETRATE_ENTITY_WHITELIST_HURTABLE) then 
                        hitEntity(traceTable.enthit, forwardVector, traceTable.pos, traceTable.normal, totalThickFraction, fromEntity, weaponInfo)
                    end

                    if hasClassname(traceTable.enthit, PENETRATE_ENTITY_WHITELIST_BLOCK) then
                        -- Hit an entity that blocks bullets - mostly NPCs; stopping penetration

                        addToQueue(tblTank, weaponInfo.weapon_bullets)
                        if not (traceTable.startsolid) and not (thickInfo.inProp) then addToQueue_back(tblTank_back, weaponInfo.weapon_bullets) end

                        print("------------------------------\nPENETRATION FINISHED: Hit an entity...")
                        return

                    elseif not hasClassname(traceTable.enthit, PENETRATE_ENTITY_WHITELIST_PENETRATABLE) then
                        -- Entity that wasn't whitelisted hit; Try to move - if hit more times or stuck in its collision then stopping penetration

                        if not traceTable.startsolid then
                            penetrate_times = penetrate_times + PENETRATE_BLACKLIST_INCREMENT
                            penetrate(penetrate_times, fraction, traceTable.pos + moveAlongForward(forwardVector, PENETRATE_BLACKLIST_OFFSET), fromEntity, forwardVector, thickInfo, ignoreEnt, totalThickFraction, weaponInfo, traceTable.startsolid, lastEnt)
                        else
                            print("------------------------------\nPENETRATION FINISHED: Stuck inside blacklisted entity...")
                            return
                        end
                    else
                        -- Hit an entity that allows for penetration - usually worldent(all baked mesh collisions) or props
                        if not (traceTable.startsolid) and not (thickInfo.inProp) then
                            addToQueue(tblTank, weaponInfo.weapon_bullets)
                            addToQueue_back(tblTank_back, weaponInfo.weapon_bullets) 
                        end

                        thickInfo = thicknessCheck(traceTable.pos+moveAlongForward(forwardVector, weaponInfo.weapon_thickness), forwardVector, fromEntity, weaponInfo, ignoreEnt)
                        if thickInfo.success then
                            totalThickFraction = totalThickFraction + thickInfo.thickTotalFrac
                            
                            if not hasClassname(traceTable.enthit, "worldent") then
                                if thickInfo.inProp then
                                    print("------------------------------\nPENETRATION FINISHED: Prop too thick...")
                                    return
                                else
                                    ignoreEnt = traceTable.enthit
                                end 
                            end

                            if traceTable.startsolid then
                                if not thickInfo.inProp then

                                    traceTable.pos = thickInfo.thickPos
                                        
                                    closenessCheck = thicknessCheck(traceTable.pos+moveAlongForward(forwardVector, PENETRATE_OFFSET), forwardVector, fromEntity, {weapon_thickness = -weaponInfo.weapon_maxdistance*(1-fraction); weapon_totalthickness = -weaponInfo.weapon_totalthickness}, ignoreEnt)
                                    local sameFrac = closenessCheck.thickFrac
                                   
                                    if closenessCheck.success then
                                        closenessCheck = thicknessCheck(closenessCheck.thickPos-moveAlongForward(forwardVector, PENETRATE_OFFSET), forwardVector, fromEntity, {weapon_thickness = weaponInfo.weapon_maxdistance*(1-fraction); weapon_totalthickness = weaponInfo.weapon_totalthickness}, ignoreEnt)
                                            
                                        if closenessCheck.success and (math.abs(closenessCheck.thickFrac - sameFrac) > PENETRATE_HULL_TOLERANCE_MAX) then
                                            print("------------------------------\nPENETRATION FINISHED: Props too close together and wall too thick...")
                                            return
                                        end
                                    else
                                        print("------------------------------\nPENETRATION FINISHED: Props too close together and nothing to hit...")
                                        return
                                    end
                                else
                                    print("------------------------------\nPENETRATION FINISHED: Props too close together and stuck in a prop...")
                                    return
                                end 
                            end

                            penetrate_times = penetrate_times + PENETRATE_INCREMENT                        
                    
                            penetrate(penetrate_times, fraction, traceTable.pos + moveAlongForward(forwardVector, PENETRATE_OFFSET), fromEntity, forwardVector, thickInfo, ignoreEnt, totalThickFraction, weaponInfo, traceTable.startsolid, traceTable.enthit)
                        else
                            print("------------------------------\nPENETRATION FINISHED: Wall too thick...")
                            return
                        end
                    end
                else
                    print("------------------------------\nPENETRATION FINISHED: Nothing to hit...")
                    return
                end
            else
                print("------------------------------\nPENETRATION FINISHED: Reached penetration max...")
                return
            end
        else
            print("------------------------------\nPENETRATION FINISHED: Reached thickness total...")
            return
        end
    else
        print("------------------------------\nPENETRATION FINISHED: Penetration too far...")
        return
    end
end

function thicknessCheck(thickStartVector, forwardThickVector, fromEntity, thickWeaponInfo, ignoreEnt)
    local traceThickTable =
            {
                startpos = thickStartVector;
                endpos = thickStartVector - moveAlongForward(forwardThickVector, thickWeaponInfo.weapon_thickness);
                ignore = ignoreEnt;
                mask = PENETRATE_MASK;
            }

    TraceLine(traceThickTable)

    if traceThickTable.hit then
       -- DebugDrawLine(traceThickTable.startpos, traceThickTable.pos, 100, 100,255,true,3)
       -- print("FREE SPACE LEFT: " .. VectorDistance(traceThickTable.startpos, traceThickTable.pos))
        --DebugDrawSphere(traceThickTable.pos,Vector(0,255,0),255,2,true,-1)
        local thicknessResultTable = {success = true; inProp = traceThickTable.startsolid; thickEnt = traceThickTable.enthit ; thickFrac = traceThickTable.fraction; thickPos = traceThickTable.pos; thickTotalFrac = ((1-traceThickTable.fraction)*thickWeaponInfo.weapon_thickness)/thickWeaponInfo.weapon_totalthickness;}
        return thicknessResultTable
    else
        --DebugDrawLine(traceThickTable.startpos, traceThickTable.endpos, 255, 100,100,true,3)
        --DebugDrawSphere(traceThickTable.endpos,Vector(255,0,0),255,3,true,-1)
        local thicknessResultTable = {success = false; inProp = traceThickTable.startsolid; thickFrac = traceThickTable.fraction; thickPos = traceThickTable.pos; thickTotalFrac = nil; thickEnt = traceThickTable.enthit;}
        return thicknessResultTable
    end
end

function moveAlongForward(forwardVec, amount)
   return forwardVec * amount
end

function hasClassname(ent, classname)
    if IsValidEntity(ent) then
        if type(classname) == "string" then
            if string.find(ent:GetClassname(), classname) then
                return true
            else
                return false
            end

        elseif type(classname) == "table" then
            for k, val in pairs(classname) do
                if string.find(ent:GetClassname(), val) then
                    return true
                end
            end

            return false
        end
    else
        return nil
    end
end
    
        
    

function createTracer(vStart, vEnd)
    local tracer = ParticleManager:CreateParticle("particles/tracer_fx/mg_heavy_tracer.vpcf", 0, determineEnt())
    ParticleManager:SetParticleControl(tracer, 0, vStart)
    ParticleManager:SetParticleControl(tracer, 1, vEnd)
    ParticleManager:ReleaseParticleIndex(tracer)
end

function hitEntity(hitEnt, hitForward, hitPos, hitNormal, hitFrac, hitInflictor, hitWeaponInfo)
    -- print("DMG DEALT: " .. tostring(RemapValClamped(hitFrac, 0, 1, hitWeaponInfo.weapon_dmg, 0)))

    hitEnt:ApplyAbsVelocityImpulse(hitForward*RemapValClamped(hitFrac, 0, 1, hitWeaponInfo.weapon_push, 0))
    -- Override inflictor to Player, originally was hitInflictor
    local dmgInfo = CreateDamageInfo(hitEnt, Entities:GetLocalPlayer(), RemapValClamped(hitFrac, 0, 1, hitWeaponInfo.weapon_push, 0)*hitForward, hitPos, RemapValClamped(hitFrac, 0, 1, hitWeaponInfo.weapon_dmg, 0)*hitWeaponInfo.weapon_bullets, DMG_BULLET)
    hitEnt:TakeDamage(dmgInfo)
    DestroyDamageInfo(dmgInfo)

    --[[
    local hitBlood = ParticleManager:CreateParticle("particles/blood_impact/blood_impact_red_01.vpcf", 0, determineEnt())
    ParticleManager:SetParticleControl(hitBlood, 0, hitPos)
    ParticleManager:SetParticleControl(hitBlood, 1, hitPos+hitNormal*10)
    ParticleManager:ReleaseParticleIndex(hitBlood)

    hitEnt:EmitSoundParams("DamageNPC.Bullet", 1, 3, 0)
    ]]--
end

function setUpTank()
    --[[
     This is the first time I/O starts breaking everything, but we still love it unconditionally.
     Can't create bullets with VScript, fine. We use func_tank entity. Fine. Func_tank can fire only after "cooldown" of the last shot ended. Cooldown is decided by firerate.
     Fine, so we increase firerate for it to be nearly instant? Why not, it's just that the I/O can't catch up with the func_tank, meaning we fire few more bullets at once - hundred more at once. 
     Fine, so we use firerate that is as fast as possible, without firing more shots at once. Very fine. Except it now takes around 0.11 seconds to fire shots by one func_tank. Very fu-, fine.
     So we spawn more func_tanks and cycle between ones that had already fired, so others may cool down, should work. But it's now around 0.11 seconds to fire a shot, which will be problematic, if we want to react to constantly
     changing circumstances - like a prop_physics quickly moving after being shot. But that's fine, we are fine gentlemen and we can handle this. Without too many tears...
    ]]--

    local isExisting = Entities:FindByName(nil, "@Penetration_Tank_*")

    if IsValidEntity(isExisting) and isExisting:GetClassname() == "func_tank" then
        print("TANK FOUND: Destroying...")
        killAllByName("@Penetration_Tank_*")
    end

    thisEntity:SetThink(function()
    for i = 1, TANK_MAX_NUMBER, 1 do
        local Tank = SpawnEntityFromTableSynchronous("func_tank",
        {
            targetname = "@Penetration_Tank_" .. i;
            origin = "0 0 0";
            angles = "0 0 0";
            alpha = "100";
            scales = "0.01 0.01 0.01";
            model = "models/penetration_tank.vmdl";
            rendermode = "kRenderNone";
            spawnflags = "525";
            yawrate = "360000000";
            yawrange = "360000";
            yawtolerance = "10000";
            pitchrate = "360000000";
            pitchrange = "360000";
            pitchtolerance = "10000";
            barrel = "0.0001";
            barrely = "0.0001";
            barrelz = "0.0001";
            firerate = "19";
            bullet_damage = "-1";
            bullet_damage_vs_player = "-1";
            persistence = "0";
            persistence2 = "0";
            effecthandling = "0";
            ammotype = "SMG1";
            firespread = "0";
            ignoregraceupto = "0";
        })
        table.insert(tankEntTable, Tank)

       DoEntFireByInstanceHandle(Tank, "AddOutput", "OnFire>!self>Deactivate>>0>-1", 0.01, self, self)
       DoEntFireByInstanceHandle(Tank, "SetTargetDir", "0 0 0", 0.01, self, self)
       DoEntFireByInstanceHandle(Tank, "Deactivate", "0 0 0", 0.015, self, self)
    end

    return end, UniqueString("SpawnTanks"), 0.01)
end

function addToQueue(tbl, times)
    if IsValidEntity(tankEntTable[currentTankIndex]) and tbl.dmg > 0 then
	   -- print("Added " .. tostring(tbl) .. " to table")
        for a = 1, times, 1 do 
	        table.insert(tankQueueTable, tbl)
        end

	    if not tankQueueRunning then
		    tankQueueRunning = true
		    thisEntity:SetThink(tankQueueThink, "tankQueueThink", 0) 
	    end
    end
end

function tankQueueThink()
    local current_Tank = tankEntTable[currentTankIndex]
    increaseTankIndex()

    if #tankQueueTable > 0 and IsValidEntity(current_Tank) then

        if  hasClassname(tankQueueTable[1].shootEnt, PENETRATE_ENTITY_BLACKLIST_BACKSHOOTING) then
            local isStableTable = tankQueueTable[1]
            local currentPos = isStableTable.shootEnt:transformToLocal(isStableTable.origin)
            local currentShootPos = isStableTable.shootEnt:transformToLocal(isStableTable.shootPos)
            current_Tank:SetThink(function() local newOrigin = isStableTable.shootEnt:transformToWorld(currentPos) current_Tank:SetAbsOrigin(newOrigin) return end, UniqueString("NOT_STABLE_THINK_FRONT"), PENETRATE_BACKSHOOT_CHECK)
        end
        print("FIRING BULLET FORWARD")

        current_Tank:SetAbsOrigin(tankQueueTable[1].origin)
        local spread = Vector(RandomFloat(-tankQueueTable[1].shootSpread, tankQueueTable[1].shootSpread), RandomFloat(-tankQueueTable[1].shootSpread, tankQueueTable[1].shootSpread), RandomFloat(-tankQueueTable[1].shootSpread, tankQueueTable[1].shootSpread))
        local tankAngle = VectorToAngles((tankQueueTable[1].shootPos-current_Tank:GetAbsOrigin()):Normalized()+spread)
        current_Tank:SetAngles(tankAngle.x, tankAngle.y, tankAngle.z)
 
        DoEntFireByInstanceHandle(current_Tank, "SetTargetDir", tostring(current_Tank:GetAngles().x .. " " .. current_Tank:GetAngles().y .. " " .. current_Tank:GetAngles().z), 0, self, self)
        DoEntFireByInstanceHandle(current_Tank, "Activate", "", 0, self, self)

        --print("Removed " .. tostring(tankQueueTable[1]) .. " from table")
        table.remove(tankQueueTable, 1)
    else
        tankQueueRunning = false
        thisEntity:StopThink("tankQueueThink")
        return
    end

    return TANK_QUEUE_SPEED
end

function addToQueue_back(tbl, times)
    if IsValidEntity(tankEntTable[currentTankIndex]) and tbl.dmg > 0 then
	   -- print("Added " .. tostring(tbl) .. " to table")
        for a = 1, times, 1 do 
	        table.insert(tankQueueTable_back, tbl)
        end

	    if not tankQueueRunning_back then
		    tankQueueRunning_back = true
		    thisEntity:SetThink(tankQueueThink_back, "tankQueueThink_back", 0.005) 
	    end
    end
end

function tankQueueThink_back()
    local current_Tank = tankEntTable[currentTankIndex]
    increaseTankIndex()

    if #tankQueueTable_back > 0 and IsValidEntity(current_Tank) then
        if IsValidEntity(tankQueueTable_back[1].shootEnt) then
            
            if  hasClassname(tankQueueTable_back[1].shootEnt, PENETRATE_ENTITY_BLACKLIST_BACKSHOOTING) then
                local isStableTable = tankQueueTable_back[1]
                local currentPos = isStableTable.shootEnt:transformToLocal(isStableTable.origin)
                local currentShootPos = isStableTable.shootEnt:transformToLocal(isStableTable.shootPos)
                current_Tank:SetThink(function() local newOrigin = isStableTable.shootEnt:transformToWorld(currentPos) local newShootPos = isStableTable.shootEnt:transformToWorld(currentShootPos) local newForwardVec = (newShootPos-newOrigin):Normalized() current_Tank:SetAbsOrigin(newOrigin) local newAngles = VectorToAngles(newForwardVec) current_Tank:SetAngles(newAngles.x, newAngles.y, newAngles.z) DoEntFireByInstanceHandle(current_Tank, "SetTargetDir", tostring(newAngles.x .. " " .. newAngles.y .. " " .. newAngles.z), 0, self, self) if not shootItself(isStableTable) then print("NOT STABLE") DoEntFireByInstanceHandle(current_Tank, "Deactivate", "", 0, self, self) end return end, UniqueString("NOT_STABLE_THINK"), PENETRATE_BACKSHOOT_CHECK)
            end

            print("FIRING BULLET BACKWARDS")
                current_Tank:SetAbsOrigin(tankQueueTable_back[1].origin)
                local spread = Vector(RandomFloat(-tankQueueTable_back[1].shootSpread, tankQueueTable_back[1].shootSpread), RandomFloat(-tankQueueTable_back[1].shootSpread, tankQueueTable_back[1].shootSpread), RandomFloat(-tankQueueTable_back[1].shootSpread, tankQueueTable_back[1].shootSpread))
                local tankAngle = VectorToAngles((tankQueueTable_back[1].shootPos-current_Tank:GetAbsOrigin()):Normalized()+spread)
                current_Tank:SetAngles(tankAngle.x, tankAngle.y, tankAngle.z)

                DoEntFireByInstanceHandle(current_Tank, "SetTargetDir", tostring(current_Tank:GetAngles().x .. " " .. current_Tank:GetAngles().y .. " " .. current_Tank:GetAngles().z), 0, self, self)
                DoEntFireByInstanceHandle(current_Tank, "Activate", "", 0, self, self)

                --print("Removed " .. tostring(tankQueueTable[1]) .. " from table")
        end

        table.remove(tankQueueTable_back, 1)
    else
        tankQueueRunning_back = false
        thisEntity:StopThink("tankQueueThink_back")
        return
    end

    return TANK_QUEUE_SPEED
end

function increaseTankIndex()
    if currentTankIndex >= TANK_MAX_NUMBER then
        currentTankIndex = 1
    else
        currentTankIndex = currentTankIndex + 1
    end
end

function determineEnt()
    if NoVR then
        -- print("DETERMINED AS PLAYER...")
        return Entities:GetLocalPlayer()
    else
        local tbl = {}
        Entities:GetLocalPlayer():GatherCriteria(tbl)
        -- print("DETERMINED AS " .. tostring(tbl.primaryhand_active_attachment) .. "...")
        return Entities:FindByClassnameNearest(tostring(tbl.primaryhand_active_attachment), Entities:GetLocalPlayer():GetCenter(), 256)
    end
end

function getWeaponInfo(weapon)
    local weaponInfoTable
    
    if weapon:GetClassname() == "hlvr_weapon_energygun" then
        weaponInfoTable = {weapon_penetratemax = Convars:GetInt("penetration_wpn_pistol_cvr_max") or WEAPON_PISTOL_PENETRATE_MAX; weapon_thickness = Convars:GetInt("penetration_wpn_pistol_cvr_thickness") or WEAPON_PISTOL_PENETRATE_THICKNESS; weapon_totalthickness = Convars:GetInt("penetration_wpn_pistol_cvr_totalthickness") or WEAPON_PISTOL_PENETRATE_THICKNESS_TOTAL; weapon_maxdistance = Convars:GetInt("penetration_wpn_pistol_cvr_maxdistance") or WEAPON_PISTOL_PENETRATE_MAX_DISTANCE; weapon_push = Convars:GetInt("penetration_wpn_pistol_cvr_push") or WEAPON_PISTOL_PENETRATE_HIT_PUSH; weapon_dmg = Convars:GetFloat("penetration_wpn_pistol_cvr_dmg") or WEAPON_PISTOL_PENETRATE_HIT_DMG; weapon_spread = Convars:GetFloat("penetration_wpn_pistol_cvr_spread") or WEAPON_PISTOL_PENETRATE_SPREAD; weapon_bullets = Convars:GetInt("penetration_wpn_pistol_cvr_bullets") or WEAPON_PISTOL_PENETRATE_BULLETS;}
    elseif weapon:GetClassname() == "hlvr_weapon_rapidfire" then
        weaponInfoTable = {weapon_penetratemax = Convars:GetInt("penetration_wpn_smg_cvr_max") or WEAPON_SMG_PENETRATE_MAX; weapon_thickness = Convars:GetInt("penetration_wpn_smg_cvr_thickness") or WEAPON_SMG_PENETRATE_THICKNESS; weapon_totalthickness = Convars:GetInt("penetration_wpn_smg_cvr_totalthickness") or WEAPON_SMG_PENETRATE_THICKNESS_TOTAL; weapon_maxdistance = Convars:GetInt("penetration_wpn_smg_cvr_maxdistance") or WEAPON_SMG_PENETRATE_MAX_DISTANCE; weapon_push = Convars:GetInt("penetration_wpn_smg_cvr_push") or WEAPON_SMG_PENETRATE_HIT_PUSH; weapon_dmg = Convars:GetFloat("penetration_wpn_smg_cvr_dmg") or WEAPON_SMG_PENETRATE_HIT_DMG; weapon_spread = Convars:GetFloat("penetration_wpn_smg_cvr_spread") or WEAPON_SMG_PENETRATE_SPREAD; weapon_bullets = Convars:GetInt("penetration_wpn_smg_cvr_bullets") or WEAPON_SMG_PENETRATE_BULLETS;}
    elseif weapon:GetClassname() == "hlvr_weapon_shotgun" then
        weaponInfoTable = {weapon_penetratemax = Convars:GetInt("penetration_wpn_shotgun_cvr_max") or WEAPON_SHOTGUN_PENETRATE_MAX; weapon_thickness = Convars:GetInt("penetration_wpn_shotgun_cvr_thickness") or WEAPON_SHOTGUN_PENETRATE_THICKNESS; weapon_totalthickness = Convars:GetInt("penetration_wpn_shotgun_cvr_totalthickness") or WEAPON_SHOTGUN_PENETRATE_THICKNESS_TOTAL; weapon_maxdistance = Convars:GetInt("penetration_wpn_shotgun_cvr_maxdistance") or WEAPON_SHOTGUN_PENETRATE_MAX_DISTANCE; weapon_push = Convars:GetInt("penetration_wpn_shotgun_cvr_push") or WEAPON_SHOTGUN_PENETRATE_HIT_PUSH; weapon_dmg = Convars:GetFloat("penetration_wpn_shotgun_cvr_dmg") or WEAPON_SHOTGUN_PENETRATE_HIT_DMG; weapon_spread = Convars:GetFloat("penetration_wpn_shotgun_cvr_spread") or WEAPON_SHOTGUN_PENETRATE_SPREAD; weapon_bullets = Convars:GetInt("penetration_wpn_shotgun_cvr_bullets") or WEAPON_SHOTGUN_PENETRATE_BULLETS;}
    elseif weapon:GetClassname() == "hlvr_weapon_generic_pistol" then
        weaponInfoTable = {weapon_penetratemax = Convars:GetInt("penetration_wpn_custom_cvr_max") or WEAPON_CUSTOM_PENETRATE_MAX; weapon_thickness = Convars:GetInt("penetration_wpn_custom_cvr_thickness") or WEAPON_CUSTOM_PENETRATE_THICKNESS; weapon_totalthickness = Convars:GetInt("penetration_wpn_custom_cvr_totalthickness") or WEAPON_CUSTOM_PENETRATE_THICKNESS_TOTAL; weapon_maxdistance = Convars:GetInt("penetration_wpn_custom_cvr_maxdistance") or WEAPON_CUSTOM_PENETRATE_MAX_DISTANCE; weapon_push = Convars:GetInt("penetration_wpn_custom_cvr_push") or WEAPON_CUSTOM_PENETRATE_HIT_PUSH; weapon_dmg = Convars:GetFloat("penetration_wpn_custom_cvr_dmg") or WEAPON_CUSTOM_PENETRATE_HIT_DMG; weapon_spread = Convars:GetFloat("penetration_wpn_custom_cvr_spread") or WEAPON_CUSTOM_PENETRATE_SPREAD; weapon_bullets = Convars:GetInt("penetration_wpn_custom_cvr_bullets") or WEAPON_CUSTOM_PENETRATE_BULLETS;}
    else
        weaponInfoTable = {weapon_penetratemax = PENETRATE_MAX; weapon_thickness = PENETRATE_THICKNESS; weapon_totalthickness = PENETRATE_THICKNESS_TOTAL; weapon_maxdistance = PENETRATE_MAX_DISTANCE; weapon_push = PENETRATE_HIT_PUSH; weapon_dmg = PENETRATE_HIT_DMG; weapon_spread = PENETRATE_SPREAD; weapon_bullets = PENETRATE_BULLETS;}
    end

    return weaponInfoTable
end

function killAllByName(name)
    for k, v in pairs (Entities:FindAllByName(name)) do
        if IsValidEntity(v) then
            v:Kill()
        end
    end
end

function isZero(val)
    if val == 0 then
        return 0
    else
        return 1
    end
end

function novrStopFiring()
    SendToServerConsole("+attack")
    thisEntity:SetThink(function() SendToServerConsole("-attack") return end, "LEJNO", 0.01)
end


function shootItself(tbl)
    local traceTable =
            {
                startpos = tbl.origin;
                endpos = tbl.shootPos - moveAlongForward(tbl.forwardVec, PENETRATE_OFFSET) ;
                ignore = Entities:FindByClassname(nil, "worldent");
                mask = PENETRATE_MASK;
            }

    TraceLine(traceTable)
    if traceTable.hit and (traceTable.enthit == tbl.shootEnt)then
        return true
    else
        return false
    end
end



--[[

function getObjectScale(ent)
    local bbox = ent:GetBounds()

    return Vector((bbox.Maxs[1]-bbox.Mins[1]),(bbox.Maxs[2]-bbox.Mins[2]),(bbox.Maxs[3]-bbox.Mins[3]))
end

function compareScaleToSpeed(velocity, scale)
    if math.abs(velocity.x)*0.11 > scale.x*0.75 or math.abs(velocity.y)*0.11 > scale.y*0.75 or math.abs(velocity.z)*0.11 > scale.z*0.75 then
        return false
    else
        return true
    end
end


function isWorldEnt(ent)
    if IsValidEntity(ent) then
        if ent:GetClassname() == "worldent" then
            return true
        else
            return false
        end
    else
        return nil
    end
end
]]--