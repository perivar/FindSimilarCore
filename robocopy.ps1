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
    [string]$LogPath = ".\log",
    [parameter()]
    [switch]$Final,
    [parameter()]
    [switch]$Security
)

if ($final) {
    write-verbose "Final is true"
}
if ($security) {
    write-verbose "Security is true"
}

$copylocations = Import-Csv .\robocopylocations.csv
ForEach ($copylocation in $copylocations) {

	# https://docs.microsoft.com/en-us/windows-server/administration/windows-commands/robocopy
	# *.cs		Only include csharp files
	# /MIR 		Mirror a complete directory tree.
	# /R:<N>	Specifies the number of retries on failed copies
	# /W:<N>	Specifies the wait time between retries, in seconds
	# /V		Produces verbose output, and shows all skipped files.
	# /Z		Copies files in restartable mode.
	# /NP		Specifies that the progress of the copying operation will not be displayed.
	# /S		Copies subdirectories. Note that this option excludes empty directories.

    $roboswitches = @("*.cs", "/MIR", "/R:0", "/W:0", "/V", "/Z", "/NP", "/S")
   
	# File        Exists In   Exists In        Source/Dest     Source/Dest   Source/Dest
	# Class       Source      Destination      File Times      File Sizes    Attributes
	# =========== =========== ================ =============== ============= ============
	# Lonely      Yes         No               n/a             n/a           n/a
	# Tweaked     Yes         Yes              Equal           Equal         Different
	# Same        Yes         Yes              Equal           Equal         Equal
	# Changed     Yes         Yes              Equal           Different     n/a
	# Newer       Yes         Yes              Source > Dest   n/a           n/a
	# Older       Yes         Yes              Source < Dest   n/a           n/a
	# Extra       No          Yes              n/a             n/a           n/a
	# Mismatched  Yes (file)  Yes (directory)  n/a             n/a           n/a	

	# Switch   	Function
	# ======== 	=====================
	# /XL      	eXclude Lonely files and directories.
	# /IT      	Include Tweaked files.
	# /IS      	Include Same files.
	# /XC      	eXclude Changed files.
	# /XN      	eXclude Newer files.
	# /XO      	eXclude Older files.
	
	# Use the following switch to suppress the reporting and processing of Extra files:  
	# /XX      	eXclude eXtra files
	# $roboswitches += ""

    # /XD		Excludes directories that match the specified names and paths, e.g .XD exclude-fold*
    # $roboswitches += "/XD exclude-fold*"

    # /L		Specifies that files are to be listed only (and not copied, deleted, or time stamped).
    #$roboswitches += "/L"

	# /XF		Excludes files that match the specified names or paths. Note that FileName can include wildcard characters (* and ?).
    if ($copylocation.ExcludeFiles) {
		$roboswitches += @("/XF", "AssemblyInfo.cs", $copylocation.ExcludeFiles)
    } else {
		$roboswitches += @("/XF", "AssemblyInfo.cs")   
	}
   
    if ($security) {
        $roboswitches += "/SEC"
    }
    $logfile = $logPath + "\" + $copylocation.name + "-Final.txt"
    if (test-path "$logfile") {
        write-output "Refusing final sync for $copylocation.name since logfile already exists. Rename log first to override"
        continue
    }
    if ($final) {
        #Performing final sync
        $roboswitches += "/LOG:$logfile"
        write-verbose "Logfile is set to $logfile and roboswitches is $roboswitches. Security: $security"
        write-verbose "Robocopying (FinaL), Source: $($copylocation.SourceDirectory) and Destination: $($copylocation.DestinationDirectory)"
        Robocopy "$($copylocation.SourceDirectory)" "$($copylocation.DestinationDirectory)" $roboswitches
    }
    else {
        $logfile = $logPath + "\" + $copylocation.name + "-FirstRun.txt"
        if (test-path "$logfile") {
            #Firstrun logfile already exist so must be sync run
            $logfile = $logPath + "\" + $copylocation.name + "-Sync.txt"
            $roboswitches += "/LOG+:$logfile"
            write-verbose "Logfile is set to $logfile and roboswitches is $roboswitches. Security: $security"
            write-verbose "Robocopying (Sync), Source: $($copylocation.SourceDirectory) and Destination: $($copylocation.DestinationDirectory)"
            Robocopy "$($copylocation.SourceDirectory)" "$($copylocation.DestinationDirectory)" $roboswitches
        }
        else {
            #Inital run
            $roboswitches += "/LOG:$logfile"
            write-verbose "Logfile is set to $logfile and roboswitches is $roboswitches. Security: $security"
            write-verbose "Robocopying (initial), Source: $($copylocation.SourceDirectory) and Destination: $($copylocation.DestinationDirectory)"
            Robocopy "$($copylocation.SourceDirectory)" "$($copylocation.DestinationDirectory)" $roboswitches
        }
    }

}