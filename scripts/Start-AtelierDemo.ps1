[CmdletBinding()]
param([ValidateRange(1024, 65535)][int] $Port = 5093)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$dotnet = if ($env:AARSHJUL_DOTNET) { $env:AARSHJUL_DOTNET } else { (Get-Command dotnet -ErrorAction Stop).Source }
$required = (Get-Content -LiteralPath (Join-Path $repo 'global.json') -Raw | ConvertFrom-Json).sdk.version
Push-Location -LiteralPath $repo
try {
    $actual = (& $dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0 -or $actual -ne $required) { throw "Bruk SDK $required fra global.json via PATH eller AARSHJUL_DOTNET." }
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, $Port)
    try { $listener.Start() } finally { $listener.Stop() }
    $run = Join-Path $repo ('TestResults/93-demo/' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N'))
    $runtime = Join-Path $run 'runtime'
    New-Item -ItemType Directory -Path $runtime -Force | Out-Null
    & $dotnet restore backend/Aarshjul.Web/Aarshjul.Web.csproj --locked-mode
    if ($LASTEXITCODE -ne 0) { throw 'Låst restore feilet. Demoen er ikke startet.' }
    & $dotnet publish backend/Aarshjul.Web/Aarshjul.Web.csproj -c Release --no-restore -o $runtime -m:1 -nr:false
    if ($LASTEXITCODE -ne 0) { throw 'Release-publisering feilet. Demoen er ikke startet.' }
    # Demo-miljøet trenger samme lokale SBR-claimoppsett som den vanlige demoprofilen.
    Copy-Item -LiteralPath (Join-Path $runtime 'appsettings.Development.json') -Destination (Join-Path $runtime 'appsettings.Demo.json')

    # Kopierte, publiserte Release-filer låser ikke repoets byggutdata. Hver start får ny database.
    $values = @{
        ASPNETCORE_ENVIRONMENT = 'Demo'; DOTNET_ENVIRONMENT = 'Demo'; Demo = 'true'
        ConnectionStrings__Aarshjul = ('Data Source=' + (Join-Path $run 'demo.db'))
        DagligEpost__OffentligBasisadresse = "http://127.0.0.1:$Port"
        Logging__LogLevel__Default = 'Warning'
    }
    $previous = @{}
    try {
        foreach ($key in (@($values.Keys) + @('KeyVault__Uri', 'KeyVault:Uri'))) {
            $previous[$key] = @{ Exists = [Environment]::GetEnvironmentVariables('Process').Contains($key); Value = [Environment]::GetEnvironmentVariable($key, 'Process') }
            if ($values.ContainsKey($key)) { [Environment]::SetEnvironmentVariable($key, $values[$key], 'Process') }
            else { Remove-Item -LiteralPath ('Env:' + $key) -ErrorAction SilentlyContinue }
        }
        $dll = Join-Path $runtime 'Aarshjul.Web.dll'
        $process = Start-Process -FilePath $dotnet -ArgumentList @(('"' + $dll + '"'), '--urls', "http://127.0.0.1:$Port") -WorkingDirectory $runtime -WindowStyle Hidden -RedirectStandardOutput (Join-Path $run 'app.log') -RedirectStandardError (Join-Path $run 'app-error.log') -PassThru
    } finally {
        foreach ($key in $previous.Keys) {
            if ($previous[$key].Exists) { [Environment]::SetEnvironmentVariable($key, $previous[$key].Value, 'Process') }
            else { Remove-Item -LiteralPath ('Env:' + $key) -ErrorAction SilentlyContinue }
        }
    }
    $ready = $false
    $deadline = [DateTime]::UtcNow.AddSeconds(40)
    while ([DateTime]::UtcNow -lt $deadline -and -not $process.HasExited) {
        try {
            $response = Invoke-WebRequest -Uri "http://127.0.0.1:$Port/demo" -TimeoutSec 2 -UseBasicParsing
            if ($response.StatusCode -eq 200) { $ready = $true; break }
        } catch { }
        Start-Sleep -Milliseconds 200
        $process.Refresh()
    }
    if (-not $ready) {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id }
        throw 'Denne demoen ble ikke klar og dens egen prosess er stoppet. Lokale logger ligger i den nye TestResults/93-demo-mappen.'
    }
    $info = [ordered]@{ ProcessId = $process.Id; Port = $Port; Sdk = $actual; Revision = (& git rev-parse HEAD); Directory = $run; StartedUtc = [DateTime]::UtcNow.ToString('o') }
    $info | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'demo.json') -Encoding utf8
    Write-Output "Demo klar: http://127.0.0.1:$Port/demo"
    Write-Output "Prosess: $($process.Id). Stopp bare denne med Stop-Process -Id $($process.Id)."
    Write-Output "Ny disponibel database og kopierte Release-filer: $run"
} finally { Pop-Location }
