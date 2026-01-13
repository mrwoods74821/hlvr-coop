--				 === Script made by CementStairs for Cazanu ===

-- This is the main script, that creates the trigger everytime the map is spawned. To add Scalable Init Support, move this file to scripts>vscripts>mods>init> and rename it to the Workshop ID of your addon (it needs to be reuploaded)

function Brual_Activate_Script()
	--StopListeningToGameEvent(Game_started)

	print("Blood Pool Script is running!")

-- Checks whether the main Trigger exists, and if so, removes duplicate entities
	Brual_Entity_Duplicate(Entities:FindByName(nil, "@Blood_Pool_Trigger"))
	Brual_Entity_Duplicate(Entities:FindByName(nil, "@Blood_Pool_Precache"))
	Brual_Entity_Duplicate(Entities:FindByName(nil, "@Blood_Pool_Filter"))

-- Create Convars if they don't exist yet
	Brutal_Make_Convars()

-- Begins to precache
	Brual_Create_Precache()
end

-- Function started after the precache entity was successfully spawned; It spawns both the Filter for npc_combine_s (Combine Soldiers) and the Trigger around the entire map, that attaches the Blood Pool script to the soldier.
function Brual_Spawn_rest()
	Brual_Create_Filter()
	Brual_Create_Trigger()
end

function Brual_Entity_Duplicate(ent)
	if IsValidEntity(ent) == true then
		ent:Kill()
	end
end

function Brual_Create_Trigger()
	Brutal_Trigger = SpawnEntityFromTableSynchronous("trigger_multiple", {
								targetname = "@Blood_Pool_Trigger",
								origin = "0 0 0",
								angles = "0 0 0",
								scales = "2300 2300 2300",
								model = "models/creatures/cat/anim_parent_helper_cube.vmdl",
								StartDisabled = "1",
								spawnflags = "1026",
								filtername = "@Blood_Pool_Filter",
								wait = "1",
	})

	--Trigger:RedirectOutput("OnStartTouch", "Attach_Script", Trigger)


--Adds the outputs that attach the Blood_pool.lua script to the npc_combine_s that touched this trigger
	DoEntFireByInstanceHandle(Brutal_Trigger, "AddOutput", "OnStartTouch>!activator>RunScriptFile>mods/init/blood_pool.lua>0>-1", 0.02, self, self)
	DoEntFireByInstanceHandle(Brutal_Trigger, "AddOutput", "OnStartTouch>!activator>CallScriptFunction>Brutal_Activate_attach>0.01>-1", 0.03, self, self)
	DoEntFireByInstanceHandle(Brutal_Trigger, "Enable", nil, 0.04, self, self)

end

function Brual_Create_Filter()
	Brutal_Filter = SpawnEntityFromTableSynchronous("filter_activator_class", {
								targetname = "@Blood_Pool_Filter",
								Negated = "0",
								filterclass = "npc_combine_s",
	})
end

function Brual_Create_Precache()
	Brutal_Precache_Ent = SpawnEntityFromTableAsynchronous("logic_script", {
								targetname = "@Blood_Pool_Precache",
								vscripts = "mods/init/precache.lua"
	}, Brual_Spawn_rest, nil)
end

function Brutal_Make_Convars()
	if Convars:GetInt('brutal_compatibility_override_dism_models') == nil then
		Convars:RegisterConvar("brutal_compatibility_override_dism_models", "1", "Should Dismemberment models be overriden", 0)
	end

	if Convars:GetInt('brutal_compatibility_override_dism_particles') == nil then
		Convars:RegisterConvar("brutal_compatibility_override_dism_particles", "1", "Should Dismemberment particles be overriden", 0)
	end

	if Convars:GetInt('brutal_compatibility_dism_only_explosions') == nil then
		Convars:RegisterConvar("brutal_compatibility_dism_only_explosions", "1", "Should Dismemberment allow only for explosion kills", 0)
	end
end

-- This makes sure the script starts after the played has "joined" the game
Brual_Game_started = ListenToGameEvent("player_activate", Brual_Activate_Script, _G)














-- Old method using activators; doesn't seem to work for trigger_multiple
--[[
function Attach_Script(activator)
print(activator:GetName())

	DoEntFireByInstanceHandle(activator, "RunScriptFile", "mods/init/blood_pool.lua", 0, self, self)
end
]]--

