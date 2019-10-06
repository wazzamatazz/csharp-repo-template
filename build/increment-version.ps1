<#
.SYNOPSIS
Updates the version number for the solution.
.DESCRIPTION
This script updates the version number for the projects in the repository.
.PARAMETER Major
Increments the major version.
.PARAMETER Minor
Increments the minor version.
.PARAMETER Patch
Increments the patch version.
.PARAMETER PreRelease
Sets the pre-release label.
.EXAMPLE
Increment the major version and set the pre-release label to "alpha".
    increment-version.ps1 -Major -PreRelease alpha
#>
[CmdletBinding(PositionalBinding = $false, DefaultParameterSetName='Groups')]
param(
    [switch]$Major,

    [switch]$Minor,

    [switch]$Patch,

    $PreRelease
)

$VersionFilePath = "$PSScriptRoot/version.json"
$Version = Get-Content $VersionFilePath | Out-String | ConvertFrom-Json

$Modified = $false

if ($Major) {
    Write-Host('Incrementing major version.')
    $Version.Major = $Version.Major + 1
    $Modified = $true
}
if ($Minor) {
    Write-Host('Incrementing minor version.')
    $Version.Minor = $Version.Minor + 1
    $Modified = $true
}
if ($Patch) {
    Write-Host('Incrementing patch version.')
    $Version.Patch = $Version.Patch + 1
    $Modified = $true
}
if (!($null -eq $PreRelease)) {
    Write-Host('Updating pre-release label.')
    if ([String]::IsNullOrWhiteSpace($PreRelease)) {
        $Version.PreRelease = ''
    } else {
        $Version.PreRelease = $PreRelease
    }
    $Modified = $true
}

if ([String]::IsNullOrWhiteSpace($Version.PreRelease)) {
    $VersionSummary = "$($Version.Major).$($Version.Minor).$($Version.Patch)"
} else {
    $VersionSummary = "$($Version.Major).$($Version.Minor).$($Version.Patch)-$($Version.PreRelease)"
}

if ($Modified) {
    Write-Host("Saving updated version: $($VersionSummary)")
    $Version | ConvertTo-Json | Out-File $VersionFilePath
} else {
    Write-Host("Version has not been modified: $($VersionSummary)")
}