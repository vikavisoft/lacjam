
function elevate-script($path) {

    # Get the ID and security principal of the current user account
    $myWindowsID=[System.Security.Principal.WindowsIdentity]::GetCurrent()
    $myWindowsPrincipal=new-object System.Security.Principal.WindowsPrincipal($myWindowsID)
 
    # Get the security principal for the Administrator role
    $adminRole=[System.Security.Principal.WindowsBuiltInRole]::Administrator
 
    # Check to see if we are currently running "as Administrator"
    if ($myWindowsPrincipal.IsInRole($adminRole))
    {
        # We are running "as Administrator" - so change the title and background color to indicate this
        $Host.UI.RawUI.WindowTitle = $path + "(Elevated)"
        clear-host
    }
    else
    {
        # We are not running "as Administrator" - so relaunch as administrator
        Write-Host "Your are not elevated. Launching window with administrator privileges"
   
        # Create a new process object that starts PowerShell
        $newProcess = new-object System.Diagnostics.ProcessStartInfo "PowerShell";
   
        # Specify the current script path and name as a parameter
        $newProcess.Arguments = $path

        # Indicate that the process should be elevated
        $newProcess.Verb = "runas";
   
        # Start the new process
        $swallow = [System.Diagnostics.Process]::Start($newProcess);
   
        # Exit from the current, unelevated, process
        break
    }
}

elevate-script -path ($myInvocation.MyCommand.Definition + " " + $args)

#
# Local Application Installation
# --------------------
# Creates a local IIS site, hosts file entry, database, compiles the solution and opens the site in a browser.
# 

function print-usage() {
    Write-Warning "Usage:  .\ApplicationInstallation.ps1 [ConfigurationFileName]"
    Write-Warning " "
    Write-Warning "Where:"
    Write-Warning " "
    Write-Warning "    ConfigurationFileName represents a path to a ps1 file defining the database and script file configuration"
}

#Administrator privileges check
If (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(`
    [Security.Principal.WindowsBuiltInRole] "Administrator"))
{
    Write-Warning "You do not have Administrator rights to run this script!`nPlease re-run this script as an Administrator!"
    exit
}

# Load the configuration file
$configurationFile = $args[0];

#if ($configurationFile -eq $null)
#{
#    print-usage
#    Write-Host "Press any key to continue ..."
#    $x = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
#
#    exit
#}

$scriptPath = $MyInvocation.MyCommand.Path
$scriptDirectory = Split-Path $scriptPath
. (Resolve-Path (Join-Path ($scriptDirectory) $configurationFile))

# Adds an IP and hostname mapping to the given hosts file
function add-host([string]$filename, [string]$ip, [string]$hostname) {
    Write-Host "Adding hosts file entry:" $ip `t`t $hostName 	
    remove-host $filename $hostname
	$ip + "`t`t" + $hostname | Out-File -encoding ASCII -append $filename
}

# Removes a specified hostname from the given hosts file
function remove-host([string]$filename, [string]$hostname) {
	$c = Get-Content $filename
	$newLines = @()

	foreach ($line in $c) {
		$bits = [regex]::Split($line, "\t+")
		if ($bits.count -eq 2) {
			if ($bits[1] -ne $hostName) {
				$newLines += $line
			}
		} else {
			$newLines += $line
		}
	}

	# Write file
	Clear-Content $filename
	foreach ($line in $newLines) {
		$line | Out-File -encoding ASCII -append $filename
	}
}

# Creates an application pool with the given name
function create-app-pool($appPool)
{    
    Write-Host Creating application pool $appPool
	remove-item -force IIS:\AppPools\$appPool -Recurse -ErrorAction SilentlyContinue
    new-item -force IIS:\AppPools\$appPool    
    $demoPool = Get-Item IIS:\AppPools\$appPool
    $demoPool.processModel.identityType = "ApplicationPoolIdentity"
    $demoPool.ManagedRuntimeVersion = "v4.0"
    $demoPool | Set-Item
}

# Creates an IIS site with HTTP port 80 binding, for the given hostname, application pool and physical path.
function create-site($domain,$apppoolname,$physicalpath)
{    
    write-host Creating site for $domain at $physicalpath
    $bindings= @{protocol="http";bindingInformation="*:80:$domain"}
    $bindings = $bindings, @{protocol="http";bindingInformation="*:80:www.$domain"}

    # Workaround for new-item iis issue when no sites exists
    $id = (dir iis:\sites | foreach {$_.id} | sort -Descending | select -first 1) + 1
    
    # Create the IIS site, and bind to the domain name and the www domain name
    New-Item "iis:\Sites\$domain" -bindings @{protocol="http";bindingInformation="*:80:$domain"} -ID $id -physicalPath "$physicalpath" -force
    
    # Link the IIS site to the new application pool
    Set-ItemProperty "IIS:\Sites\$domain" -name applicationPool -value "$apppoolname"
    
}
function create-virtual-site($domain,$alias,$apppoolname,$physicalpath)
{    
    write-host Creating virtual site for $alias inside $domain at $physicalpath
        
    # Create the IIS site as an alias/virtual application inside the existing site
    New-Item "iis:\Sites\$domain\$alias" -type Application -physicalPath "$physicalpath" -force
    
    # Link the IIS site to the new application pool
    Set-ItemProperty "IIS:\Sites\$domain\$alias" -name applicationPool -value "$apppoolname"
    
}



# Creates the web server, using values from the loaded configuration file
function create-webserver($appHostName, $appPoolName, $webServerPath){
    Import-Module WebAdministration
    try {
        create-app-pool -appPool $appPoolName
        
    }
    catch {
    	"Create application pool error: $_"
    }
    
    try {
        create-site -domain $appHostName -apppoolname $appPoolName -physicalpath $webServerPath
        
    }
    catch {
    	"Create site: $_"
    } 
}

# Creates the api server, using values from the loaded configuration file
function create-apiserver($apiHostName, $apiAppAlias, $apiAppPoolName, $apiPath){
    Import-Module WebAdministration
    try {
        create-app-pool -appPool $apiAppPoolName
        
    }
    catch {
    	"Create application pool error: $_"
    }
    
    try {
        create-virtual-site -domain $apiHostName -alias $apiAppAlias -apppoolname $apiAppPoolName -physicalpath $apiPath
        
    }
    catch {
    	"Create site: $_"
    } 
}


# Starts the newly created IIS web site
function start-iis-website($appHostName) {
    Write-Host "Starting Web Site..."
    Start-Website $appHostName
}

# Compiles the solution
function compile-solution() {
    Write-Host Compiling solution...
    
    $si = new-object System.Diagnostics.ProcessStartInfo
    $si.FileName = $msBuild
    $si.Arguments = "$config_slnFile"
    $si.RedirectStandardOutput = $true
    $si.UseShellExecute = $false
    $si.CreateNoWindow = $true
    $si.WorkingDirectory = $scriptDirectory
    
    $process = [diagnostics.process]::Start($si)
    $process.StandardOutput.ReadToEnd()
    $process.WaitForExit()
#    
#    if ($process.ExitCode -ne 0)
#    {
#        Write-Error "Compilation failed"
#        Write-Host "Press any key to continue ..."
#        $x = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
#
#        break
#    }

}

function replay-events(){

	Write-Host "Replaying events"
    
	$si = new-object System.Diagnostics.ProcessStartInfo
    $si.WorkingDirectory = $config_rootPath #.parent.FullName
	$si.FileName = "Replay Events.bat"
	
	$process = [diagnostics.process]::Start($si)
	$process.WaitForExit()
}

# First, compile the solution so that it's ready to run!
compile-solution

# Add a host entry for the web server
add-host -filename $hostsFile -ip $config_iisIpAddress -hostname $config_applicationHostName

# Create the web server in IIS
create-webserver -appHostName $config_applicationHostName -appPoolName $config_AppPoolName -webServerPath $config_webServerPath

# Create the web server for the API in IIS
create-apiserver -apiHostName $config_applicationHostName -apiAppAlias $config_applicationApiName -apiAppPoolName $config_ApiAppPoolName -apiPath $config_apiPath

# Create the local database

$scriptPath = (Get-Location)
$path = [IO.Path]::GetDirectoryName($scriptPath)
Write-Host $path
$bulk  =    "$path\Lacjam\Database Recreate.bat"
Write-Host $bulk
start-process ($bulk)

# Replay events
#replay-events


# Start the IIS web site
start-iis-website -appHostName $config_applicationHostName

Write-Host You have been kickstarted!

# Open the web site in the default browser. Voila!
explorer http://$config_applicationHostName 
#
#Write-Host "Press any key to continue ..."
#$x = $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
#