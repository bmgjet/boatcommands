boatcommands
============

BoatCommands is an Oxide plugin for the game Rust that adds an in-game UI
for controlling player boats. It allows players to toggle engines, sails,
reverse, and anchors via buttons, and provides admin-only cannon automation
with NPC gunners and a firing UI.

Author: bmgjet
Version: 1.0.0

------------------------------------------------------------
SCREENSHOT
------------------------------------------------------------

https://github.com/bmgjet/boatcommands/blob/main/screenshot.png

------------------------------------------------------------
FEATURES
------------------------------------------------------------

• On-screen UI when mounted on a player boat  
• Toggle boat engines on/off  
• Open/close sails  
• Toggle engine reverse (also rotates sails if allowed)  
• Raise/lower anchors with delayed UI refresh  
• Automatic UI refresh while mounted  
• Cooldown protection on commands  
• Permission-based access  

ADMIN FEATURES:
• Spawn NPC gunners to man boat cannons  
• Fire individual cannons via chat command or UI buttons  
• Cannon UI auto-generated per boat  
• NPC gunners auto-reload cannons  
• Cleanup of NPCs and mounted cannons on unload  

------------------------------------------------------------
PERMISSIONS
------------------------------------------------------------

boatcommands.use
Required to use the boat control UI.

Register with:
oxide.grant group default boatcommands.use

------------------------------------------------------------
CHAT & CONSOLE COMMANDS
------------------------------------------------------------

Player Commands (requires permission):
/engine
- Toggle all boat engines on or off

/sails
- Open or close all sails

/reverse
- Toggle engine reverse (also rotates sails if possible)

/boatanchor
- Raise or lower all anchors on the boat

Admin Commands:
/mancannon
- Look at a cannon on a player boat and spawn an NPC gunner
- Returns a cannon ID for firing

/manallcannons
- Spawns NPC gunners on all cannons attached to the boat

/firecannon <id>
- Fires a specific manned cannon by ID

Console Command:
boat.firecannon <id>
- Fires a cannon from console or UI button

------------------------------------------------------------
HOW IT WORKS
------------------------------------------------------------

• UI appears when a player mounts a PlayerBoat
• UI refreshes every 3 seconds while mounted
• UI is removed when the player dismounts
• Commands are rate-limited to prevent spam
• NPC gunners are created using player prefabs
• NPCs are equipped with hazmat suits
• Cannons are automatically reloaded after firing
• All NPCs and mounts are cleaned up on plugin unload

------------------------------------------------------------
INSTALLATION
------------------------------------------------------------

1. Copy boatcommands.cs into:
   oxide/plugins/

2. Reload the plugin:
   oxide.reload boatcommands

3. Grant permission:
   oxide.grant user <name|steamid> boatcommands.use

------------------------------------------------------------
NOTES
------------------------------------------------------------

• Admin-only cannon controls
• UI only appears on player boats
• Boat authorization is respected
• Safe cleanup on plugin unload
• No configuration file required

------------------------------------------------------------
LICENSE
------------------------------------------------------------

This plugin is provided as-is.
You may modify and redistribute for personal or server use
You must not sell any of this code.

------------------------------------------------------------
END
------------------------------------------------------------
