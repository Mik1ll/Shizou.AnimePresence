Dim path, discordclientid, allowrestricted, port
path = chr(34) & WScript.Arguments(0) & chr(34)
discordclientid = WScript.Arguments(1)
allowrestricted = WScript.Arguments(2)
port = WScript.Arguments(3)
CreateObject("Wscript.Shell").Run path & " " & discordclientid & " " & allowrestricted & " vlc " & port, 0, False
