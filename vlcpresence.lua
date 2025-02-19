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

local oldhost = vlc.config.get("http-host")
local oldport = vlc.config.get("http-port")
local host = "127.234.133.79"
math.randomseed(os.time())
local port = math.random(1024, 65535)

vlc.msg.info("status: http://" .. host .. ':' .. port .. '/status.json')

vlc.config.set("http-host", host)
vlc.config.set("http-port", port)
local h = vlc.httpd()
local fileudata = h:file("/status.json", "application/json", nil, "password", callback_status, nil)
vlc.config.set("http-host", oldhost)
vlc.config.set("http-port", oldport)

sleep(5)

local directory = select(1, debug.getinfo(1, 'S').source:match([=[^@(.*[/\])]=]))
if directory == nil then
    vlc.msg.err("Unable to find directory from debug info")
end
vlc.msg.info("directory: " .. directory)

local cmd = ""
-- cmd = "'" .. directory .. "Shizou.AnimePresence' vlc " .. port
cmd = 'WScript.exe "' .. directory .. 'start_hidden.vbs" "' .. directory .. 'Shizou.AnimePresence.exe" vlc ' .. port

vlc.msg.info("starting presence: " .. cmd)
os.execute(cmd)
