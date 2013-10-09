try
{
	Write-Host "Looking for process Gap"
	$ProcessActive = Get-Process Gap -ErrorAction SilentlyContinue
	if($ProcessActive -eq $null)
	{
		Write-host "Process not running."
		return
	}
	else
	{
		Write-host "Process running, trying to kill"
		stop-process -name Gap -force
		Write-Host "Process killed successfully"
	}
}
catch
{
	Write-Host "Failed PreDeploy: " $_
	exit 1
}