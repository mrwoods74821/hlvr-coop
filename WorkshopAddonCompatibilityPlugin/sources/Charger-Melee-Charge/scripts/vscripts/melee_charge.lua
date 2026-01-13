function Activate()
	-- Make sure function is executed once
	if lock == nil then

		thisEntity:RegisterAnimTagListener( AnimTagListener )
		
		PistolEmptyListener = ListenToGameEvent('player_pistol_empty_clip', PlayerScrambling, nil)
		PistolEjectClipListener = ListenToGameEvent('player_eject_clip', PlayerScrambling, nil)
		SMGEmptyListener = ListenToGameEvent('player_rapidfire_magazine_empty', PlayerScrambling, nil)
		SMGChamberEmptyListener = ListenToGameEvent('player_rapidfire_capsule_chamber_empty', PlayerScrambling, nil)
		ShotgunOpenListener = ListenToGameEvent('player_shotgun_open', PlayerScrambling, nil)
		HealthStationListener = ListenToGameEvent('player_using_healthstation', PlayerScrambling, nil)
		HealthPenPrepareListener = ListenToGameEvent('player_health_pen_prepare', PlayerScrambling, nil)
		
		-- Register main think function for evaluating melee mode activation and charge path creation
		thisEntity:SetThink(MainThinkFunc, 'MainThinkFunc', 0)
		
		-- Settings tables for spawning charge props dynamically
		BatonProp =
		{
			model = 'models/props_combine/stun_stick/combine_stun_stick.vmdl',
			renderamt = '255',
		}
		
		BatonParticle =
		{
			effect_name = 'particles/generic_fx/fx_electricspark_follow.vpcf',
			start_active = '0',
		}
		
		GetPlayerPos = 0
		ChargeInProgress = 0
		ChargeChanceTimeout = 0
		
		lock = 1
	end
end

function UpdateOnRemove()
	-- Stop think function
	thisEntity:StopThink('MainThinkFunc')
	
	-- Stop animgraph Listener
	thisEntity:UnregisterAnimTagListener()
	
	-- Stop gameevent Listeners
	StopListeningToGameEvent(PistolEmptyListener)
	StopListeningToGameEvent(PistolEjectClipListener)
	StopListeningToGameEvent(SMGEmptyListener)
	StopListeningToGameEvent(SMGChamberEmptyListener)
	StopListeningToGameEvent(ShotgunOpenListener)
	StopListeningToGameEvent(HealthStationListener)
	StopListeningToGameEvent(HealthPenPrepareListener)
end

function AnimTagListener( sTagName, nStatus )
	
	if (sTagName == 'Switch_To_Baton') then
		SwitchToBaton()
	end
	
	if (sTagName == 'Switch_to_Gun') then
		SwitchToGun()
	end
	
	if (sTagName == 'Charge_Fakeout') then
		ChargeFakeout()
	end
	
	if (sTagName == 'Charge_Strike') then
		ChargeStrike()
	end
	
	if (sTagName == 'Charge_Preparing') then
		ChargePreparing()
	end
	
	if (sTagName == 'Charge_Announce') then
		ChargeAnnounce()
	end
	
	if (sTagName == 'Charge_BatonGlowup') then
		ChargeBatonGlowup()
	end
	
end

function SwitchToBaton()
	baton = SpawnEntityFromTableSynchronous('prop_dynamic', BatonProp)
	baton:SetParent(thisEntity, 'baton')
	baton:SetLocalAngles(110, 180, 90)
	baton:SetLocalOrigin(Vector(0, -2, -1))
	
	batonParticle = SpawnEntityFromTableSynchronous('info_particle_system', BatonParticle)
	batonParticle:SetParent(baton, 'attach_end')
	batonParticle:SetLocalAngles(0, 0, 0)
	batonParticle:SetLocalOrigin(Vector(0, 0, 0))
	
	DoEntFireByInstanceHandle(thisEntity, 'SpeakResponseConcept', 'COMBINESOLDIER_SWITCHINGWEAPONS', 0 ,self, self)
	DoEntFireByInstanceHandle(thisEntity, 'ForceLookAtTarget', '!player', 0 ,self, self)
	
	thisEntity:GetChildren()[1]:SetRenderAlpha(0)
	
	GetPlayerPos = 1
end

function ChargeBatonGlowup()
	DoEntFireByInstanceHandle(batonParticle, 'Start' , '', 0, self, self)
	DoEntFireByInstanceHandle(batonParticle, 'Stop' , '', 1, self, self)
	
	StartSoundEvent('HandFX.Electrocuted', batonParticle)
	StartSoundEvent('Zombie_Revived.ZapAttack', batonParticle)
end

function ChargePreparing()
	DoEntFireByInstanceHandle(thisEntity, 'SpeakResponseConcept', 'COMBINESOLDIER_PREPARECHARGE', 0 ,self, self)
end

function ChargeAnnounce()
	DoEntFireByInstanceHandle(thisEntity, 'SpeakResponseConcept', 'COMBINESOLDIER_ANNOUNCECHARGE', 0 ,self, self)
end

function ChargeFakeout()
	DoEntFireByInstanceHandle(thisEntity, 'SpeakResponseConcept', 'COMBINESOLDIER_PLAYERISHURT', 0 ,self, self)
end

function ChargeStrike()
	StartSoundEvent('Zombie_Revived.ZapAttack', batonParticle)
	DoEntFireByInstanceHandle(batonParticle, 'Start' , '', 0, self, self)
	DoEntFireByInstanceHandle(batonParticle, 'Stop' , '', 1, self, self)
end

function SwitchToGun()
	StopSoundEvent('HandFX.Electrocuted', batonParticle)
	StopSoundEvent('Zombie_Revived.ZapAttack', batonParticle)
	
	if IsValidEntity(baton) then
		baton:Kill()
	end

	DoEntFireByInstanceHandle(thisEntity, 'SpeakResponseConcept', 'COMBINESOLDIER_SWITCHINGWEAPONS', 0 ,self, self)
	DoEntFireByInstanceHandle(thisEntity, 'ClearForcedLookAtTarget', '', 0 ,self, self)
	
	thisEntity:GetChildren()[1]:SetRenderAlpha(255)
	
	GetPlayerPos = 0
	thisEntity:NpcNavClearGoal()
end

-- 1/3 change to activate melee mode when player ran out of ammo and was recently seen
function PlayerScrambling(p)
	if ChargeInProgress == 0 then
		if (table['enemy'] == 'player') and (table['in_combat'] == 1) and (table['timesinceseenplayer'] < 10) then
			local x = RandomInt(1,3)

			if x == 1 then
				DoEntFireByInstanceHandle(thisEntity, 'SetAnimgraphParameter', 'b_melee_mode=true', 1, self, self)
				DoEntFireByInstanceHandle(thisEntity, 'RunScriptCode', 'ChargeInProgress = 0', 15, self, self)

				ChargeInProgress = 1
			end
		end
	end
end


function MainThinkFunc() 
	local localPlayer = Entities:GetLocalPlayer()

	if localPlayer ~= nil then
		if thisEntity:GetHealth() > 0 then
		
			thisEntity:GatherCriteria(table)
			
			-- 1/4 chance to activate melee mode every 5 seconds when player is seen
			if (ChargeInProgress == 0 and ChargeChanceTimeout == 0) then
				if (table['enemy'] == 'player') and (table['in_combat'] == 1) and ( (table['seeplayer'] == 1) or (table['seenbyplayer'] == 1) ) then
				
					local x = RandomInt(1,4)
					
					if x == 1 then
						DoEntFireByInstanceHandle(thisEntity, 'SetAnimgraphParameter', 'b_melee_mode=true', 1, self, self)
						DoEntFireByInstanceHandle(thisEntity, 'RunScriptCode', 'ChargeInProgress = 0', 15, self, self)
						
						ChargeInProgress = 1		
					else
						DoEntFireByInstanceHandle(thisEntity, 'RunScriptCode', 'ChargeChanceTimeout = 0', 5, self, self)
						
						ChargeChanceTimeout = 1
					end
				end
			end
			
			if GetPlayerPos == 1 then
				thisEntity:NpcForceGoPosition( localPlayer:GetAbsOrigin(), false, 64 )
			end
			
		else
			StopSoundEvent('HandFX.Electrocuted', batonParticle)
			StopSoundEvent('Zombie_Revived.ZapAttack', batonParticle)
			
			if IsValidEntity(baton) then
				baton:Kill()
			end
		end
	end

	return 0.1
end
