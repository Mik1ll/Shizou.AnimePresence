Dim Arguments
Arguments = ""
For Each strArg in WScript.Arguments
  Arguments = Arguments & """" & strArg & """ "
Next
Arguments = Trim(Arguments)
CreateObject("Wscript.Shell").Run Arguments, 0, False
