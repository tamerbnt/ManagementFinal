Get-ChildItem -Path "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy" -Filter *.cs -Recurse | Where-Object { $_.Length -eq 0 } | Select-Object FullName
