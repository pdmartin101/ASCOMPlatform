@echo off
echo -
set current=%CD%
if "%1"=="" ( set platform=x64 ) else ( set platform=%1 ) 
if "%2"=="" ( set config=Release ) else ( set config=%2 ) 

call link.bat "Drivers and Simulators\Dome Simulator .NET" %platform% %config%
call link.bat "Drivers and Simulators\Rotator Simulator\RotatorServer" %platform% %config%
call link.bat "Drivers and Simulators\Focus Simulator 2010\FocuserSimulatorDriver" %platform% %config%
call link.bat "Drivers and Simulators\CameraSimulator.NET\CameraSimulator" %platform% %config%
call link.bat "ASCOM.Astrometry\ASCOM.Astrometry" %platform% %config%
call link.bat "ASCOM.Utilities\ASCOM.Utilities" %platform% %config%
call link.bat "Interfaces\ASCOMExceptions" %platform% %config%
call link.bat "ASCOM.DeviceInterface" %platform% %config%
call link.bat "drivers and simulators\safetymonitor simulator\" %platform% %config%
call link.bat "Drivers and Simulators\ASCOM.Simulator.Video" %platform% %config%
call link.bat "ASCOM.Utilities.Video" %platform% %config%
call link.bat "Drivers and Simulators\SwitchV2 Simulator\SwitchServer" %platform% %config%
call link.bat "Drivers and Simulators\ObservingConditions Hub\OCH Server" %platform% %config%

call link.bat "Drivers and Simulators\ObservingConditions Simulator\OC Simulator Server" %platform% %config%
call link.bat "Drivers and Simulators\OpenWeatherMap\OpenWeatherMap" %platform% %config%
call link.bat "ASCOM.DriverAccess" %platform% %config%
call link.bat "ASCOM.DriverAccess.Platform5" %platform% %config%
call link.bat "ASCOM.Utilities\ASCOM.Cache" %platform% %config%
call link.bat "ASCOM.Utilities\ASCOM Diagnostics" %platform% %config%
call link.bat "ASCOM.Utilities\AlpacaDynamicClients\Alpaca Client Device Base Classes" %platform% %config%
call link.bat "ASCOM.Attributes" %platform% %config%
call link.bat "ASCOM.Utilities\ASCOM.Newtonsoft.Json" %platform% %config%
call link.bat "ASCOM.Utilities\AlpacaDynamicClients\ASCOM.AlpacaDynamicClientManager" %platform% %config%
call link.bat "Drivers and Simulators\CoverCalibrator Simulator" %platform% %config%

rem call link.bat "Drivers and Simulators\ASCOM Device Hub\TelescopeDriver" %platform% %config%
rem call link.bat "Drivers and Simulators\ASCOM Device Hub\DomeDriver" %platform% %config%
rem call link.bat "Drivers and Simulators\ASCOM Device Hub\FocuserDriver" %platform% %config%
call link.bat "Drivers and Simulators\ASCOM Device Hub\DeviceHub" %platform% %config%
call link.bat "ASCOM.Utilities\FusionLib" %platform% %config%
call link.bat "ASCOM.Astrometry\ASCOM.Astrometry556\" %platform% %config%
call link.bat "ASCOM.SettingsProvider\ASCOM.SettingsProvider" %platform% %config%
call link.bat "ASCOM.Controls\ASCOM.Controls" %platform% %config%
call link.bat "ASCOM.Utilities\ASCOM.Utilities.556" %platform% %config%
call link.bat "ASCOM.Internal.Extensions" %platform% %config%
call link.bat "ASCOM.Utilities\MigrateProfile" %platform% %config%
call link.bat "GACInstall" %platform% %config%
call link.bat "ASCOM.DriverConnect" %platform% %config%
call link.bat "Driver Inst\InstallerGen" %platform% %config%
call link.bat "ASCOM.Utilities\Profile Explorer" %platform% %config%
call link.bat "Drivers and Simulators\FilterWheel Simulator .NET\FilterWheelDriver" %platform% %config%
call link.bat "Drivers and Simulators\Rotator Simulator\RotatorServer" %platform% %config%
call link.bat "Drivers and Simulators\SwitchV2 Simulator\SwitchServer" %platform% %config%
call link.bat "Drivers and Simulators\ObservingConditions Hub\OCH Test" %platform% %config%
call link.bat "Drivers and Simulators\ObservingConditions Simulator\OC Simulator Test" %platform% %config%
call link.bat "ASCOM.Astrometry\EarthRotationUpdate" %platform% %config%
call link.bat "ASCOM.Utilities\AlpacaDynamicClients\Alpaca Shared Resources" %platform% %config%
call link.bat "ASCOM.Utilities\AlpacaDynamicClients\Alpaca Client Local Server" %platform% %config%
call link.bat "ValidatePlatform" %platform% %config%
rem call link.bat "DriverTemplates\ASCOM.Setup.TemplateWizard" %platform% %config%


:end
cd %current%