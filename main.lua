local mp = require("mp")

local discord_client_id_str = "737663962677510245"
local socket_name = "/tmp/shizou-socket"

mp.set_property("input-ipc-server", socket_name)

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

local exePath = script_dir .. "/" .. "Shizou.MpvDiscordPresence";
if not file_exists(exePath) then
    exePath = exePath .. ".exe"
    if not file_exists(exePath) then
	    mp.msg.fatal(exePath .. " not found, stopping")
	    return
    end
end

local function start()
    mp.msg.info("Starting subprocess")
	mp.command_native({
		name = "subprocess",
		playback_only = false,
		args = { exePath, discord_client_id_str, socket_name },
	})
end

mp.register_event("file-loaded", start)

mp.register_event("shutdown", function() end)
