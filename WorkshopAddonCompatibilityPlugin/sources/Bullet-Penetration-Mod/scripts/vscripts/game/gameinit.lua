if Convars:GetStr("default_enabled_addons_list"):find("2182586257") then
    print("Scalable Init Detected")
    SendToConsole("addon_enable 2182586257")
else
    print("Scalable Init Support not detected, starting the mod manually")
    require("penetration/penetration_prepare")
end