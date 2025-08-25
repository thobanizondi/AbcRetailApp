# Centralized Logging (FR008, FR009)

Logs are written to Azure File Share (configured via `Storage:FileShareLogs`). To mount the Azure File Share on a Windows VM:

1. Retrieve the storage account key.
2. Run (PowerShell):
```
$acct = "<storageAccountName>"
$share = "logs"
$pwd = ConvertTo-SecureString -String "<storageKey>" -AsPlainText -Force
New-PSDrive -Name Z -PSProvider FileSystem -Root "\\\\$acct.file.core.windows.net\\$share" -Credential (New-Object System.Management.Automation.PSCredential ("Azure\\$acct", $pwd)) -Persist
```
Then view `info.log` and `error.log`.
