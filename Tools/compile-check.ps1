# compile-check.ps1 -- offline C# syntax/API check for Astro Aces runtime scripts.
#
# WHY THIS EXISTS: Unity's own compile loop requires the Editor to be focused and to
# reimport, which is slow and cannot be triggered reliably from an agent session.
# This compiles Assets/AstroAces/Scripts/**.cs against the exact same reference
# assemblies Unity uses, so a wrong API name (e.g. rb.velocity instead of
# rb.linearVelocity) is caught in ~5 seconds instead of after a scene test.
#
# It is a SYNTAX + API check only. It does not run the game, does not validate
# scene wiring, and a clean result does NOT mean the gameplay is correct.
#
# Usage:   powershell -ExecutionPolicy Bypass -File Tools\compile-check.ps1
# Exit 0 = clean. Non-zero = compile errors (printed).

$ErrorActionPreference = 'Stop'

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$UnityRoot   = 'C:\Program Files\Unity\Hub\Editor\6000.3.21f1\Editor\Data'
$Csc         = Join-Path $UnityRoot 'DotNetSdkRoslyn\csc.dll'
$OutDir      = Join-Path $env:TEMP 'astroaces-compilecheck'
$Rsp         = Join-Path $OutDir 'compile.rsp'

if (-not (Test-Path $Csc)) { Write-Error "Roslyn not found at $Csc - is Unity 6000.3.21f1 installed?" }
if (-not (Test-Path $OutDir)) { New-Item -ItemType Directory -Force $OutDir | Out-Null }

# Source files: every runtime script under Assets/AstroAces (our own code, plus vendored
# third-party like ThirdParty/UTI). Editor-only and test scripts are excluded because they
# reference UnityEditor / NUnit assemblies.
$ScriptRoot = Join-Path $ProjectRoot 'Assets\AstroAces'
if (-not (Test-Path $ScriptRoot)) { Write-Host "No scripts yet at $ScriptRoot - nothing to check."; exit 0 }
$Sources = Get-ChildItem -Path $ScriptRoot -Filter *.cs -Recurse -File |
           Where-Object { $_.FullName -notmatch '\\Editor\\' -and $_.FullName -notmatch '\\Tests\\' }
if ($Sources.Count -eq 0) { Write-Host "No .cs files found - nothing to check."; exit 0 }

# Reference assemblies: netstandard facade + every UnityEngine module + the
# package assemblies Unity already built into Library\ScriptAssemblies.
$Refs = @()
$Refs += Join-Path $UnityRoot 'NetStandard\ref\2.1.0\netstandard.dll'
$Refs += (Get-ChildItem (Join-Path $UnityRoot 'NetStandard\compat\2.1.0\shims\netfx') -Filter *.dll -File).FullName
$Refs += (Get-ChildItem (Join-Path $UnityRoot 'Managed\UnityEngine') -Filter UnityEngine*.dll -File).FullName
$ScriptAsm = Join-Path $ProjectRoot 'Library\ScriptAssemblies'
if (Test-Path $ScriptAsm) {
    # Exclude Assembly-CSharp* and AstroAces* -- Unity's live editor keeps recompiling those
    # from the same sources we're about to compile fresh, which would define every type
    # twice (CS0436 warnings, harmless but noisy). Real third-party/package assemblies only.
    $Refs += (Get-ChildItem $ScriptAsm -Filter *.dll -File |
              Where-Object { $_.Name -notmatch 'Editor|^Assembly-CSharp|^AstroAces' }).FullName
}

# LangVersion 9.0 / netstandard2.1 mirrors Assembly-CSharp.csproj exactly.
# If the executor writes C# 10+ syntax (file-scoped namespaces, global usings)
# it will fail here, which is the point -- Unity 6.3 would reject it too.
$lines = @('-target:library', '-nologo', '-nostdlib+', '-langversion:9.0',
           "-out:`"$(Join-Path $OutDir 'AstroAces.Runtime.dll')`"")
foreach ($r in ($Refs | Sort-Object -Unique)) { $lines += "-r:`"$r`"" }
foreach ($s in $Sources) { $lines += "`"$($s.FullName)`"" }
Set-Content -Path $Rsp -Value $lines -Encoding utf8

Write-Host "Compiling $($Sources.Count) script(s) against Unity 6000.3.21f1 reference assemblies..."
& dotnet $Csc "@$Rsp"
$code = $LASTEXITCODE
if ($code -eq 0) { Write-Host "COMPILE CHECK PASSED ($($Sources.Count) files)." }
else { Write-Host "COMPILE CHECK FAILED (exit $code)." }
exit $code
