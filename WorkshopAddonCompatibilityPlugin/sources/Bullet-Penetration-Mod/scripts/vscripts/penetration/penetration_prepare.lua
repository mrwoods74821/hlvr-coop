function createPenetration_Script_Main()
	print("Penetration Preparation running...")
	
	local peneScript = Entities:FindByName(nil, "@Penetration_Script_Main")

	penetration_setUpConvars()


	if not IsValidEntity(peneScript) then
		peneScript = SpawnEntityFromTableAsynchronous("logic_script", {
			targetname = "@Penetration_Script_Main";
			vscripts = "penetration/penetration.lua";
		}, nil, nil)
	end
end

function penetration_setUpConvars()
	-- ==== PISTOL ====
	createConvarIfNotExisitng("penetration_wpn_pistol_cvr_max","4", "How many times it can penetrate", 0)
	createConvarIfNotExisitng("penetration_wpn_pistol_cvr_thickness","10", "Thickest wall it can penetrate, in units", 0)
	createConvarIfNotExisitng("penetration_wpn_pistol_cvr_totalthickness","192", "Total amount of allowed units that it can penetrate", 0)
	createConvarIfNotExisitng("penetration_wpn_pistol_cvr_maxdistance","3000", "Maximum distance the bullet can fly", 0)
	createConvarIfNotExisitng("penetration_wpn_pistol_cvr_push","0", "Push of the bullet", 0)
	createConvarIfNotExisitng("penetration_wpn_pistol_cvr_dmg",tostring(Convars:GetFloat("vr_energygun_dmg") or 8), "Dmg of the bullet", 0)
	createConvarIfNotExisitng("penetration_wpn_pistol_cvr_spread","0", "Spread of the bullet", 0)
	createConvarIfNotExisitng("penetration_wpn_pistol_cvr_bullets","1", "Number of the bullets that penetrate", 0)
	-- ==== SMG ====
	createConvarIfNotExisitng("penetration_wpn_smg_cvr_max","3", "How many times it can penetrate", 0)
	createConvarIfNotExisitng("penetration_wpn_smg_cvr_thickness","12", "Thickest wall it can penetrate, in units", 0)
	createConvarIfNotExisitng("penetration_wpn_smg_cvr_totalthickness","192", "Total amount of allowed units that it can penetrate", 0)
	createConvarIfNotExisitng("penetration_wpn_smg_cvr_maxdistance","3000", "Maximum distance the bullet can fly", 0)
	createConvarIfNotExisitng("penetration_wpn_smg_cvr_push","0", "Push of the bullet", 0)
	createConvarIfNotExisitng("penetration_wpn_smg_cvr_dmg",tostring(Convars:GetFloat("vr_rapidfire_dmg") or 4), "Dmg of the bullet", 0)
	createConvarIfNotExisitng("penetration_wpn_smg_cvr_spread","0", "Spread of the bullet", 0)
	createConvarIfNotExisitng("penetration_wpn_smg_cvr_bullets","1", "Number of the bullets that penetrate", 0)
	-- ==== SHOTGUN ====
	createConvarIfNotExisitng("penetration_wpn_shotgun_cvr_max","3", "How many times it can penetrate", 0)
	createConvarIfNotExisitng("penetration_wpn_shotgun_cvr_thickness","16", "Thickest wall it can penetrate, in units", 0)
	createConvarIfNotExisitng("penetration_wpn_shotgun_cvr_totalthickness","256", "Total amount of allowed units that it can penetrate", 0)
	createConvarIfNotExisitng("penetration_wpn_shotgun_cvr_maxdistance","1000", "Maximum distance the bullet can fly", 0)
	createConvarIfNotExisitng("penetration_wpn_shotgun_cvr_push","0", "Push of the bullet", 0)
	createConvarIfNotExisitng("penetration_wpn_shotgun_cvr_dmg",tostring(Convars:GetFloat("sk_npc_dmg_buckshot") or 8), "Dmg of the bullet", 0)
	createConvarIfNotExisitng("penetration_wpn_shotgun_cvr_spread","0.1", "Spread of the bullet", 0)
	createConvarIfNotExisitng("penetration_wpn_shotgun_cvr_bullets","7", "Number of the bullets that penetrate", 0)
	-- ==== CUSTOM ====
	createConvarIfNotExisitng("penetration_wpn_custom_cvr_max","4", "How many times it can penetrate", 0)
	createConvarIfNotExisitng("penetration_wpn_custom_cvr_thickness","10", "Thickest wall it can penetrate, in units", 0)
	createConvarIfNotExisitng("penetration_wpn_custom_cvr_totalthickness","192", "Total amount of allowed units that it can penetrate", 0)
	createConvarIfNotExisitng("penetration_wpn_custom_cvr_maxdistance","3000", "Maximum distance the bullet can fly", 0)
	createConvarIfNotExisitng("penetration_wpn_custom_cvr_push","0", "Push of the bullet", 0)
	createConvarIfNotExisitng("penetration_wpn_custom_cvr_dmg",tostring(Convars:GetFloat("vr_energygun_dmg") or 8), "Dmg of the bullet", 0)
	createConvarIfNotExisitng("penetration_wpn_custom_cvr_spread","0", "Spread of the bullet", 0)
	createConvarIfNotExisitng("penetration_wpn_custom_cvr_bullets","1", "Number of the bullets that penetrate", 0)

	SendToConsole("execifexists bulletpenetration_config")
end

function createConvarIfNotExisitng(name, defaultValue, helpText, flags)
	if Convars:GetFloat(name) == nil then
		Convars:RegisterConvar(name, defaultValue, helpText, flags)
	end
end

ListenToGameEvent("player_activate", createPenetration_Script_Main, nil)