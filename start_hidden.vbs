Dim path, port
path = chr(34) & WScript.Arguments(0) & chr(34)
port = WScript.Arguments(1)
CreateObject("Wscript.Shell").Run path & " vlc " & port, 0, False
