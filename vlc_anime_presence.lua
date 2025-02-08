local dkjson = require("dkjson")
local httprequests = require("httprequests")

local function callback_status(data, query)
    local status = {}
    local item = vlc.player.item()
    if item then
        status.uri = item.uri() 
    end
    
    return dkjson.encode(status)
end

local function sleep(sec)
    vlc.misc.mwait(vlc.misc.mdate() + sec * 1000000)
end


local allow_restricted = "false"

local discord_client_id_str = "1230418743734042694"

sleep(5)

math.randomseed(os.time())
local oldhost = vlc.config.get("http-host")
local host = "127.234.133.79"
vlc.config.set("http-host", host)
local port = math.random(1024, 65535)
local oldport = vlc.config.get("http-port")
vlc.config.set("http-port", port)

vlc.msg.info("host:" .. host)
vlc.msg.info("port:" .. port)

local h = vlc.httpd()
local statush = h:file("/status.json", "application/json", nil, "password", callback_status, nil)

vlc.config.set("http-host", oldhost)
vlc.config.set("http-port", oldport)

local function find_in_datadir(subpath)
    local list = vlc.config.datadir_list(subpath)
    for _, l in ipairs(list) do
        local s = vlc.net.stat(l)
        if s then
            return l
        end
    end
    return nil
end

local subpath = "intf/Shizou.AnimePresence"
local exePath = find_in_datadir(subpath) or find_in_datadir(subpath .. ".exe")
if not exePath then
    vlc.msg.err("Unable to find " .. subpath .. " in datadirs")
    return
end
vlc.msg.info("exePath:" .. exePath)
os.execute('"' .. exePath .. '"' .. discord_client_id_str .. ' ' .. allow_restricted .. ' vlc ' .. port)
