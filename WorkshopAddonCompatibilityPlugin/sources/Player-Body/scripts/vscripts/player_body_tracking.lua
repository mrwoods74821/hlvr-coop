function Activate()
    -- Determine mainhand and offhand index. These can be used by entities IO, hence global
    if (Convars:GetBool('hlvr_left_hand_primary') == true) then
        _G.offHandInt = 0
    else
        _G.offHandInt = 1
    end
	
	-- Get related entities
	Definitions()
	
	-- Set think function
	thisEntity:SetContextThink(nil, MainThinkFunc, 0)
	
	-- Animgraph Listener
	thisEntity:RegisterAnimTagListener(AnimTagListener)
	
	-- Game event listeners
    HandSettingListener = ListenToGameEvent('primary_hand_changed', AdjustHandIndex, nil)
    ClimbingListener = ListenToGameEvent('player_grabbed_ladder', PlayerClimb, nil)
	TeleportListener = ListenToGameEvent('player_teleport_start', PlayerTeleportStart, nil)
	ChangeLevelListener = ListenToGameEvent('change_level_activated', PlayerBodyKill, nil)
	WeaponSwitchListener = ListenToGameEvent('weapon_switch', EnableEquipmentShadow, nil)
	TwoHandPistolGrabStartListener = ListenToGameEvent('two_hand_pistol_grab_start', DisableShadowGrabbity, nil)
	TwoHandPistolGrabEndListener = ListenToGameEvent('two_hand_pistol_grab_end', EnableShadowGrabbity, nil)
	ItemPickupListener = ListenToGameEvent('item_pickup', EnableItemPickupShadow, nil)
	GlovePullListener = ListenToGameEvent('grabbity_glove_pull', EnableItemPullShadow, nil)
	HealthPenUseListener = ListenToGameEvent('player_health_pen_used', HealthpenUsed, nil)
	PlayerHurtListener = ListenToGameEvent('player_hurt', PlayerHurt, nil)
	
	if (Entities:GetLocalPlayer():GetHMDAvatar() ~= nil) then
		thisEntity:GetMoveParent():SetAbsOrigin(Vector(Entities:GetLocalPlayer():GetAbsOrigin()[1], Entities:GetLocalPlayer():GetAbsOrigin()[2], Entities:GetLocalPlayer():GetHMDAvatar():GetAbsOrigin()[3]-60))
		thisEntity:GetMoveParent():SetAbsAngles(0, Entities:GetLocalPlayer():GetHMDAvatar():GetAnglesAsVector()[2], 0)
	end
	
	-- Determine if logic_playerproxy is already in the map, then copy all I/O to it, and delete the prefab one
	if (Entities:FindAllByClassname('logic_playerproxy')[2] ~= nil) then
		for k, v in pairs(Entities:FindAllByClassname('logic_playerproxy')) do
			if (v:GetName() == 'player_body_proxy') then
				v:Kill()
			else
				v:RedirectOutput('OnPlayerTeleported', 'PlayerTeleportFinished', thisEntity)
				v:RedirectOutput('PlayerDied', 'PlayerBodyKill', thisEntity)
				v:RedirectOutput('PlayerDied', 'PlayerBodyRagdoll', thisEntity)
				v:RedirectOutput('OnPlayerHitBySpit', 'PlayerHitBySpit', thisEntity)
			end
		end
	end
	
end

function Spawn()
	-- Set initial body preference convar as auto, unless it's already set by globals (level transition/load retention workaround)
	if (Convars:GetInt('player_body_preference') == nil) then
		Convars:RegisterConvar('player_body_preference', '0', '', 0)
	end
end

function UpdateOnRemove()
	-- Stop think function
	thisEntity:StopThink('MainThinkFunc')
	
	-- Stop animgraph Listener
	thisEntity:UnregisterAnimTagListener()
	
	-- Stop game event listeners
    StopListeningToGameEvent(HandSettingListener)
    StopListeningToGameEvent(ClimbingListener)
    StopListeningToGameEvent(TeleportListener)
	StopListeningToGameEvent(ChangeLevelListener)
	StopListeningToGameEvent(WeaponSwitchListener)
	StopListeningToGameEvent(TwoHandPistolGrabStartListener)
	StopListeningToGameEvent(TwoHandPistolGrabEndListener)
	StopListeningToGameEvent(ItemPickupListener)
	StopListeningToGameEvent(GlovePullListener)
	StopListeningToGameEvent(HealthPenUseListener)
	StopListeningToGameEvent(PlayerHurtListener)
end

function Definitions()
	localPlayer = Entities:GetLocalPlayer()
	localPlayerHealthCurrent = localPlayer:GetHealth()
	hmdAvatar = Entities:GetLocalPlayer():GetHMDAvatar()
	hmdAnchor = Entities:GetLocalPlayer():GetHMDAnchor()
	bodyAnchor = Entities:FindByName(nil, 'player_body_anchor')
	leftHandGlove = Entities:FindByName(nil, 'left_hand_glove')
	leftHandParticleStart = Entities:FindByName(nil, 'player_body_lefthand_start')
	rightHandGlove = Entities:FindByName(nil, 'right_hand_glove')
	rightHandParticleStart = Entities:FindByName(nil, 'player_body_righthand_start')
	
	disableFootAdjustment = false 	--toggle for Foot Adjustment
	
	playerPrevZ = 0
	playerZ = 0
	zDiff = 0
	fallingVar = true
	switch_stopper = 0
	if (_G.player_body_menu == nil) then _G.player_body_menu = false end
end

function AnimTagListener(tagName, tagStatus)
	if (tagName == 'Turning') then
		if (tagStatus == 1) then
			AnimTurning = true
			disableFootAdjustment = true
		else
			AnimTurning = false
			disableFootAdjustment = false
		end
	elseif (tagName == 'MoveFinished') then	
			thisEntity:SetGraphLookTarget(thisEntity:GetAttachmentOrigin(6)) --This prevents turn animation from unnecessarily playing every time you stop walking
	end
end

function AdjustHandIndex(p)
	--Determine mainhand and offhand index when the game setting is changed
    if (p.is_primary_left == 1) then
		_G.offHandInt = 0
	else
		_G.offHandInt = 1
	end
end

function DisableShadowGrabbity()
	-- Fix for grabbity shadow interfering with flashlight attachment
	if Entities:FindByClassname(nil, 'hlvr_flashlight_attachment') ~= nil then
		DoEntFire('prop_grabbity_gloves', 'disableshadow', '', 0.10, self, self)
	end
end

function EnableShadowGrabbity()
	-- Fix for grabbity shadow interfering with flashlight attachment
	if Entities:FindByClassname(nil, 'hlvr_flashlight_attachment') ~= nil then
		DoEntFire('prop_grabbity_gloves', 'enableshadow', '', 0.10, self, self)
	end
end

function EnableItemPickupShadow(p)
	--Brute force to enable shadows for picked up items
	if (p.item ~= nil) then
		if (p.item_name ~= '') and (Entities:FindByName(nil, p.item_name):GetMass() < 20) then
			DoEntFire(tostring(p.item_name), 'disableshadow', '', 0, self, self)
			DoEntFire(tostring(p.item_name), 'enableshadow', '', 0.001, self, self)
		else
		
		leftHandObject = Entities:FindByClassnameNearest(tostring(p.item), leftHandGlove:GetOrigin(), 32)
		rightHandObject = Entities:FindByClassnameNearest(tostring(p.item), rightHandGlove:GetOrigin(), 32)
			
			if IsValidEntity(leftHandObject) then
				if (leftHandObject:GetMass() < 20) then
					DoEntFireByInstanceHandle(leftHandObject, 'disableshadow', '', 0, self, self)
					DoEntFireByInstanceHandle(leftHandObject, 'enableshadow', '', 0.001, self, self)
					--print(leftHandObject:GetMass())
				end
			end
			
			if IsValidEntity(rightHandObject) then
				if (rightHandObject:GetMass() < 20) then
					DoEntFireByInstanceHandle(rightHandObject, 'disableshadow', '', 0, self, self)
					DoEntFireByInstanceHandle(rightHandObject, 'enableshadow', '', 0.001, self, self)
					--print(rightHandObject:GetMass())
				end
			end
		end
	end
	--print(p.item_name .. ' ' .. p.item)
end

function EnableItemPullShadow(p)
	DoEntFireByInstanceHandle(EntIndexToHScript(p.entindex), 'disableshadow', '', 0, self, self)
	DoEntFireByInstanceHandle(EntIndexToHScript(p.entindex), 'enableshadow', '', 0.001, self, self)
end

function EnableEquipmentShadow(p)
	--Enable shadow when switching weapons in case player gets them later in the level
	DoEntFire(tostring(p.item), 'enableshadow', '', 0, self, self)
	DoEntFire('prop_grabbity_gloves', 'enableshadow', '', 0, self, self)
	DoEntFire('hlvr_flashlight_attachment', 'enableshadow', '', 0, self, self)
	
	-- Fixup holder positions in case player gets them later on in the level
	if (Entities:FindByName(nil, 'item_holder_l') ~= nil) and (Entities:FindByName(nil, 'item_holder_r') ~= nil) then
		Entities:FindByName(nil, 'item_holder_l'):SetLocalOrigin(Vector(3,3.5,0))
		Entities:FindByName(nil, 'item_holder_r'):SetLocalOrigin(Vector(3,3.5,0))
	end
	
	-- Fixup grabbity glove clipping for Gordon gloves in case player gets them later on in the level
	if (tostring(leftHandGlove:GetMaterialGroupMask()) == '2') or (tostring(rightHandGlove:GetMaterialGroupMask()) == '2') then
		DoEntFire('prop_grabbity_gloves', 'setscale', '1.2', 0, self, self)
	end
	
	if p.item == 'hlvr_weapon_energygun' then
		DoEntFire('hlvr_slide_interactable', 'enableshadow', '', 0, self, self)
		DoEntFire('hlvr_upgrade_hopper', 'enableshadow', '', 0, self, self)
		DoEntFire('reflex_sights', 'enableshadow', '', 0, self, self)
	end
	
	if p.item == 'hlvr_weapon_shotgun' then
		DoEntFire('hlvr_shotgun_slide_interactable', 'enableshadow', '', 0, self, self)
		DoEntFire('baseanimating', 'enableshadow', '', 0, self, self)
	end
	
	if p.item == 'hlvr_weapon_rapidfire' then
		DoEntFire('hlvr_weapon_rapidfire_upgrade_model', 'enableshadow', '', 0, self, self)
		DoEntFire('hlvr_weapon_rapidfire_extended_magazine', 'enableshadow', '', 0, self, self)
	end
end

function PlayerClimb()
	DoEntFire('player_body_items_col*', 'DisableCollisions', '', 0, self, self)
	thisEntity:SetGraphParameterBool('Falling', true)
	fallingVar = false
end

function PlayerTeleportStart()
	DoEntFire('player_body_items_col*', 'EnableCollisions', '', 0, self, self) --enables collisions back after PlayerClimb disabled them
	thisEntity:SetGraphParameterBool('Falling', true)
	AnimTurning = true
	fallingVar = false
end

function PlayerBodyKill()
	DoEntFire('player_body_anchor', 'KillHierarchy', '', 0, self, self)
	DoEntFire('player_body_lefthand_start', 'Kill', '', 0, self, self)
	DoEntFire('player_body_righthand_start', 'Kill', '', 0, self, self)
	DoEntFire('player_body_save', 'Kill', '', 0, self, self)
	DoEntFire('player_body-spawner', 'Kill', '', 0, self, self)
	DoEntFire('player_body_ragdoll-spawner', 'Kill', '', 0, self, self)
	DoEntFire('player_body_ragdoll-spawner-maker', 'Kill', '', 0, self, self)
	DoEntFire('player_body_items_0_frag_auto', 'Kill', '', 0, self, self)
	DoEntFire('player_body_items_1_frag_auto', 'Kill', '', 0, self, self)
	DoEntFire('player_body_items_0_healthvial_auto', 'Kill', '', 0, self, self)
	DoEntFire('player_body_items_1_healthvial_auto', 'Kill', '', 0, self, self)
	
	-- Hook for body holsters addon
	if type(PlayerBodyKillHook) == "function" then
		PlayerBodyKillHook()
	end
end

function PlayerBodyRagdoll()
	DoEntFire('player_body_ragdoll-spawner-maker', 'ForceSpawnAtEntityOrigin', 'point_hmd_anchor', 0, self, self)
end

-- For Gordon H.E.V callouts
function PlayerHurt(p)
	if (Convars:GetInt('player_body_preference') == 1) then
	
		damageTaken = localPlayerHealthCurrent - p.health
		print( 'dmg: ' .. damageTaken .. ' health: ' .. p.health )
		if p.health <= 0 then
			StartSoundEventFromPositionReliable('hev_suit.flatline',thisEntity:GetOrigin())
			
		elseif p.health <= localPlayer:GetMaxHealth()/10 then
			DoEntFire('player_body_hev_callouts_neardeath_2','Trigger','',0,self,self)
			
		elseif p.health <= localPlayer:GetMaxHealth()/4 then
			DoEntFire('player_body_hev_callouts_neardeath_1','Trigger','',0,self,self)
			
			--Estimate: Bit by Poisoned Headcrab
		elseif damageTaken == Convars:GetInt('sk_headcrab_poison_accumulate_dmg_amt') or damageTaken == math.floor((Convars:GetInt('sk_headcrab_poison_accumulate_dmg_amt')/2)+0.5) then
			DoEntFire('player_body_hev_callouts_poison','Trigger','',0,self,self)
	
		elseif localPlayerHealthCurrent >= 50 and p.health < localPlayer:GetMaxHealth()/2 then
			DoEntFire('player_body_hev_callouts_armorgone','Trigger','',0,self,self)
			
		elseif damageTaken > 25 then
			DoEntFire('player_body_hev_callouts_major','Trigger','',0,self,self)
			
			--Estimate: Shot by Combine
		elseif damageTaken == Convars:GetInt('sk_npc_dmg_ar2') or damageTaken == Convars:GetInt('sk_npc_dmg_smg1') or damageTaken == Convars:GetInt('sk_npc_dmg_heavymg') or damageTaken == Convars:GetInt('sk_npc_dmg_buckshot') then
			DoEntFire('player_body_hev_callouts_bullets','Trigger','',0,self,self)
			
			--Estimate: Attacked by Electric Headcrab
		elseif damageTaken == Convars:GetInt('sk_reviver_storm_dmg') or damageTaken == Convars:GetInt('sk_headcrab_runner_spit_damage') or damageTaken == Convars:GetInt('sk_zombie_reviver_zap_damage') then
			DoEntFire('player_body_hev_callouts_electrical','Trigger','',0,self,self)
			
		elseif damageTaken > 10 then
			DoEntFire('player_body_hev_callouts_minor','Trigger','',0,self,self)
		end
		
		localPlayerHealthCurrent = p.health
	end
end

-- For Gordon H.E.V callouts
function HealthpenUsed()
	if (Convars:GetInt('player_body_preference') == 1) then
		DoEntFire('player_body_hev_callouts_healthpen','Trigger','',0,self,self)
	end
end

-- For Gordon H.E.V callouts
function PlayerHitBySpit()
	if (Convars:GetInt('player_body_preference') == 1) then
		DoEntFire('player_body_hev_callouts_chemical','Trigger','',0,self,self)
	end
end

-- This should be called by an output in playerproxy using OnPlayerTeleported. The game event available in vscript ('player_teleport_finish') fires every time player walks so its not usable, hence this workaround.
function PlayerTeleportFinished()
	thisEntity:SetGraphLookTarget(thisEntity:GetAttachmentOrigin(6)) --attachment 6 is LookAt_Forward. This prevents turn animation from unnecessarily playing as you come out of teleport
	if (hmdAnchor:GetMoveParent() ~= nil) then --When anchor is parented - e.g. elevators, moving platforms - use an action sequence to disable foot lock
		thisEntity:SetGraphParameterBool('Parented', true)
	else --When player stops walking, disable action sequence to allow foot lock/Foot Adjustments
		thisEntity:SetGraphParameterBool('Parented', false)
	end
	DoEntFireByInstanceHandle(thisEntity, 'SetAnimgraphParameter', 'Falling=false', 0.1, self, self)
	DoEntFireByInstanceHandle(thisEntity, 'Runscriptcode', 'AnimTurning = false', 0.1, self, self)
	DoEntFireByInstanceHandle(thisEntity, 'Runscriptcode', 'fallingVar = true', 0.1, self, self)
end

-- Think function ran every frame
function MainThinkFunc()

	--Abort in case hmd_avatar does not exist
	if (hmdAvatar == nil) then
		print('PlayerBody: hmd_avatar not found! Aborting...')
		PlayerBodyKill()
		return
	end

	--Control position. This is to keep the model in the desired place relative to the moving player. It's setting an absolute position every frame so not ideal solution.
	bodyAnchor:SetAbsOrigin(Vector(localPlayer:GetAbsOrigin()[1], localPlayer:GetAbsOrigin()[2], hmdAvatar:GetAbsOrigin()[3]-60))
	--Control rotation. This is required to keep pitch and roll of the body at 0. It's setting absolute rotations every frame so not ideal solution.
	bodyAnchor:SetAbsAngles(0, hmdAvatar:GetAnglesAsVector()[2], 0)
	
	if (AnimTurning == true) then
		thisEntity:SetGraphLookTarget(thisEntity:GetAttachmentOrigin(6)) --attachment 6 is LookAt_Forward
	else
		--Foot Adjustment when player walks around in vr playspace
		local hmdAvatarVecXY = Vector(hmdAvatar:GetAbsOrigin()[1], hmdAvatar:GetAbsOrigin()[2], 0)
		local hmdLFootVecXY = Vector(thisEntity:GetAttachmentOrigin(4)[1], thisEntity:GetAttachmentOrigin(4)[2], 0) -- attachment 4 is left_foot
		local hmdRFootVecXY = Vector(thisEntity:GetAttachmentOrigin(5)[1], thisEntity:GetAttachmentOrigin(5)[2], 0) -- attachment 5 is right_foot
		
		if (disableFootAdjustment == false) then
			if (VectorDistanceSq(hmdAvatarVecXY, hmdLFootVecXY) > 256) or (VectorDistanceSq(hmdAvatarVecXY, hmdRFootVecXY) > 441) then --bias for retarded right foot, as its distance stays unusually larger. Consider testing just one foot
				thisEntity:SetGraphLookTarget(thisEntity:GetAttachmentOrigin(7))
				disableFootAdjustment = true
			end
		end
	end
	
	--Get vertical distance between HMD and anchor to determine if player is crouching
	if (thisEntity:GetGraphParameter('fl_move_speed') > 0.2) then
		thisEntity:SetGraphParameterFloat('fl_move_height', hmdAvatar:GetAbsOrigin()[3] - hmdAnchor:GetAbsOrigin()[3])
	end
	--print(hmdAvatar:GetAbsOrigin()[3] - hmdAnchor:GetAbsOrigin()[3])
		
	--Determine if player is parented to a moving platform, 'Parented' graph paremeter disables footlock
	if (thisEntity:GetGraphParameter('Parented') == false) then
		if (hmdAnchor:GetMoveParent() ~= nil) then
			thisEntity:SetGraphParameterBool('Parented', true)
		end
	else	
		if (hmdAnchor:GetMoveParent() == nil) then
			thisEntity:SetGraphParameterBool('Parented', false)
		end
	end
		--Read move speed from analog input and pass to animgraph
		speed = VectorDistance(Vector(0,0,0), localPlayer:GetAnalogActionPositionForHand(offHandInt,3))
		thisEntity:SetGraphParameterFloat('fl_move_speed', speed)
		--print('speed: ' .. speed)
		
		--Read direction from analog input and pass to animgraph 
		local vectorX = localPlayer:GetAnalogActionPositionForHand(offHandInt,3)[1]
		local vectorY = localPlayer:GetAnalogActionPositionForHand(offHandInt,3)[2]
		thisEntity:SetGraphParameterFloat('fl_blend_x', vectorX)
		thisEntity:SetGraphParameterFloat('fl_blend_y', vectorY)
		--print('X axis: ' .. vectorX .. ', Y axis: ' .. vectorY)
	
	--Determine if player is falling and play the falling animation
	if (localPlayer:IsDigitalActionOnForHand(offHandInt, 22) == false) and (fallingVar == true) then
		
		playerZ = bodyAnchor:GetAbsOrigin()[3]
		zDiff = playerZ - playerPrevZ
		--print(playerZ .. ' - ' .. playerPrevZ .. ' = ' .. zDiff)
		
		if (zDiff > 1.0) or (zDiff < -1.0) then
			if (thisEntity:GetGraphParameter('Falling') == false) and (hmdAnchor:GetMoveParent() == nil) then
				thisEntity:SetGraphParameterBool('Falling', true)
				--print('FALLING TRUE')
			end
		else
			if (thisEntity:GetGraphParameter('Falling') == true) then
				thisEntity:SetGraphParameterBool('Falling', false)
				--print('FALLING FALSE')
			end
		end
		
		playerPrevZ = playerZ
		
	end
	
	--Get HMD pitch to translate to model head animation blend
	thisEntity:SetGraphParameterFloat('fl_look_pitch', hmdAvatar:GetLocalAngles()[1])
	
	--Change between Alyx and Gordon prefabs based on gloves and preference and set in-game globals to reflect the preference convar for level transition/load retention
	if (Convars:GetInt('player_body_preference') == 0) then
		if (tostring(leftHandGlove:GetMaterialGroupMask()) == '1') or (tostring(rightHandGlove:GetMaterialGroupMask()) == '1') then
			if (leftHandParticleStart:GetLocalOrigin() == Vector(0.3,0.3,0)) or (rightHandParticleStart:GetLocalOrigin() == Vector(0.3,0.3,0)) then
				PlayerBodyKill()
				DoEntFire('player_body_init_hands', 'Enable', '', 0, self, self)
				SendToConsole('spawn_group_load prefabs/player_body_alyx')
				Convars:SetInt('animgraph_footlock_enabled',0)
			end
		elseif (tostring(leftHandGlove:GetMaterialGroupMask()) == '2') or (tostring(rightHandGlove:GetMaterialGroupMask()) == '2') then
			if (leftHandParticleStart:GetLocalOrigin() == Vector(1.75,0.05,0)) or (rightHandParticleStart:GetLocalOrigin() == Vector(1.75,0.05,0)) then
				PlayerBodyKill()
				DoEntFire('player_body_init_hands', 'Enable', '', 0, self, self)
				SendToConsole('spawn_group_load prefabs/player_body_gordon')
				Convars:SetInt('animgraph_footlock_enabled',0)
			end
		end
		
	SendToConsole('global_set player_body_preference_auto 1')
	SendToConsole('global_set player_body_preference_gordon 0')
	SendToConsole('global_set player_body_preference_alyx 0')
		
	elseif (Convars:GetInt('player_body_preference') == 1) then
		if (leftHandParticleStart:GetLocalOrigin() == Vector(1.75,0.05,0)) or (rightHandParticleStart:GetLocalOrigin() == Vector(1.75,0.05,0)) then
			PlayerBodyKill()
			DoEntFire('player_body_init_hands', 'Enable', '', 0, self, self)
			SendToConsole('spawn_group_load prefabs/player_body_gordon')
			Convars:SetInt('animgraph_footlock_enabled',0)
		end
		
		if (tostring(leftHandGlove:GetMaterialGroupMask()) == '1') or (tostring(rightHandGlove:GetMaterialGroupMask()) == '1') then
			DoEntFire('logic_playerproxy', 'SetGlovesGordon', '', 0, self, self)
		end
		
	SendToConsole('global_set player_body_preference_auto 0')
	SendToConsole('global_set player_body_preference_gordon 1')
	SendToConsole('global_set player_body_preference_alyx 0')

	elseif (Convars:GetInt('player_body_preference') == 2) then
		if (leftHandParticleStart:GetLocalOrigin() == Vector(0.3,0.3,0)) or (rightHandParticleStart:GetLocalOrigin() == Vector(0.3,0.3,0)) then
			PlayerBodyKill()
			DoEntFire('player_body_init_hands', 'Enable', '', 0, self, self)
			SendToConsole('spawn_group_load prefabs/player_body_alyx')
			Convars:SetInt('animgraph_footlock_enabled',0)
		end
		
		if (tostring(leftHandGlove:GetMaterialGroupMask()) == '2') or (tostring(rightHandGlove:GetMaterialGroupMask()) == '2') then
			DoEntFire('logic_playerproxy', 'SetGlovesAlyx', '', 0, self, self)
		end
		
	SendToConsole('global_set player_body_preference_auto 0')
	SendToConsole('global_set player_body_preference_gordon 0')
	SendToConsole('global_set player_body_preference_alyx 1')
	
	end
	
	-- Display panorama panel for switching player bodies
	if (localPlayer:IsDigitalActionOnForHand(1-offHandInt, 11) == true) then 
		if (_G.player_body_menu == true) then 
			if (player_body_toggle_counter == 0) then
				DoEntFire('player_body_switch_panel','Kill', '', 0, self, self)
				_G.player_body_menu = false
				player_body_toggle_counter = 61
			end
		else 
			player_body_toggle_counter = player_body_toggle_counter + 1
			
			if (player_body_toggle_counter == 60) then
				DoEntFire('player_body_switch_panel-spawner','ForceSpawn', '', 0, self, self)
				_G.player_body_menu = true
			end
		end
	else
		player_body_toggle_counter = 0
	end
	
	--Fixup for shadow variables getting reset when player goes into settings
	--r_high_quality_shadows being set to 0 allows dynamic shadow casting on gloves
	if (Convars:GetInt('r_high_quality_shadows') ~= 0) or (Convars:GetInt('r_mixed_shadows_max_important_shadow_casters') ~= 24) then
		Convars:SetInt('r_high_quality_shadows', 0)
		Convars:SetInt('r_mixed_shadows_max_important_shadow_casters', 24)
	end
	
	return 0
end