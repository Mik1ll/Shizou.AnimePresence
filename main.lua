local mp = require("mp")
local utils = require("mp.utils")

local discord_client_id_str = "1230418743734042694"
local pid = utils.getpid()
local socket_name = "/tmp/mpv-socket" .. '-' .. pid
local toggle_keybind = "ctrl+d"

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

local presence_command = nil

local function start()
    mp.msg.info("Starting subprocess")
	presence_command, err = mp.command_native_async({
		name = "subprocess",
		playback_only = false,
		args = { exePath, discord_client_id_str, socket_name },
	})
	if err ~= nil then
        mp.msg.error("Subprocess failed to start: " .. err)
    end
end

local function stop()
    if presence_command ~= nil then
        mp.msg.info("Stopping subprocess")
        mp.abort_async_command(presence_command)
        presence_command = nil
    else
        mp.msg.warn("No subprocess to stop")
    end
end

local function toggle()
   if presence_command ~= nil then
       mp.osd_message("Stopping Discord Presence")
       stop()
   else
       mp.osd_message("Starting Discord Presence")
       start()
   end
end

mp.set_property("input-ipc-server", socket_name)

if toggle_keybind ~= nil then
    mp.add_key_binding(toggle_keybind, "toggleDiscordMpv", toggle)
end

mp.register_event("file-loaded", start)

mp.register_event("shutdown", stop)
