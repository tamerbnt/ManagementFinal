# Load the SQLite assembly (adjust path if necessary based on NuGet packages or provided DLLs)
# The application bin folder should have the necessary DLLs
$binPath = "c:\Users\techbox\.gemini\antigravity\scratch\ManagementCopy\Management.Presentation\bin\Debug\net8.0-windows"
$dllPath = Join-Path $binPath "System.Data.SQLite.dll"

# If System.Data.SQLite.dll is not directly there, try loading Microsoft.Data.Sqlite.dll which is standard for EF Core
$efDllPath = Join-Path $binPath "Microsoft.Data.Sqlite.dll"

if (Test-Path $efDllPath) {
    Add-Type -Path $efDllPath
    $connectionString = "Data Source=$binPath\Management.db"
    $connection = New-Object Microsoft.Data.Sqlite.SqliteConnection($connectionString)
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandText = "SELECT full_name, facility_id, is_active, is_deleted, role FROM staff_members"
        $reader = $command.ExecuteReader()
        
        Write-Output "--- STAFF DUMP START ---"
        while ($reader.Read()) {
            $name = $reader["full_name"]
            $facId = $reader["facility_id"]
            $active = $reader["is_active"]
            $deleted = $reader["is_deleted"]
            $role = $reader["role"]
            Write-Output "Name: $name | Facility: $facId | Active: $active | Deleted: $deleted | Role: $role"
        }
        Write-Output "--- STAFF DUMP END ---"
    }
    catch {
        Write-Error $_
    }
    finally {
        $connection.Close()
    }
} else {
    Write-Error "Could not find Microsoft.Data.Sqlite.dll in $binPath"
}
