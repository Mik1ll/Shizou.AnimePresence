local function status_callback_inner()
    local item = vlc.input.item()
    local str = "{"
    if item then
        str = str .. '"uri":"' .. item:uri() .. '",'
        str = str .. '"speed":' .. tostring(vlc.var.get(vlc.object.input(), "rate")) .. ","
        str = str .. '"paused":' .. tostring(vlc.playlist.status() ~= "playing") .. ','
        local duration = item:duration()
        if duration > 0 then
            str = str .. '"duration":' .. duration .. ","
        end
        local time = vlc.var.get(vlc.object.input(), "time")
        if time > 0 then
            str = str .. '"time":' .. tostring(time / 1000000) .. ','
        end
        str = str:sub(1, #str - 1) .. "}"
    else
        str = str .. "}"
    end
    return str
end

local function status_callback(params, url, query, r_type, data, addr, host)
    vlc.msg.info(string.format("Callback params:\n%s\n%s\n%s\n%s\n%s\n%s\n%s", tostring(params), tostring(url),
        tostring(query), tostring(r_type), tostring(data), tostring(addr), tostring(host)))
    if r_type == 3 then
        return "Status: 200\nContent-Type: application/json\n\n"
    end

    local ok, content = pcall(status_callback_inner)
    if not ok then
        local errmsg = "Experienced an error while getting the status: " .. tostring(content)
        local resp = "Status: 500\nContent-Type: text/plain\nContent-Length: " .. #errmsg .. "\n\n" .. errmsg
        vlc.msg.info(resp)
        return resp
    end
    local resp = "Status: 200\nContent-Type: application/json\nContent-Length: " .. #content .. "\n\n" .. content
    vlc.msg.info(resp)
    return resp
end

local function sleep(sec)
    vlc.misc.mwait(vlc.misc.mdate() + sec * 1000000)
end

local host = "127.234.133.79"
math.randomseed(os.time())
local port = math.random(1024, 65535)

local oldhost = vlc.config.get("http-host")
vlc.config.set("http-host", host)
local oldport = vlc.config.get("http-port")
vlc.config.set("http-port", port)

-- Assign global to prevent garbage collection
_G.httpd = vlc.httpd()
_G.file_handler = httpd:handler("/status", nil, "password", status_callback, nil)

vlc.config.set("http-host", oldhost)
vlc.config.set("http-port", oldport)

vlc.msg.info("Status: http://" .. host .. ':' .. port .. '/status')

local directory = select(1, debug.getinfo(1, 'S').source:match([=[^@(.*[/\])]=]))
if directory == nil then
    vlc.msg.err("Unable to find directory from debug info")
end
vlc.msg.info("Directory: " .. directory)

local cmd = ""
if vlc.win == nil then
    cmd = "'" .. directory .. "Shizou.AnimePresence' vlc " .. port
else
    cmd = 'WScript.exe "' .. directory .. 'start_hidden.vbs" "' .. directory .. 'Shizou.AnimePresence.exe" vlc ' .. port
end
vlc.msg.info("Starting presence: " .. cmd)
os.execute(cmd)
