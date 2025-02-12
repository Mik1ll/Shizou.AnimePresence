local directory = select(1, debug.getinfo(1,'S').source:match([=[^@(.*[/\])]=]))
if directory == nil then
   error("Unable to find directory from debug info") 
end

if mp ~= nil then
    dofile(directory .. "mpvpresence.lua")
elseif vlc ~= nil then
    dofile(directory .. "vlcpresence.lua")
else
    error("Not running in mpv or vlc environment")
end
