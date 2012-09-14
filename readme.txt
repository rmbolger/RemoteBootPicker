Overview
---------------------------------------------------------------------
Remote Boot Picker is a Windows service that hosts a simple web service that provides a quick way to remotely request a reboot from Windows to Mac OS X on a dual-boot Apple system.  It also contains sample methods to query the status of the system including info about the user currently logged on to the console.


Background
---------------------------------------------------------------------
When I was working at USC, my group found itself in the position of having to support public computer labs comprised entirely of dual-boot Apple hardware running Mac OS X and Windows 7. Anyone who has had to deal with a similar situation knows how annoying it can be when you want to remotely manage one OS when the other is currently booted.  Even something as simple as determining which OS is running can be a chore when you have to wait for OS specific tools to timeout if the wrong OS is loaded.

To ease our management responsibilities, my co-worker, Armin Briegel, and I decided to write a pair of web services (one for each OS) that presented a common interface to query the status of the system and request a reboot into the alternate OS.  We already had custom code running on the Windows side that we could plug a new module into.  And it turned out to be easy enough to write a standalone web service in Python.

This project contains a simplified and standalone version of Windows module I wrote.


Technical Info
---------------------------------------------------------------------
The project targets .NET 4.0 and is written in C#.  It makes use of the following open source libraries via NuGet:
 - Cassia: http://code.google.com/p/cassia
 - log4net: http://logging.apache.org/log4net

The solution should be compatible with both Visual Studio 2010 SP1 and Visual Studio 2012. The NuGet libraries are not included in the repositories, but will be retrieved on build if you enable the feature in your copy of Visual Studio (Tools - Options - Package Manager - General).  More info can be found here:
http://docs.nuget.org/docs/workflows/using-nuget-without-committing-packages

The port that the web service listens on is configurable in the App.config file in the <system.services> section. But remember to match the port used on the Mac side, otherwise you lose a lot of the convenience of this solution.

The logging configuration is also in the App.config file. Consult the log4net documentation if you wish to modify it.


Basic Usage
---------------------------------------------------------------------
Install the service using the .NET installutil.exe utility as follows:
> installutil -i BootSwitchSvc.exe

Connect to the webservice with a browser or other utility that speaks http. The following methods are available and all return responses as JSON. The same set of methods should be available from either OS that is loaded.

http://hostname:port/bootoptions
	Returns an array of accepted boot commands for the currently running OS
http://hostname:port/boot/win
	Requests the machine boot to Windows
http://hostname:port/boot/mac
	Requests the machine boot to Mac OS X
http://hostname:port/boot/net
	Requests the machine boot from the network (only supported from Mac side)
http://hostname:port/os
	Returns the current OS code (Win or Mac)
http://hostname:port/status
	Returns an object containing useful information about the currently loaded OS such as whether a user is logged on and who it is.

If a user (other than Administrator) is currently logged on, a reboot request to Mac OS X will be accepted, but queued until the user logs off.