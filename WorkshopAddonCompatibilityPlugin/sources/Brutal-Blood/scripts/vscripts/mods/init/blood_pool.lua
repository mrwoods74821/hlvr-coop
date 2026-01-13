--				 === Script made by CementStairs for Cazanu ===

-- Script handling spawning of the blood pool particle

lastDripParticle = ""

function isVR()
	if GlobalSys:CommandLineCheck('-novr') then VREnabled = false else VREnabled = true end
end

function Brutal_Modify_Dismemberment_Models()
	if thisEntity:GetClassname() == "npc_combine_s" then

		if thisEntity:GetModelName() == "models/characters/combine_grunt/combine_grunt.vmdl" then

			-- Models used for Combine Grunt

			local bodyGibModels = {
				'models/brutal_dism_replacement/grunt/gib/brutal_replacement_gib_body.vmdl',
				'models/brutal_dism_replacement/grunt/gib/brutal_replacement_gib_body2.vmdl'
			}
			local gibModels = {
				'models/brutal_dism_replacement/grunt/gib/brutal_replacement_gib_separate.vmdl',
				'models/brutal_dism_replacement/grunt/gib/brutal_replacement_gib_separate2.vmdl'
			}
			local rightArmModels = {
				'models/brutal_dism_replacement/grunt/right_arm/brutal_replacement_right_arm_separate.vmdl',
				'models/brutal_dism_replacement/grunt/right_arm/brutal_replacement_right_arm_separate2.vmdl'
                        }
			local leftArmModels = {
				'models/brutal_dism_replacement/grunt/left_arm/brutal_replacement_left_arm_separate.vmdl',
				'models/brutal_dism_replacement/grunt/left_arm/brutal_replacement_left_arm_separate2.vmdl'
			}
			local rightLegModels = {
				'models/brutal_dism_replacement/grunt/right_leg/brutal_replacement_right_leg_separate.vmdl',
				'models/brutal_dism_replacement/grunt/right_leg/brutal_replacement_right_leg_separate2.vmdl'
			}
                        local heartModel = 'models/heart.vmdl'
                        local kidneyModel = 'models/kidney.vmdl'

			DoEntFireByInstanceHandle(thisEntity, "RunScriptCode", 
			[[
				bodyModel_gib = ']] .. bodyGibModels[RandomInt(1, #bodyGibModels)] .. [['
				partBodyModel_leftLeg = 'models/brutal_dism_replacement/grunt/left_leg/brutal_replacement_left_leg_separate.vmdl'
				partBodyModel_rightLeg = ']] .. rightLegModels[RandomInt(1, #rightLegModels)] .. [['
				partBodyModel_leftArm = ']] .. leftArmModels[RandomInt(1, #leftArmModels)] .. [['
				partBodyModel_rightArm = ']] .. rightArmModels[RandomInt(1, #rightArmModels)] .. [['
				partBodyModel_gib = ']] .. gibModels[RandomInt(1, #gibModels)] .. [['
                                partBodyModel_heart = ']] .. heartModel .. [['
	                        partBodyModel_kidney = ']] .. kidneyModel .. [['
                                partBodyModel_bit = 'models/bit1.vmdl'
                                partBodyModel_bit2 = 'models/bit3.vmdl'
			]]
			, 1, self, self)

		elseif thisEntity:GetModelName() == "models/characters/combine_soldier_captain/combine_captain.vmdl" then

			-- Models used for Combine Officer

			local rightLegModels = {
				'models/brutal_dism_replacement/grunt/right_leg/brutal_replacement_right_leg_separate.vmdl',
				'models/brutal_dism_replacement/officer/right_leg/brutal_replacement_right_leg_separate_officer.vmdl'
			}
			local leftArmModels = {
				'models/brutal_dism_replacement/grunt/left_arm/brutal_replacement_left_arm_separate.vmdl',
				'models/brutal_dism_replacement/officer/left_arm/brutal_replacement_left_arm_separate_officer.vmdl',
                                'models/brutal_dism_replacement/officer/left_arm/brutal_replacement_left_arm_separate_officer2.vmdl'
			}
	 
			DoEntFireByInstanceHandle(thisEntity, "RunScriptCode", 
			[[
			bodyModel_gib = 'models/brutal_dism_replacement/officer/gib/brutal_replacement_gib_body_captain.vmdl'
                        partBodyModel_gib = 'models/brutal_dism_replacement/officer/gib/brutal_replacement_gib_separate_captain.vmdl'
                        partBodyModel_heart = 'models/heart.vmdl'
                        partBodyModel_kidney = 'models/kidney.vmdl'
                        partBodyModel_rightLeg = ']] .. rightLegModels[RandomInt(1, #rightLegModels)] .. [['
                        partBodyModel_leftArm = ']] .. leftArmModels[RandomInt(1, #leftArmModels)] .. [['
                        partBodyModel_leftLeg = 'models/bit2.vmdl'
                        partBodyModel_rightArm = 'models/bit4.vmdl'
                        partBodyModel_bit = 'models/bit1.vmdl'
                        partBodyModel_bit2 = 'models/bit3.vmdl'
			]]
			, 1, self, self)

		end
	end
end

function Brutal_Modify_Dismemberment_Particles(delay)
	DoEntFireByInstanceHandle(thisEntity, "RunScriptCode",
	[[   
	]]
	, delay, self, self)
end


function Brutal_Activate_attach()
	-- This is activated once, after the script is attached to a combine soldier
	isVR()
	gastank_check = false

	if (Convars:GetStr("default_enabled_addons_list"):find("2891235430") or Convars:GetStr("default_enabled_addons_list"):find("2885804999") or Convars:GetStr("default_enabled_addons_list"):find("2904827638")) then 
		Brutal_IsUsingDismemberment = true
	else
		Brutal_IsUsingDismemberment = false
	end

	if Brutal_IsUsingDismemberment then
		thisEntity:SetThink(function() hookExplosionDetect() return end, UniqueString("hookExplosionDelay"), 0.5)
		thisEntity:SetThink(function() hookExplosionDetect() return end, UniqueString("hookExplosionDelay2"), 1)

		if Convars:GetBool('brutal_compatibility_override_dism_models') then
			Brutal_Modify_Dismemberment_Models()
		end

		if Convars:GetBool('brutal_compatibility_override_dism_particles') then
			Brutal_Modify_Dismemberment_Particles(1)
		end

		if Convars:GetBool('brutal_compatibility_dism_only_explosions') then
			DoEntFireByInstanceHandle(thisEntity, "RunScriptCode", "thisEntity:DisconnectRedirectedOutput('OnDeath', 'DamageTaken', thisEntity)", 1, self, self)
		end
	end
		

	if not Brutal_Once then
		Brutal_Once = true
		print("Script Attached")
		Brutal_Only_once = true
		
		
-- If the Combine soldier dies and enters the "Ragdoll" state, the Death function will trigger
		thisEntity:RedirectOutput("OnDamagedByPlayer", "Brutal_Damaged", thisEntity)
		Listen = ListenToGameEvent("npc_ragdoll_created", Brutal_Death, nil)
		listenWeaponswitch = ListenToGameEvent("weapon_switch", Brutal_playerWeapon, nil)

		if VREnabled then
			DoEntFireByInstanceHandle(thisEntity, "RunScriptCode", "Brutal_currentHeldWeapon = Brutal_findInitialWeapon()", 1, self, self)
		end
	--	DoEntFireByInstanceHandle(thisEntity, "RunScriptCode", "print(currentHeldWeapon:GetClassname())", 1.25, self, self)
	end
end


function Brutal_Death(data)
    if thisEntity:entindex() == data.npc_entindex then
        if not Brutal_IsUsingDismemberment then StopListeningToGameEvent(Listen) end

        StopListeningToGameEvent(listenWeaponswitch)
        if Brutal_Only_once == true then
            local pos_z = thisEntity:GetOrigin().z
            local Unique = UniqueString()
            Brutal_Only_once = false
            local Rag = EntIndexToHScript(data.ragdoll_entindex)

            -- attachment locations for belly chest head 
            local attachmentIndex = lastDripParticle == "particles/drippingparticles.vpcf" and 9 or 11

            -- This part creates the particle itself, it's defined so that it appears after 3 seconds from when the soldier died at the soldier's chest area
            local particleName = 'particles/blood_pool_particle.vpcf'
            local delay = 4 -- Delay for the particle to appear
            local particleCode = string.format([[
                %s = ParticleManager:CreateParticle('%s', 0, Entities:GetLocalPlayer())
                ParticleManager:SetParticleControl(%s, 0, Vector(thisEntity:GetAttachmentOrigin(%d).x, thisEntity:GetAttachmentOrigin(%d).y, %s))
                ParticleManager:ReleaseParticleIndex(%s)]],
                Unique, particleName, Unique, attachmentIndex, attachmentIndex, tostring(pos_z - 55), Unique)

            DoEntFireByInstanceHandle(Rag, "RunScriptCode", particleCode, delay, nil, nil)
        end
    end
end


function Brutal_Damaged()
    -- Calculating the hit position and direction
    local startVector
    local startVectorForward

    local player = nil  -- Initialize player variable
    for _, entity in pairs(Entities:FindAllByClassname("player")) do
        player = entity  -- Assuming there's only one player entity in the game.
        break  -- Break after finding the first player entity.
    end

    if player == nil then
        print("Player entity not found.")
        return  -- Exit the function if no player entity is found.
    end

    if VREnabled then
        startVector = Brutal_currentHeldWeapon:GetAttachmentOrigin(Brutal_currentHeldWeapon:ScriptLookupAttachment("muzzle"))
        startVectorForward = Brutal_currentHeldWeapon:GetAttachmentForward(Brutal_currentHeldWeapon:ScriptLookupAttachment("muzzle")) * 10000
    else
        startVector = player:EyePosition()  -- Use EyePosition method of the player entity
        startVectorForward = player:GetForwardVector() * 10000  -- Use GetForwardVector method of the player entity
        Brutal_currentHeldWeapon = player
    end

    local traceTable =
        {
            startpos = startVector;
            endpos = startVector + startVectorForward;
            ignore = Brutal_currentHeldWeapon;
        }

    TraceLine(traceTable)

    if traceTable.hit and traceTable.enthit == thisEntity then
        local hitPosition = traceTable.pos
        local hitDirection = (player:EyePosition() - hitPosition):Normalized()

        -- Determine the hit bone
        local hitBone = thisEntity:GetGraphParameter("i_Hit_Bone")

        print("Hit detected on bone: " .. tostring(hitBone))

        -- List of bones to exclude from spawning specific particles
        local excludedBones = {30, 31, 32, 23, 27, 28, 29, 24, 25, 26}

        -- Check if the hit bone is in the excluded list
        local isExcludedBone = table.contains(excludedBones, hitBone)

        -- Define upper body bones
        local upperBodyBones = {15, 9, 7, 13, 14}

        -- Check if the hit bone is in the upper body list
        local isUpperHit = table.contains(upperBodyBones, hitBone)

        if not isExcludedBone then
        if isUpperHit then
            -- Apply upper body particle effect (following player's view)
            Brutal_particleFollow("particles/drippingparticles.vpcf", hitDirection, hitPosition)
            lastDripParticle = "particles/drippingparticles.vpcf"
        else
            -- Apply lower body particle effect (similar to blood effects orientation)
            local hitNormal = traceTable.normal  -- Assuming traceTable.normal gives the hit surface normal
            Brutal_particleFollow("particles/drippingparticles2.vpcf", hitNormal, hitPosition)
            lastDripParticle = "particles/drippingparticles2.vpcf"
        end
    end


        -- Apply blood effects for every impact
        local bloodEffectParticle = "particles/bloodeffects.vpcf"
        Brutal_particleFollow(bloodEffectParticle, -hitDirection, hitPosition)
    end
end

-- Helper function to check if a table contains a value
function table.contains(table, element)
    for _, value in pairs(table) do
        if value == element then
            return true
        end
    end
    return false
end

function Brutal_particleFollow(particleName, hitDirection, tablePos)
    local bloodHit = ParticleManager:CreateParticle(particleName, 0, Entities:GetLocalPlayer())
    ParticleManager:SetParticleControlForward(bloodHit, 0, hitDirection)
    ParticleManager:SetParticleControlForward(bloodHit, 1, hitDirection)
    ParticleManager:SetParticleControl(bloodHit, 0, tablePos)
    ParticleManager:SetParticleControl(bloodHit, 1, tablePos + hitDirection * 50)
    ParticleManager:ReleaseParticleIndex(bloodHit)
end

function Brutal_playerWeapon(data)
	Brutal_currentWeapon = data.item
	Brutal_currentHeldWeapon = Entities:FindByClassname(nil, Brutal_currentWeapon)
	--print(currentHeldWeapon)
end

function Brutal_findInitialWeapon()
	local initialHeldWeapon
	local holdingHand

		if Convars:GetInt('hlvr_left_hand_primary') == 1 then
			holdingHand = Entities:GetLocalPlayer():GetHMDAvatar():GetVRHand(0)
		else
			holdingHand = Entities:GetLocalPlayer():GetHMDAvatar():GetVRHand(1)
		end

	local initialTable = {
		Entities:FindByClassnameNearest("hlvr_weapon_energygun", holdingHand:GetOrigin(), 50),
		Entities:FindByClassnameNearest("hlvr_weapon_rapidfire", holdingHand:GetOrigin(), 50),
		Entities:FindByClassnameNearest("hlvr_weapon_shotgun", holdingHand:GetOrigin(), 50),
		Entities:FindByClassnameNearest("hlvr_weapon_generic_pistol", holdingHand:GetOrigin(), 50)
	}

	for a, b in pairs(initialTable) do
		if b ~= nil then initialHeldWeapon = b end
	end

	if initialHeldWeapon ~= nil then
		for k,v in pairs(initialTable) do
			if v ~= nil and VectorDistance(v:GetAttachmentOrigin(v:ScriptLookupAttachment("muzzle")), holdingHand:GetOrigin()) < VectorDistance(initialHeldWeapon:GetAttachmentOrigin(initialHeldWeapon:ScriptLookupAttachment("muzzle")), holdingHand:GetOrigin()) then
				initialHeldWeapon = v
			end
		end
	end
	return initialHeldWeapon
end

function hookExplosionDetect()
	for k,v in pairs(thisEntity:GetChildren()) do 
		if IsValidEntity(v) then
			if v:GetClassname() == "prop_physics" and v:GetName() == "dis_exploDetect" then
				--v:RedirectOutput("OnTakeDamage", "Brutal_Explosion_Gib_Spawn", thisEntity)
				DoEntFireByInstanceHandle(v, "RunScriptFile", "mods/init/brutal_explo_detect_custom_gib.lua", 0, v, v)
				return
			end
		end
	end
end

function Brutal_Explosion_Gib_Spawn()
	if not brutal_explosion_gib_check then 
		brutal_explosion_gib_check = true

		-- Set which bodyparts should be spawned

		Brutal_createExplosionGib_custom(partBodyModel_leftLeg, "prop_physics_override")
		Brutal_createExplosionGib_custom(partBodyModel_rightLeg, "prop_physics_override")
		Brutal_createExplosionGib_custom(partBodyModel_leftArm, "prop_physics_override")
		Brutal_createExplosionGib_custom(partBodyModel_rightArm, "prop_physics_override")
                Brutal_createExplosionGib_custom(partBodyModel_heart, "prop_physics_override")
		Brutal_createExplosionGib_custom(partBodyModel_kidney, "prop_physics_override")
                Brutal_createExplosionGib_custom(partBodyModel_bit, "prop_physics_override")
		Brutal_createExplosionGib_custom(partBodyModel_bit2, "prop_physics_override")
	end
end

function Brutal_createExplosionGib_custom(gibModelName, gibClassname)
	-- Function for spawning gibs after explosion

	local gib_random =	SpawnEntityFromTableSynchronous(gibClassname, {
											origin = thisEntity:GetOrigin()+Vector(0,0,50)+Vector(RandomInt(1,3),RandomInt(1,3),RandomInt(1,3)),
											angles = QAngle(RandomInt(0,360),RandomInt(0,360),RandomInt(0,360)),
											model = gibModelName,
											spawnflags = "6",
											solid = "0",
											HoldAnimation = "1",
											forcenpcexclude = "1",
		})

	-- Schedule the blood pool particle effect after a delay
    gib_random:SetContextThink("SpawnBloodPool", function()
        local particleName = 'particles/blood_pool_particle2.vpcf'
        local bloodPool = ParticleManager:CreateParticle(particleName, PATTACH_CUSTOMORIGIN, gib_random)
        ParticleManager:SetParticleControlEnt(bloodPool, 0, gib_random, PATTACH_POINT_FOLLOW, "Gib_Custom", gib_random:GetAttachmentOrigin(66), true)
        ParticleManager:ReleaseParticleIndex(bloodPool)
    end, 4)  -- 4-second delay

    -- Apply forces to the gib
    gib_random:ApplyAbsVelocityImpulse(Vector(0,0,25) + Vector(RandomInt(75,150)*randomPositive(), RandomInt(75,150)*randomPositive(), RandomInt(150,200)))
    gib_random:ApplyLocalAngularVelocityImpulse(Vector(RandomInt(75,140),RandomInt(75,140),RandomInt(75,140)))

    return gib_random
end

function randomPositive()
	return RandomInt(0,1) < 0.5 and -1 or 1
end