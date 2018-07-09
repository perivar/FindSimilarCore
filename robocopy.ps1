<#
Purpose:
Uses robocopy to migrate one source file area to a destination file area.
Input list in CSV format that can handle multiple sources and destination.
Logic for handling logging in 3 different phases, the first run, synchronization runs and the last final run.

To USE:
Run powershell or powershell-ISE as administrator (for backup rights).
Edit robocopylocations.csv and fill in a friendly name (for logfiles), sourcedirectory and destinationdirectory

Example robocopylocations.csv for moving two folders:
Name,SourceDirectory,DestinationDirectory
a,\\test\c$\temp\copytest\source\a,C:\temp\copytest\dest\a
b,C:\temp\copytest\source\b,C:\temp\copytest\dest\b

Run the script.

When doing the final move append -Final to the script to have it log to a seperate logfile
Append -Security to also copy security information (important, freeze changes after inital sync as additional syncs require special time consuming /secfix to fix existing)


Details:
First time it encounters a new folder it will log to <friendly-name>-FirstRun.txt
If its run multiple times after that it will log to <friendly-name>-Sync.txt, appending all runs.

When you are ready to do the actual move, make sure to take a registry backup of share permissions
make sure the actual source is not writeable (change share permissions to readonly).
The run the script with -final behind.

There is a check if the -Final logfile is already existing to prevent running more runs after final is done,
and if it is it refuses to copy that folder again. To override behaviour rename or delete logfile.
#>
[CmdletBinding()]
param(
[parameter()]
[string]$LogPath=".\log",
[parameter()]
[switch]$Final,
[parameter()]
[switch]$Security
)

if ($final){
	write-verbose "Final is true"
}
if ($security){
	write-verbose "Security is true"
}

$copylocations = Import-Csv .\robocopylocations.csv
ForEach ($copylocation in $copylocations) {
   # only include csharp files 
   $roboswitches = @("*.cs", "/mir", "/R:0", "/W:0", "/V", "/Z", "/NP")
   
   # only include same files
   $roboswitches += "/IS"
   
   # exclude folders
   # $roboswitches += "/XD exclude-fold*"

   # exclude files and prevent copying empty directories
   $roboswitches += @("/s", "/XF", "AssemblyInfo.cs")
   
   # debug /L - List only
   #$roboswitches += "/L"
    
   if ($security){
	$roboswitches += "/SEC"
   }
   $logfile = $logPath+"\"+$copylocation.name+"-Final.txt"
   if (test-path "$logfile"){
	write-output "Refusing final sync for $copylocation.name since logfile already exists. Rename log first to override"
	continue
   }
   if ($final){
 	#Performing final sync
	$roboswitches += "/LOG:$logfile"
	write-verbose "Logfile is set to $logfile and roboswitches is $roboswitches. Security: $security"
	write-verbose "Robocopying (FinaL), Source: $($copylocation.SourceDirectory) and Destination: $($copylocation.DestinationDirectory)"
	Robocopy "$($copylocation.SourceDirectory)" "$($copylocation.DestinationDirectory)" $roboswitches
   }
   else{
        $logfile = $logPath+"\"+$copylocation.name+"-FirstRun.txt"
	if (test-path "$logfile"){
		#Firstrun logfile already exist so must be sync run
		$logfile = $logPath+"\"+$copylocation.name+"-Sync.txt"
		$roboswitches += "/LOG+:$logfile"
		write-verbose "Logfile is set to $logfile and roboswitches is $roboswitches. Security: $security"
		write-verbose "Robocopying (Sync), Source: $($copylocation.SourceDirectory) and Destination: $($copylocation.DestinationDirectory)"
		Robocopy "$($copylocation.SourceDirectory)" "$($copylocation.DestinationDirectory)" $roboswitches
	}
	else{
		#Inital run
		$roboswitches += "/LOG:$logfile"
		write-verbose "Logfile is set to $logfile and roboswitches is $roboswitches. Security: $security"
		write-verbose "Robocopying (initial), Source: $($copylocation.SourceDirectory) and Destination: $($copylocation.DestinationDirectory)"
		Robocopy "$($copylocation.SourceDirectory)" "$($copylocation.DestinationDirectory)" $roboswitches
	}
   }

}