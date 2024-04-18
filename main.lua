local mp = require("mp")

local discord_client_id_str = "737663962677510245"

local function file_exists(name)
	local f = io.open(name, "r")
	if f ~= nil then
		io.close(f)
		return true
	else return false end
end

local script_dir = mp.get_script_directory()
if script_dir == nil then
	mp.msg.fatal("Not running in script directory, stopping")
	return
end
local exeName = "Shizou.MpvDiscordPresence.exe";
local exePath = script_dir .. "/" .. exeName;
if not file_exists(exePath) then
	mp.msg.fatal(exeName .. " not found, stopping")
	return
end

mp.set_property("input-ipc-server", "shizou-socket")

local function start()
    mp.msg.info("Starting subprocess")
	mp.command_native({
		name = "subprocess",
		playback_only = false,
		args = { exePath, discord_client_id_str },
	})
end

mp.register_event("file-loaded", start)

mp.register_event("shutdown", function() end)
