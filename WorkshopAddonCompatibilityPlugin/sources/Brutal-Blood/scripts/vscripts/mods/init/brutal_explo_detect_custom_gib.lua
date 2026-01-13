function UpdateOnRemove()
	if IsValidEntity(thisEntity:GetMoveParent()) then
		if thisEntity:GetMoveParent():IsAlive() then
			DoEntFireByInstanceHandle(thisEntity:GetMoveParent(), "CallScriptFunction", "Brutal_Explosion_Gib_Spawn", 0, self, self)
		end
	end
end