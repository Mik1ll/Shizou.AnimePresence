local mp = require("mp")

local function file_exists(name)
   local f=io.open(name,"r")
   if f~=nil then io.close(f) return true else return false end
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

mp.set_property("input-ipc-server", "tmp/mpvsocket")

local cmd = nil

local function start()
	if cmd == nil then
		cmd = mp.command_native_async({
			name = "subprocess",
			playback_only = false,
			args = { exePath },
		}, function() end)
		mp.msg.info("launched subprocess")
		mp.osd_message("Discord Rich Presence: Started")
	end
end

local function stop()
    if cmd ~= nil then
        mp.abort_async_command(cmd)
        cmd = nil
        mp.msg.info("aborted subprocess")
        mp.osd_message("Discord Rich Presence: Stopped")
	end
end

mp.register_event("file-loaded", start)

mp.register_event("shutdown", stop)
