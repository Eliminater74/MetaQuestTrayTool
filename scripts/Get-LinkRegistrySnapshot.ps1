<#
.SYNOPSIS
Reads only tray-owned Meta Link overrides for before/after ODT comparisons.
.DESCRIPTION
Does not write registry values, launch Meta software, or restart services.
Includes absent values and registry kinds so defaults, wrong types, and view
differences remain visible. Does not recurse into Air Link pairing data.
#>
[CmdletBinding()]
param()

$valueNames = @(
    'BitrateMbps', 'EncodeWidth', 'EncodeResolutionWidth', 'HEVC', 'NumSlices',
    'LinkSharpeningEnabled', 'DistortionCurve', 'DBR', 'DBRMax', 'DBROffsetMbps', 'MobileASWMode'
)
$registryPath = 'Software\Oculus\RemoteHeadset'
foreach ($hive in @([Microsoft.Win32.RegistryHive]::CurrentUser, [Microsoft.Win32.RegistryHive]::LocalMachine)) {
    foreach ($view in @([Microsoft.Win32.RegistryView]::Registry64, [Microsoft.Win32.RegistryView]::Registry32)) {
        $baseKey = $null
        $linkKey = $null
        try {
            $baseKey = [Microsoft.Win32.RegistryKey]::OpenBaseKey($hive, $view)
            $linkKey = $baseKey.OpenSubKey($registryPath, $false)
            $presentNames = if ($null -ne $linkKey) { $linkKey.GetValueNames() } else { @() }
            $values = foreach ($name in $valueNames) {
                $present = $presentNames -contains $name
                $kind = if ($present) { $linkKey.GetValueKind($name).ToString() } else { $null }
                # Numeric values suffice for diagnostics; do not emit arbitrary strings/binary data.
                $value = if ($present -and $kind -in @('DWord', 'QWord')) { $linkKey.GetValue($name) } else { $null }
                [pscustomobject]@{ Name = $name; Present = $present; Kind = $kind; Value = $value }
            }
            [pscustomobject]@{
                Hive = $hive.ToString(); View = $view.ToString(); Path = $registryPath
                KeyExists = $null -ne $linkKey; Values = @($values); Error = $null
            }
        }
        catch {
            [pscustomobject]@{
                Hive = $hive.ToString(); View = $view.ToString(); Path = $registryPath
                KeyExists = $null; Values = @(); Error = $_.Exception.Message
            }
        }
        finally {
            if ($null -ne $linkKey) { $linkKey.Dispose() }
            if ($null -ne $baseKey) { $baseKey.Dispose() }
        }
    }
}
