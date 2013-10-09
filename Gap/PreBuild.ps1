function Get-AssemblyInfoVersion($path)
{
	# Get the line containing the AssemblyVersion custom attribute
	$attr = (get-content $path | select-string "AssemblyVersion").ToString()

	# Parse the attribute to get the 3 digit version
	$s = $attr.IndexOf("`"")+1
	$e = $attr.LastIndexOf("`"")
	$version = $attr.Substring($s,$e-$s)
	return $version
}

function Update-FileVersion($path, $oldVersion, $newVersion)
{
	$rp = Resolve-Path $path
	Write-Host "Updating Version " $oldVersion "->" $newVersion " in file " (Resolve-Path $rp)

	$content = [IO.File]::ReadAllText($path)
	$content = $content -replace $oldVersion, $newVersion

	[IO.File]::WriteAllText($path,$content)

	# I don't use the below method because it screws with the file encoding.
	#(Get-Content $rp) | 
	#	Foreach-Object {$_ -replace $oldVersion, $newVersion} | 
	#	Set-Content $rp
}

try
{
	$oldVersionString = Get-AssemblyInfoVersion ".\Properties\AssemblyInfo.cs"
	$oldVersion = New-Object System.Version($oldVersionString)
	Write-Host "Old Version: " $oldVersion

	$newVersion = New-Object System.Version($oldVersion.Major, $oldVersion.Minor, $oldVersion.Build, ($oldVersion.Revision+1))
	
	Write-Host "New Version: " $newVersion

	Update-FileVersion .\Properties\AssemblyInfo.cs $oldVersion $newVersion
	Update-FileVersion .\Gap.nuspec $oldVersion $newVersion
}
catch
{
	Write-Host "Failed PreBuild: " $_
	exit 1
}