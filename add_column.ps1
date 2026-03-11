$dbPath = "Management.Presentation\bin\Debug\net8.0-windows\Management.db"
$dllPath = "Management.Presentation\bin\Debug\net8.0-windows\Microsoft.Data.Sqlite.dll"
Add-Type -Path $dllPath
$conn = New-Object Microsoft.Data.Sqlite.SqliteConnection("Data Source=$dbPath")
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "ALTER TABLE gym_settings ADD COLUMN DailyRevenueTarget TEXT NOT NULL DEFAULT '10000';"
try {
    $cmd.ExecuteNonQuery()
    Write-Host "Success: DailyRevenueTarget added to gym_settings."
} catch {
    Write-Host "Note: Column might already exist or another error occurred: $_"
}
$conn.Close()
