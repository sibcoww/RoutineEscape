$ErrorActionPreference = 'Stop'

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$configPath = 'C:\Program Files\PostgreSQL\17\data\pg_hba.conf'
$backupPath = 'C:\Program Files\PostgreSQL\17\data\pg_hba.conf.routineescape-backup'
$psql = 'C:\Program Files\PostgreSQL\17\bin\psql.exe'
$createUser = 'C:\Program Files\PostgreSQL\17\bin\createuser.exe'
$createDb = 'C:\Program Files\PostgreSQL\17\bin\createdb.exe'
$pgCtl = 'C:\Program Files\PostgreSQL\17\bin\pg_ctl.exe'
$dataDir = 'C:\Program Files\PostgreSQL\17\data'
$statusPath = Join-Path $repoRoot '.postgres-reset-status'

$passwordBytes = New-Object byte[] 24
$rng = [Security.Cryptography.RandomNumberGenerator]::Create()

try {
    $rng.GetBytes($passwordBytes)
}
finally {
    $rng.Dispose()
}

$newPassword = [BitConverter]::ToString($passwordBytes).Replace('-', '')

$original = Get-Content -Raw -LiteralPath $configPath

Copy-Item `
    -LiteralPath $configPath `
    -Destination $backupPath `
    -Force

try {
    $temporaryRules = "host all all 127.0.0.1/32 trust`r`nhost all all ::1/128 trust`r`n"

    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)

    [System.IO.File]::WriteAllText(
        $configPath,
        ($temporaryRules + $original),
        $utf8NoBom
    )

    & $pgCtl reload -D $dataDir

    if ($LASTEXITCODE -ne 0) {
        throw 'Could not reload PostgreSQL configuration.'
    }

    Start-Sleep -Seconds 1

    $roleExists = & $psql `
        -h 127.0.0.1 `
        -U postgres `
        -d postgres `
        -tAc "SELECT 1 FROM pg_roles WHERE rolname='routineescape'"

    if ($LASTEXITCODE -ne 0) {
        throw 'Could not inspect PostgreSQL roles.'
    }

    if ([string]::IsNullOrWhiteSpace($roleExists)) {
        & $createUser `
            -h 127.0.0.1 `
            -U postgres `
            --login routineescape

        if ($LASTEXITCODE -ne 0) {
            throw 'Could not create the routineescape role.'
        }
    }

    & $psql `
        -h 127.0.0.1 `
        -U postgres `
        -d postgres `
        -v ON_ERROR_STOP=1 `
        -c "ALTER ROLE routineescape PASSWORD '$newPassword';" | Out-Null

    if ($LASTEXITCODE -ne 0) {
        throw 'Could not set the routineescape password.'
    }

    $dbExists = & $psql `
        -h 127.0.0.1 `
        -U postgres `
        -d postgres `
        -tAc "SELECT 1 FROM pg_database WHERE datname='routineescape'"

    if ($LASTEXITCODE -ne 0) {
        throw 'Could not inspect PostgreSQL databases.'
    }

    if ([string]::IsNullOrWhiteSpace($dbExists)) {
        & $createDb `
            -h 127.0.0.1 `
            -U postgres `
            -O routineescape `
            routineescape

        if ($LASTEXITCODE -ne 0) {
            throw 'Could not create the routineescape database.'
        }
    }
}
finally {
    Copy-Item `
        -LiteralPath $backupPath `
        -Destination $configPath `
        -Force

    & $pgCtl reload -D $dataDir | Out-Null
}

$connectionString = "Host=localhost;Port=5432;Database=routineescape;Username=routineescape;Password=$newPassword"

Push-Location $repoRoot

try {
    dotnet user-secrets set `
        'ConnectionStrings:RoutineEscape' `
        $connectionString `
        --project RoutineEscape.Bot | Out-Null

    if ($LASTEXITCODE -ne 0) {
        throw 'Could not save the connection string in User Secrets.'
    }

    $env:ROUTINEESCAPE_CONNECTION_STRING = $connectionString

    dotnet ef database update `
        --project RoutineEscape.Infrastructure\RoutineEscape.Infrastructure.csproj

    if ($LASTEXITCODE -ne 0) {
        throw 'Could not apply EF Core migrations.'
    }

    Set-Content `
        -LiteralPath $statusPath `
        -Value 'DATABASE_READY' `
        -Encoding ASCII
}
catch {
    Set-Content `
        -LiteralPath $statusPath `
        -Value ("ERROR: " + $_.Exception.Message) `
        -Encoding UTF8

    throw
}
finally {
    Pop-Location
}
