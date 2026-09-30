param(
    [Parameter(Mandatory = $true)]
    [string]$SourceDirectory,
    [string]$DestinationDirectory = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'
$src = (Resolve-Path -LiteralPath $SourceDirectory).Path
$dst = (Resolve-Path -LiteralPath $DestinationDirectory).Path

$dirs = @(
    "$dst\images\powers",
    "$dst\images\monsters\wrath_servant",
    "$dst\images\monsters\green_stem_hermit",
    "$dst\images\monsters\hermit_staff",
    "$dst\images\backgrounds\wrath_servant_strong",
    "$dst\audio\sfx\wrath_servant",
    "$dst\audio\sfx\green_stem_hermit"
)
foreach ($d in $dirs) { New-Item -ItemType Directory -Force -Path $d | Out-Null }

$mappings = @{
    "愤怒侍从.png"="images\monsters\wrath_servant\idle.png"
    "愤怒侍从-受击.png"="images\monsters\wrath_servant\hit.png"
    "愤怒侍从-打击.png"="images\monsters\wrath_servant\attack_strike.png"
    "愤怒侍从-斩击.png"="images\monsters\wrath_servant\attack_slash.png"
    "愤怒侍从-斩击2.png"="images\monsters\wrath_servant\attack_slash2.png"
    "愤怒侍从-S1.png"="images\monsters\wrath_servant\s1.png"
    "愤怒侍从-S2.png"="images\monsters\wrath_servant\s2.png"
    "愤怒侍从-S3.png"="images\monsters\wrath_servant\s3.png"
    "愤怒侍从-特殊.png"="images\monsters\wrath_servant\special.png"
    "青林隐士.png"="images\monsters\green_stem_hermit\idle.png"
    "青林隐士-受击.png"="images\monsters\green_stem_hermit\hit.png"
    "青林隐士-伸手.png"="images\monsters\green_stem_hermit\reach.png"
    "青林隐士-击地.png"="images\monsters\green_stem_hermit\ground.png"
    "青林隐士-刺击.png"="images\monsters\green_stem_hermit\thrust.png"
    "隐士之杖.png"="images\monsters\hermit_staff\idle.png"
    "隐士之杖-攻击.png"="images\monsters\hermit_staff\attack.png"
    "隐士之杖-受击.png"="images\monsters\hermit_staff\hit.png"
    "手杖图标.png"="images\powers\wrath_servant_staff_mark_power.png"
    "腐蚀图标.png"=@(
        "images\powers\wrath_servant_corrosion_power.png",
        "images\powers\wrath_servant_next_turn_corrosion_power.png"
    )
    "愤怒侍从背景.png"="images\backgrounds\wrath_servant_strong\background.png"
    "愤怒侍从-打击.ogg"="audio\sfx\wrath_servant\attack_strike.ogg"
    "愤怒侍从-突刺.ogg"="audio\sfx\wrath_servant\attack_thrust.ogg"
    "愤怒侍从-斩击.ogg"="audio\sfx\wrath_servant\attack_slash.ogg"
    "愤怒侍从-强力攻击1.ogg"="audio\sfx\wrath_servant\special_1.ogg"
    "愤怒侍从-强力攻击2.ogg"="audio\sfx\wrath_servant\special_2.ogg"
    "愤怒侍从-强力攻击-结束.ogg"="audio\sfx\wrath_servant\special_end.ogg"
    "愤怒侍从-青林隐士-攻击.ogg"="audio\sfx\green_stem_hermit\attack.ogg"
    "愤怒侍从-青林隐士-强力攻击.ogg"="audio\sfx\green_stem_hermit\strong_attack.ogg"
    "愤怒侍从-青林隐士-击地.ogg"="audio\sfx\green_stem_hermit\ground.ogg"
}

$ok = 0; $miss = 0
foreach ($entry in $mappings.GetEnumerator()) {
    $srcFile = Join-Path $src $entry.Key
    foreach ($destination in @($entry.Value)) {
        $dstFile = Join-Path $dst $destination
        if (Test-Path $srcFile) {
            Copy-Item $srcFile $dstFile -Force
            $ok++
        } else {
            Write-Host "MISS: $($entry.Key)"
            $miss++
        }
    }
}
Write-Host "Copied: $ok, Missing: $miss"
