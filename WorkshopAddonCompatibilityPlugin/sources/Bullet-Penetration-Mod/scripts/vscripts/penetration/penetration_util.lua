function CBaseEntity:transformToLocal(vec)
	vec = vec-self:GetAbsOrigin()

	local R = self:GetRightVector()	
	local U = self:GetUpVector()	
	local F = self:GetForwardVector()

	local invertRotationMatrix =
	{
		{R.x, R.y, R.z, 0,};
		{U.x, U.y, U.z, 0,};
		{F.x, F.y, F.z, 0,};
		{0,   0,   0,   1,};
	}
	
	vec = {vec.x, vec.y, vec.z, 1}
	local result = matrixVectorMultiply(invertRotationMatrix, vec)

	return Vector(result[1], result[2], result[3])
end

function CBaseEntity:transformToWorld(vec)
	local R = self:GetRightVector()	
	local U = self:GetUpVector()	
	local F = self:GetForwardVector()

	local rotationMatrix =
	{
		{R.x; U.x; F.x; 0;};
		{R.y; U.y; F.y; 0;};
		{R.z; U.z; F.z; 0;};
		{0;   0;   0;   1;};
	}

	vec = {vec.x; vec.y; vec.z; 1;}
	local result = matrixVectorMultiply(rotationMatrix, vec)

	return Vector(result[1], result[2], result[3])+self:GetAbsOrigin()
end

function matrixVectorMultiply(mat, vec)
    local result = {}
    for i = 1, 4 do
        result[i] = 0
        for j = 1, 4 do
            result[i] = result[i] + mat[i][j] * vec[j]
        end
    end
    return result
end