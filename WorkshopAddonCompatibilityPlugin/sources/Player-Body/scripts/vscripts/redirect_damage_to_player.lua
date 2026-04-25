function Activate()
    localPlayer = Entities:GetLocalPlayer()
end

function OnTakeDamage(dmgInfo)
    for k,v in pairs(dmgInfo) do
        if (k=="damage") then dmgAmount = v end
        if (k=="damage_position") then dmgPosition = v end
        if (k=="damage_force") then dmgForce = v end
		if (k=="inflictor") then dmgInflictor = v end
        if (k=="attacker") then dmgAttacker = v end
    end
	
	if (localPlayer:GetHealth() > Convars:GetInt('vr_rapidfire_dmg')) then -- Temporary fix for killing self with SMG causing game crash
		local ok, damageInfo = pcall(CreateDamageInfo, dmgInflictor, dmgAttacker, dmgForce, dmgPosition, dmgAmount, 0)
		if ok then
			localPlayer:TakeDamage(damageInfo)
		end
	end
	
end