Param(
  [string]$TempRoot = "$env:TEMP\ProgramaOTLauncherUpdateTest",
  [string]$TargetDir = "$env:TEMP\ProgramaOTLauncherTargetTest"
)

Write-Host "[Test] Preparando diretórios de teste..."
New-Item -ItemType Directory -Force -Path $TempRoot | Out-Null
New-Item -ItemType Directory -Force -Path $TargetDir | Out-Null

$payload = Join-Path $TempRoot 'payload'
New-Item -ItemType Directory -Force -Path $payload | Out-Null

Write-Host "[Test] Criando arquivos fictícios no payload..."
Set-Content -Path (Join-Path $payload 'dummy.txt') -Value "conteudo de teste"

# Opcional: coloque um executável fictício para validar relançamento
Set-Content -Path (Join-Path $payload 'ProgramaOT-Launcher.exe') -Value "exe-fake"

# Copia Updater.exe gerado em Release
$updaterExe = "j:\Projeto\Ot\ProgramaOT-Launcher\Updater\bin\Release\Updater.exe"
if (!(Test-Path $updaterExe)) {
  Write-Warning "Updater.exe não encontrado em $updaterExe. Tentando compilar..."
  $msbuild = (Get-Command msbuild -ErrorAction SilentlyContinue)
  if ($msbuild) {
    & $msbuild.Source "j:\Projeto\Ot\ProgramaOT-Launcher\Updater\Updater.csproj" /p:Configuration=Release
  } else {
    $dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue)
    if ($dotnet) {
      & $dotnet build "j:\Projeto\Ot\ProgramaOT-Launcher\Updater\Updater.csproj" -c Release
    } else {
      Write-Error "Nem msbuild nem dotnet encontrados. Compile o Updater manualmente."
      exit 1
    }
  }
}

if (!(Test-Path $updaterExe)) {
  Write-Error "Updater.exe ainda não existe. Abortando teste."
  exit 1
}

Write-Host "[Test] Executando Updater.exe com payload fictício..."
$args = @(
  '--source', $payload,
  '--target', $TargetDir,
  '--exe', 'ProgramaOT-Launcher.exe',
  '--waitpid', '0'
)
& $updaterExe $args
$exitCode = $LASTEXITCODE
Write-Host "[Test] Updater encerrado com código $exitCode"

Write-Host "[Test] Validando cópia..."
if (Test-Path (Join-Path $TargetDir 'dummy.txt')) {
  Write-Host "[OK] dummy.txt copiado para $TargetDir"
} else {
  Write-Error "[FAIL] dummy.txt não encontrado em $TargetDir"
}

Write-Host "[Test] Concluído."
