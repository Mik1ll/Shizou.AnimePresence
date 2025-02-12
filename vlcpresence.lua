local dkjson = require("dkjson")

local function callback_status(data, query)
    local status = {}
    local item = vlc.input.item() -- vlc 4.0 ~ vlc.player.item()
    if item then
        status.uri = item:uri()
        status.speed = vlc.var.get(vlc.object.input(), "rate") -- vlc 4.0 ~ vlc.player.get_rate()
        status.paused = vlc.playlist.status() ~= "playing"
        local duration = item:duration()
        if duration > 0 then status.duration = duration end
        local time = vlc.var.get(vlc.object.input(), "time")
        if time > 0 then
            status.time = time / 1000000
        end
    end
    return next(status) == nil and '{}' or dkjson.encode(status)
end

local function sleep(sec)
    vlc.misc.mwait(vlc.misc.mdate() + sec * 1000000)
end

sleep(10)

math.randomseed(os.time())
local oldhost = vlc.config.get("http-host")
local host = "127.234.133.79"
vlc.config.set("http-host", host)
local port = math.random(1024, 65535)
local oldport = vlc.config.get("http-port")
vlc.config.set("http-port", port)

vlc.msg.info("host: " .. host .. ':' .. port)

local h = vlc.httpd()
local statush = h:file("/status.json", "application/json", nil, "password", callback_status, nil)

-- vlc.config.set("http-host", oldhost)
-- vlc.config.set("http-port", oldport)

local function get_OS()
    return package.config:sub(1, 1) == "\\" and "win" or "unix"
end

local directory = select(1, debug.getinfo(1,'S').source:match([=[^@(.*[/\])]=]))
if directory == nil then
   error("Unable to find directory from debug info") 
end
vlc.msg.info("directory: " .. directory)

local cmd = ""
if get_OS() == "win" then
    cmd = 'WScript.exe "' .. directory .. 'start_hidden.vbs" "' .. directory .. 'Shizou.AnimePresence.exe" vlc ' .. port
else
    cmd = "'" .. directory .. "Shizou.AnimePresence' vlc " .. port 
end

vlc.msg.info("starting presence: " .. cmd)
os.execute(cmd)
