campaign_config = {}

campaign_config.gamemodetype = "campaign"
campaign_config.pregame_timer = 60 -- Time in seconds before the game starts
campaign_config.checkpoints = true -- Enable checkpoints
campaign_config.respawn = true -- Enable respawning

campaign_config.sharing = {}
campaign_config.sharing.resin = false -- Share resin with other players
campaign_config.sharing.ammo = false -- Share ammo with other players
campaign_config.sharing.health = false -- Share health with other players

lua_config.sub_gamemodes[campaign_config.gamemodetype] = "Story Mode"
