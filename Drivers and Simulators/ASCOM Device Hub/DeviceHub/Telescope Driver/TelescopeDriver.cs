//tabs=4
// --------------------------------------------------------------------------------
//
// ASCOM Telescope driver for DeviceHub
//
// Description:	This class implements the Telescope object that is exposed by the
//				ASCOM Device Hub application. The properties get values that are
//				cached by the Device Hub Telescope Manager and set values that update the
//				cache and forward them to the true driver. Dome command methods
//				make calls through the Dome Manager
//
// Implements:	ASCOM Telescope interface version: 3
// Author:		(RDB) Rick Burke <astroman133@gmail.com>
//
// Edit Log:
//
// Date			Who	Vers	Description
// -----------	---	-----	-------------------------------------------------------
// 11-Dec-2018	RDB	6.4.0	Initial edit, created from ASCOM driver template
// --------------------------------------------------------------------------------
//

using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using ASCOM.DeviceInterface;
using ASCOM.Utilities;

using static System.FormattableString;

namespace ASCOM.DeviceHub
{
	//
	// Your driver's DeviceID is ASCOM.DeviceHub.Telescope
	//
	// The Guid attribute sets the CLSID for ASCOM.DeviceHub.Telescope
	// The ClassInterface/None attribute prevents an empty interface called
	// _DeviceHub from being created and used as the [default] interface
	//

	/// <summary>
	/// ASCOM Telescope Driver for DeviceHub.
	/// </summary>
	[ComVisible(true)]
	[Guid( "ad3ff473-56b5-4e04-af93-185d9cb42395" )]
	[ProgId( "ASCOM.DeviceHub.Telescope" )]
	[ServedClassName( "Device Hub Telescope" )]
	[ClassInterface( ClassInterfaceType.None )]
	public class Telescope : DeviceDriverBase, ITelescopeV3
	{
		#region Private Fields and Properties

		private TelescopeManager TelescopeManager => TelescopeManager.Instance;

		/// <summary>
		/// Returns true if there is a valid connection to the driver hardware
		/// </summary>
		private bool ConnectedState { get; set; }

		private Util Utilities { get; set; }

		#endregion Private Fields and  Properties

		#region Instance Constructor 

		/// <summary>
		/// Initializes a new instance of the <see cref="DeviceHub"/> class.
		/// Must be public for COM registration.
		/// </summary>
		public Telescope()
		{
			_driverID = Marshal.GenerateProgIdForType( this.GetType() );
			_driverDescription = GetDriverDescriptionFromAttribute();

			_logger = new TraceLogger( "", "DeviceHubTelescope" );
			ReadProfile(); // Read device configuration from the ASCOM Profile store

			LogMessage( "Telescope", "Starting initialization" );

			ConnectedState = false; // Initialize connected to false
			Utilities = new Util(); //Initialize util object

			// We need this to get the telescope ID.

			AppSettingsManager.LoadAppSettings();

			LogMessage( "Telescope", "Completed initialization" );
		}

		#endregion Instance Constructor 

		//
		// PUBLIC COM INTERFACE ITelescopeV3 IMPLEMENTATION
		//

		#region Common properties and methods.

		/// <summary>
		/// Displays the Setup Dialog form.
		/// If the user clicks the OK button to dismiss the form, then
		/// the new settings are saved, otherwise the old values are reloaded.
		/// THIS IS THE ONLY PLACE WHERE SHOWING USER INTERFACE IS ALLOWED!
		/// </summary>
		public void SetupDialog()
		{
			if ( Server.ScopesInUse > 1 || TelescopeManager.Instance.IsConnected )
			{
				// If ScopesInUse is greater than 1 then there are other clients and we cannot change the driver properties.
				// If The TelescopeManager is connected then we cannot change the driver properties.

				System.Windows.MessageBox.Show( "Unable to change Telescope Properties at this time." );

				return;
			}

			if ( ConnectedState )
			{
				System.Windows.MessageBox.Show( "Already connected, just press OK" );

				return;
			}

			// Launch the setup dialog on the U/I thread.

			Task taskShow = new Task( () =>
			{
				DriverSetupDialogViewModel vm = new DriverSetupDialogViewModel();
				DriverSetupDialogView view = new DriverSetupDialogView { DataContext = vm };
				vm.RequestClose += view.OnRequestClose;
				vm.InitializeCurrentTelescope( TelescopeManager.TelescopeID, TelescopeManager.Instance.FastPollingPeriod );
				vm.IsLoggingEnabled = _logger.Enabled;
				view.ContentRendered += ( sender, eventArgs ) => view.Activate();

				bool? result = view.ShowDialog();

				if ( result.HasValue && result.Value )
				{
					_logger.Enabled = vm.IsLoggingEnabled;
					string telescopeID = vm.TelescopeSetupVm.TelescopeID;
					TelescopeManager.TelescopeID = telescopeID;
					TelescopeManager.Instance.FastPollingPeriod = vm.TelescopeSetupVm.FastUpdatePeriod;
					SaveProfile();

					view.DataContext = null;
					view = null;

					vm.Dispose();
					vm = null;
				}
			} );

			taskShow.Start( Globals.UISyncContext );

			// Wait for the task to be completed and the profile to be updated.

			taskShow.Wait();
		}

		public ArrayList SupportedActions
		{
			get
			{
				ArrayList retval;
				string msg = "";

				try
				{
					retval = TelescopeManager.SupportedActions;
					msg += $"Returning list from driver {_done}";
				}
				catch (Exception)
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get SupportedActions:", msg );
				}

				return retval;
			}
		}

		public string Action( string actionName, string actionParameters )
		{
			string retval;

			if ( String.IsNullOrEmpty( actionName ))
			{
				throw new InvalidValueException( "Action method: no actionName was provided." );
			}

			string msg = $"Action {actionName}, parameters {actionParameters}";

			try
			{
				retval = TelescopeManager.Action( actionName, actionParameters );
				msg += $", returned {retval}{_done}";
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( "Action:", msg );
			}

			return retval;
		}

		public void CommandBlind( string command, bool raw )
		{
			string msg = $"Command {command}, Raw {raw}";

			try
			{
				TelescopeManager.CommandBlind( command, raw );
				msg += _done;
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( "CommandBlind:", msg );
			}
		}

		public bool CommandBool( string command, bool raw )
		{
			bool retval;
			string msg = $"Command {command}, Raw {raw}";

			try
			{
				retval = TelescopeManager.CommandBool( command, raw );
				msg += $", Returned {retval}{_done}.";
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( "CommandBool:", msg );
			}

			return retval;
		}

		public string CommandString( string command, bool raw )
		{
			string retval;
			string msg = $"Command {command}, Raw {raw}";

			try
			{
				retval = TelescopeManager.CommandString( command, raw );
				msg += $", Returned {retval}{_done}.";
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( "CommandString:", msg );
			}

			return retval;
		}

		public void Dispose()
		{
			// Clean up the tracelogger and util objects

			_logger.Enabled = false;
			_logger.Dispose();
			_logger = null;
			Utilities.Dispose();
			Utilities = null;
		}

		public bool Connected
		{
			get
			{
				LogMessage( "Get Connected:", ConnectedState.ToString() );

				return ConnectedState;
			}
			set
			{
				string msg = $"Setting Connected to {value}";

				try
				{
					if ( value == ConnectedState ) // Short circuit since we are already connected.
					{
						msg += " (no change)";

						return;
					}

					if ( value )
					{
						msg += $" (connecting to {TelescopeManager.TelescopeID})";

						// Do this on the U/I thread.

						Task task = new Task(() =>
						{
							ConnectedState = TelescopeManager.Connect();
						});

						task.Start( Globals.UISyncContext );
						task.Wait();
					}
					else
					{
						ConnectedState = false;
						Server.DisconnectTelescopeIf();
						msg += $" (disconnecting from {TelescopeManager.TelescopeID}){_done}";
					}
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Set Connected:", msg );
				}
			}
		}

		public string Description
		{
			get
			{
				LogMessage( "Get Description:", _driverDescription );

				return _driverDescription;
			}
		}

		public string DriverInfo
		{
			get
			{
				Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
				string driverInfo = Invariant( $"DeviceHub telescope driver. Version: {version.Major}.{version.Minor}" );

				LogMessage( "Get DriverInfo:", driverInfo );

				return driverInfo;
			}
		}

		public string DriverVersion
		{
			get
			{
				Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
				string driverVersion = Invariant( $"{version.Major}.{version.Minor}" );

				LogMessage( "Get DriverVersion:", driverVersion );

				return driverVersion;
			}
		}

		public short InterfaceVersion
		{
			// Set by the driver wizard

			get
			{
				short version = 3;

				LogMessage( "Get InterfaceVersion:", version.ToString() );

				return version;
			}
		}

		public string Name
		{
			get
			{
				Assembly assembly = Assembly.GetEntryAssembly();
				var attribute = (AssemblyProductAttribute)assembly.GetCustomAttributes( typeof(AssemblyProductAttribute), false ).FirstOrDefault();
				string name = attribute.Product;

				string downstreamName = TelescopeManager.Name;

				if ( !String.IsNullOrEmpty( downstreamName ) )
				{
					name += $" -> {downstreamName}";
				}

				LogMessage( "Get Name:", name );

				return name;
			}
		}

		#endregion

		#region ITelescope Implementation

		public void AbortSlew()
		{
			string name = "AbortSlew:";

			CheckConnected( name );
			CheckParked( name, TelescopeManager.Status.AtPark );

			string msg = "";

			try
			{
				TelescopeManager.AbortSlew();

				msg = _done;
			}
			catch ( Exception )
			{
				msg = _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		public AlignmentModes AlignmentMode
		{
			get
			{
				AlignmentModes mode;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Parameters.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					mode = TelescopeManager.Parameters.AlignmentMode;
					msg = $"{mode}{_done}";
				}
				catch ( Exception )
				{
					msg = _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get AlignmentMode:", msg );
				}

				return mode;
			}
		}

		public double Altitude
		{
			get
			{
				string name = "Get Altitude:";

				CheckConnected( name );

				var retval = Double.NaN;
				string msg= "";

				try
				{
					Exception xcp = TelescopeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Status.Altitude;
					msg += $"{Utilities.DegreesToDMS( retval )}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}

				return retval;
			}
		}

		public double ApertureArea
		{
			get
			{
				var retval = Double.NaN;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Parameters.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Parameters.ApertureArea;
					msg += $"{retval:F3}{_done}";
				}
				catch (Exception)
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get ApertureArea:", msg );
				}

				return retval;
			}
		}

		public double ApertureDiameter
		{
			get
			{
				var retval = Double.NaN;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Parameters.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Parameters.ApertureDiameter;
					msg += $"{retval:F3}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get ApertureDiameter:", msg );
				}

				return retval;
			}
		}

		public bool AtHome
		{
			get
			{
				string name = "Get AtHome:";
				CheckConnected( name );

				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Status.AtHome;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}

				return retval;
			}
		}

		public bool AtPark
		{
			get
			{
				string name = "Get AtPark:";
				CheckConnected( name );

				var atPark = false;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					atPark = TelescopeManager.Status.AtPark;
					msg += $"{atPark}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}

				return atPark;
			}
		}

		public IAxisRates AxisRates( TelescopeAxes axis )
		{
			IAxisRates axisRates = null;
			string msg = axis.ToString();

			try
			{
				axisRates = new AxisRates( axis );
				msg += _done;
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( "Get AxisRates:", msg );
			}

			return axisRates;
		}

		public double Azimuth
		{
			get
			{
				string name = "Get Azimuth:";
				CheckConnected( name );

				var retval = Double.NaN;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Status.Azimuth;
					msg += $"{Utilities.DegreesToDMS( retval )}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}

				return retval;
			}
		}

		public bool CanFindHome
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Capabilities.CanFindHome;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanFindHome:", msg );
				}

				return retval;
			}
		}

		public bool CanMoveAxis( TelescopeAxes axis )
		{
			var retval = false;
			string msg = axis.ToString();

			try
			{
				switch ( axis )
				{
					case TelescopeAxes.axisPrimary:
						retval = TelescopeManager.Capabilities.CanMovePrimaryAxis;

						break;

					case TelescopeAxes.axisSecondary:
						retval = TelescopeManager.Capabilities.CanMoveSecondaryAxis;

						break;

					case TelescopeAxes.axisTertiary:
						retval = TelescopeManager.Capabilities.CanMoveTertiaryAxis;

						break;
				}

				msg += $"{retval}{_done}";
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( "Get CanMoveAxis:", msg );
			}

			return retval;
		}

		public bool CanPark
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Capabilities.CanPark;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanPark:", msg );
				}

				return retval;
			}
		}

		public bool CanPulseGuide
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Capabilities.CanPulseGuide;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanPulseGuide:", msg );
				}

				return retval;
			}
		}

		public bool CanSetDeclinationRate
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Capabilities.CanSetDeclinationRate;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanSetDeclinationRate:", msg );
				}

				return retval;
			}
		}

		public bool CanSetGuideRates
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Capabilities.CanSetGuideRates;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanSetGuideRates:", msg );
				}

				return retval;
			}
		}

		public bool CanSetPark
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Capabilities.CanSetPark;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanSetPark:", msg );
				}

				return retval;
			}
		}

		public bool CanSetPierSide
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Capabilities.CanSetPierSide;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanSetPierSide:", msg );
				}

				return retval;
			}
		}

		public bool CanSetRightAscensionRate
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Capabilities.CanSetRightAscensionRate;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanSetRightAscensionRate:", msg );
				}

				return retval;
			}
		}

		public bool CanSetTracking
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Capabilities.CanSetTracking;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanSetTracking:", msg );
				}

				return retval;
			}
		}

		public bool CanSlew
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Capabilities.CanSlew;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanSlew:", msg );
				}

				return retval;
			}
		}

		public bool CanSlewAltAz
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Capabilities.CanSlewAltAz;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanSlewAltAz:", msg );
				}

				return retval;
			}
		}

		public bool CanSlewAltAzAsync
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Capabilities.CanSlewAltAzAsync;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanSlewAltAzAsync:", msg );
				}

				return retval;
			}
		}

		public bool CanSlewAsync
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Capabilities.CanSlewAsync;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanSlewAsync:", msg );
				}

				return retval;
			}
		}

		public bool CanSync
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Capabilities.CanSync;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanSync:", msg );
				}

				return retval;
			}
		}

		public bool CanSyncAltAz
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Capabilities.CanSyncAltAz;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanSyncAltAz:", msg );
				}

				return retval;
			}
		}

		public bool CanUnpark
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Capabilities.CanUnpark;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanUnpark:", msg );
				}

				return retval;
			}
		}

		public double Declination
		{
			get
			{
				CheckConnected( "Get Declination:" );

				var retval = Double.NaN;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Status.Declination;
					msg += $"{Utilities.DegreesToDMS( retval )}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get Declination:", msg );
				}

				return retval;
			}
		}

		public double DeclinationRate
		{
			get
			{
				CheckConnected( "Get DeclinationRate:" );

				var retval = Double.NaN;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Status.DeclinationRate;
					msg += $"{retval:F1}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get DeclinationRate:", msg );
				}

				return retval;
			}
			set
			{
				CheckConnected( "Set DeclinationRate:" );

				string msg = "";

				try
				{
					TelescopeManager.DeclinationRate = value;
					msg += $"{value:F1}{_done}";
				}
				catch (Exception)
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Set DeclinationRate:", msg );
				}
			}
		}

		public PierSide DestinationSideOfPier( double rightAscension, double declination )
		{
			var retval = PierSide.pierUnknown;
			string msg = "";

			CheckConnected( "DestinationSideOfPier:" );

			try
			{
				retval = TelescopeManager.GetTargetSideOfPier( rightAscension, declination );
				msg += $"{retval}{_done}";
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( "Get DestinationSideOfPier:", msg );
			}

			return retval;
		}

		public bool DoesRefraction
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Parameters.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Parameters.DoesRefraction;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get DoesRefraction:", msg );
				}

				return retval;
			}
			set
			{
				CheckConnected( "Set DoesRefraction:" );

				string msg = "";

				try
				{
					TelescopeManager.DoesRefraction = value;
					msg += $"{value}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Set DoesRefraction:", msg );
				}
			}
		}

		public EquatorialCoordinateType EquatorialSystem
		{
			get
			{
				EquatorialCoordinateType retval;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Parameters.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Parameters.EquatorialSystem;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get EquatorialSystem:", msg );
				}

				return retval;
			}
		}

		public void FindHome()
		{
			string name = "FindHome:";

			CheckConnected( name );
			CheckCapabilityForMethod( name, "CanFindHome", TelescopeManager.Capabilities.CanFindHome );

			string msg = "";

			try
			{
				TelescopeManager.FindHome();
				msg += _done;
			}
			catch (Exception)
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		public double FocalLength
		{
			get
			{
				var retval = Double.NaN;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Parameters.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Parameters.FocalLength;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get FocalLength:", msg );
				}

				return retval;
			}
		}

		public double GuideRateDeclination
		{
			get
			{
				string name = "Set GuideRateDeclination:";
				CheckConnected( name );
				var retval = Double.NaN;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Status.GuideRateDeclination;
					msg += $"{retval:F1}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}

				return retval;
			}
			set
			{
				string name = "Set GuideRateDeclination:";
				CheckConnected( name );
				string msg = "";

				try
				{
					TelescopeManager.GuideRateDeclination = value;
					msg += $"{value:F1}{_done}";
				}
				catch (Exception)
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}
			}
		}

		public double GuideRateRightAscension
		{
			get
			{
				string name = "Get GuideRateRightAscension:";

				CheckConnected( name );

				var retval = Double.NaN;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Status.GuideRateRightAscension;
					msg += $"{retval:F1}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}

				return retval;
			}
			set
			{
				string name = "Set GuideRateRightAscension:";

				CheckConnected( name );

				string msg = "";

				try
				{
					TelescopeManager.GuideRateRightAscension = value;
					msg += $"{value:F1}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}
			}
		}

		public bool IsPulseGuiding
		{
			get
			{
				string name = "Get IsPulseGuiding:";

				CheckConnected( name );

				var retval = false;
				string msg = "";

				CheckCapabilityForProperty( name, "CanPulseGuide", TelescopeManager.Capabilities.CanPulseGuide );

				try
				{
					Exception xcp = TelescopeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Status.IsPulseGuiding;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}

				return retval;
			}
		}

		public void MoveAxis( TelescopeAxes axis, double rate )
		{
			string name = "MoveAxis:";

			CheckConnected( name );
			CheckRate( name, axis, rate );

			string msg = $"{axis} {rate:F5}";

			if ( !CanMoveAxis( axis ) )
			{
				string axisName = Enum.GetName( typeof( TelescopeAxes ), axis );
				LogMessage( name, "Cannot move {0}.", axisName );
				throw new MethodNotImplementedException( $"CanMoveAxis {axisName}" );
			}

			try
			{
				if ( rate == 0 )
				{
					TelescopeManager.StopJogMoveAxis( axis );
				}
				else
				{
					Debug.WriteLine( "MoveAxis - Checking if Parked" );
					CheckParked( name, TelescopeManager.Status.AtPark );
					Debug.WriteLine( "MoveAxis - Verifying NOT in the process of parking." );
					CheckParkingStatus( name, TelescopeManager.Status.ParkingState, ParkingStateEnum.Unparked );
					Debug.WriteLine( $"TelescopeDriver: StartJogMoveAxis - {axis} {rate:F3}" );
					TelescopeManager.StartJogMoveAxis( axis, rate );
					Debug.WriteLine( "Back from StartJogMoveAxis" );
				}

				msg += _done;
			}
			catch ( Exception xcp )
			{
				Debug.WriteLine( $"StartJogMoveAxis caught exception - {xcp.Message}" );
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		public void Park()
		{
			string name = "Park:";

			CheckConnected( name );
			CheckCapabilityForMethod( name, "CanPark", TelescopeManager.Capabilities.CanPark );

			if ( TelescopeManager.Status.AtPark )
			{
				return;
			}

			string msg = "";

			try
			{
				TelescopeManager.SetParkingState( ParkingStateEnum.IsAtPark );
				msg += _done;
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		public void PulseGuide( GuideDirections direction, int duration )
		{
			string name = "PulseGuide:";

			CheckConnected( name );
			CheckParked( name, TelescopeManager.Status.AtPark );
			CheckParkingStatus( name, TelescopeManager.Status.ParkingState, ParkingStateEnum.Unparked );
			CheckCapabilityForMethod( name, "CanPulseGuide", TelescopeManager.Capabilities.CanPulseGuide );
			CheckRange( name, duration, 0, 30000 );

			string msg = $"{direction} {duration}ms";

			try
			{
				TelescopeManager.PulseGuide( direction, duration );
				msg += _done;
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		public double RightAscension
		{
			get
			{
				string name = "Get RightAscension:";

				CheckConnected( name );

				var retval = Double.NaN;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Status.RightAscension;
					msg += $"{Utilities.HoursToHMS( retval )}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}

				return retval;
			}
		}

		public double RightAscensionRate
		{
			get
			{
				string name = "Get RightAscensionRate:";

				CheckConnected( name );

				var retval = Double.NaN;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Status.RightAscensionRate;
					msg += $"{retval:F5}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}

				return retval;
			}
			set
			{
				string name = "Set RightAscensionRate:";

				CheckConnected( name );

				string msg = value.ToString( "F5" );

				try
				{
					TelescopeManager.RightAscensionRate = value;
					msg += _done;
				}
				catch (Exception)
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}
			}
		}

		public void SetPark()
		{
			string name = "SetPark:";

			CheckConnected( name );
			CheckCapabilityForMethod( name, "CanSetPark", TelescopeManager.Capabilities.CanSetPark );

			string msg = "";

			try
			{
				TelescopeManager.SetPark();
				msg += _done;
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		public PierSide SideOfPier
		{
			get
			{
				string name = "Get SideOfPier:";

				CheckConnected( name );

				var retval = PierSide.pierUnknown;
				string msg = "";

				try
				{
					retval = TelescopeManager.Status.SideOfPier;

					Exception xcp = TelescopeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}

				return retval;
			}
			set
			{
				string msg = "";

				try
				{
					if ( value != TelescopeManager.Status.SideOfPier )
					{
						TelescopeManager.StartMeridianFlip();
						msg = $"{value} {_done}";
					}
					else
					{
						msg = $"No change {_done}";
					}

				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Set SideOfPier: ", msg );
				}
			}
		}

		public double SiderealTime
		{
			get
			{
				double siderealTime = 0.0;
				string msg = "";

				try
				{
					// I chose to return the telescope's sidereal time rather than the PC's sidereal time.
					// However, I kept the template code below that shows how to use NOVAS 3.1 to calculate it.

					//// Use NOVAS 3.1 to get the GreenwichApparentSiderealTime (GAST).

					//using ( var novas = new ASCOM.Astrometry.NOVAS.NOVAS31() )
					//{
					//	var jd = Utilities.DateUTCToJulian( DateTime.UtcNow );
					//	novas.SiderealTime( jd, 0, novas.DeltaT( jd ),
					//		ASCOM.Astrometry.GstType.GreenwichApparentSiderealTime,
					//		ASCOM.Astrometry.Method.EquinoxBased,
					//		ASCOM.Astrometry.Accuracy.Reduced, ref siderealTime );
					//}

					//// Adjust for the longitude.

					//siderealTime += SiteLongitude / 360.0 * 24.0;

					//// Normalize the time to the range 0 to 24 hours.

					//siderealTime += 24.0; // Make sure it is not negative.
					//siderealTime = siderealTime % 24.0;

					Exception xcp = TelescopeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					siderealTime = TelescopeManager.Status.SiderealTime;
					msg += $"{siderealTime:F5}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get SiderealTime:", msg );
				}

				return siderealTime;
			}
		}

		public double SiteElevation
		{
			get
			{
				var retval = Double.NaN;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Parameters.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Parameters.SiteElevation;
					msg += $"{retval:F3}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get SiteElevation:", msg );
				}

				return retval;
			}
			set
			{
				string msg = value.ToString( "F0" );

				try
				{
					TelescopeManager.SiteElevation = value;
					msg += _done;
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Set SiteElevation:", msg );
				}
			}
		}

		public double SiteLatitude
		{
			get
			{
				var retval = Double.NaN;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Parameters.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Parameters.SiteLatitude;
					msg += $"{retval:f5}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get SiteLatitude:", msg );
				}

				return retval;
			}
			set
			{
				string msg = value.ToString( "F0" );

				try
				{
					TelescopeManager.SiteLatitude = value;
					msg += _done;
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Set SiteLatitude:", msg );
				}
			}
		}

		public double SiteLongitude
		{
			get
			{
				var retval = Double.NaN;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Parameters.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Parameters.SiteLongitude;
					msg += $"{retval:f5}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get SiteLongitude:", msg );
				}

				return retval;
			}
			set
			{
				string msg = value.ToString( "F0" );

				try
				{
					TelescopeManager.SiteLongitude = value;
					msg += _done;
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Set SiteLongitude:", msg );
				}
			}
		}

		public short SlewSettleTime
		{
			get
			{
				short retval = 0;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Parameters.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Parameters.SlewSettleTime;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get SlewSettleTime:", msg );
				}

				return retval;
			}
			set
			{
				string msg = value.ToString();

				try
				{
					TelescopeManager.SlewSettleTime = value;
					msg += _done;
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Set SlewSettleTime:", msg );
				}
			}
		}

		public void SlewToAltAz( double azimuth, double altitude )
		{
			string name = "SlewToAltAz:";
			CheckCapabilityForMethod( name, "CanSlewAltAz", TelescopeManager.Capabilities.CanSlewAltAz );
			CheckParked( name, TelescopeManager.Status.AtPark );
			CheckParkingStatus( name, TelescopeManager.Status.ParkingState, ParkingStateEnum.Unparked );
			CheckTracking( name, false );
			CheckRange( name, azimuth, 0, 360 );
			CheckRange( name, altitude, -90, 90 );

			string msg = $"Altitude = {altitude:F5}, Azimuth = {azimuth:F5}";

			try
			{
				TelescopeManager.DoSlewToAltAz( azimuth, altitude );
				msg += _done;
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		public void SlewToAltAzAsync( double azimuth, double altitude )
		{
			string name = "SlewToAltAzAsync:";
			CheckCapabilityForMethod( name, "CanSlewAltAzAzsync", TelescopeManager.Capabilities.CanSlewAltAzAsync );
			CheckParked( name, TelescopeManager.Status.AtPark );
			CheckParkingStatus( name, TelescopeManager.Status.ParkingState, ParkingStateEnum.Unparked );
			CheckTracking( name, false );
			CheckRange( name, azimuth, 0, 360 );
			CheckRange( name, altitude, -90, 90 );

			string msg = $"Altitude = {altitude:f5}, Azimuth = {azimuth:f5}";

			try
			{
				TelescopeManager.BeginSlewToAltAzAsync( azimuth, altitude );
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		public void SlewToCoordinates( double rightAscension, double declination )
		{
			string name = "SlewToCoordinates:";
			CheckCapabilityForMethod( name, "CanSlew", TelescopeManager.Capabilities.CanSlew );
			CheckRange( name, rightAscension, 0.0, 24.0 );
			CheckRange( name, declination, -90.0, 90.0 );
			CheckParked( name, TelescopeManager.Status.AtPark );
			CheckParkingStatus( name, TelescopeManager.Status.ParkingState, ParkingStateEnum.Unparked );
			CheckTracking( name, true );

			string msg = "";
			try
			{
				msg += $"RightAscension = {rightAscension:F5}, Declination = {declination:F5}";

				TelescopeManager.DoSlewToCoordinates( rightAscension, declination );
				msg += _done;
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		public void SlewToCoordinatesAsync( double rightAscension, double declination )
		{
			string name = "SlewToCoordinatesAsync:";
			CheckCapabilityForMethod( name, "CanSlew", TelescopeManager.Capabilities.CanSlew );
			CheckCapabilityForMethod( name, "CanSlewAsync", TelescopeManager.Capabilities.CanSlewAsync );
			CheckRange( name, rightAscension, 0.0, 24.0 );
			CheckRange( name, declination, -90.0, 90.0 );
			CheckParked( name, TelescopeManager.Status.AtPark );
			CheckParkingStatus( name, TelescopeManager.Status.ParkingState, ParkingStateEnum.Unparked );
			CheckTracking( name, true );

			string msg = $"RightAscension = {rightAscension:f5}, Declination = {declination:f5}";

			try
			{
				TelescopeManager.BeginSlewToCoordinatesAsync( rightAscension, declination );
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		public void SlewToTarget()
		{
			string name = "SlewToTarget:";
			CheckCapabilityForMethod( name, "CanSlew", TelescopeManager.Capabilities.CanSlew );
			CheckRange( name, TelescopeManager.TargetRightAscension, 0.0, 24.0 );
			CheckRange( name, TelescopeManager.TargetDeclination, -90.0, 90.0 );
			CheckParked( name, TelescopeManager.Status.AtPark );
			CheckParkingStatus( name, TelescopeManager.Status.ParkingState, ParkingStateEnum.Unparked );
			CheckTracking( name, true );

			string msg = "";

			try
			{
				TelescopeManager.DoSlewToTarget();
				msg += _done;
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		public void SlewToTargetAsync()
		{
			string name = "SlewToTargetAsync:";
			CheckCapabilityForMethod( name, "CanSlew", TelescopeManager.Capabilities.CanSlew );
			CheckCapabilityForMethod( name, "CanSlewAsync", TelescopeManager.Capabilities.CanSlew );
			CheckRange( name, TelescopeManager.TargetRightAscension, 0.0, 24.0 );
			CheckRange( name, TelescopeManager.TargetDeclination, -90.0, 90.0 );
			CheckParked( name, TelescopeManager.Status.AtPark );
			CheckParkingStatus( name, TelescopeManager.Status.ParkingState, ParkingStateEnum.Unparked );
			CheckTracking( name, true );

			string msg = "";

			try
			{
				TelescopeManager.BeginSlewToTargetAsync();
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		public bool Slewing
		{
			get
			{
				string name = "Get Slewing:";

				CheckConnected( name );

				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Status.Slewing;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}

				return retval;
			}
		}

		public void SyncToAltAz( double azimuth, double altitude )
		{
			string name = "SyncToAltAz:";
			CheckCapabilityForMethod( name, "CanSyncAltAz", TelescopeManager.Capabilities.CanSyncAltAz );
			CheckRange( name, azimuth, 0.0, 360.0 );
			CheckRange( name, altitude, -90.0, 90.0 );
			CheckParked( name, TelescopeManager.Status.AtPark );
			CheckParkingStatus( name, TelescopeManager.Status.ParkingState, ParkingStateEnum.Unparked );
			CheckTracking( name, false );

			string msg = $"Altitude = {altitude:f5}, Azimuth = {azimuth:f5}";

			try
			{
				TelescopeManager.SyncToAltAz( azimuth, altitude );
				msg += _done;
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		public void SyncToCoordinates( double rightAscension, double declination )
		{
			string name = "SyncToCoordinates:";
			CheckCapabilityForMethod( name, "CanSync", TelescopeManager.Capabilities.CanSync );
			CheckRange( name, rightAscension, 0.0, 24.0 );
			CheckRange( name, declination, -90.0, 90.0 );
			CheckParked( name, TelescopeManager.Status.AtPark );
			CheckParkingStatus( name, TelescopeManager.Status.ParkingState, ParkingStateEnum.Unparked );
			CheckTracking( name, true );

			string msg = $"RightAscension = {rightAscension:f5}, Declination = {declination:f5}";

			try
			{
				TelescopeManager.SyncToCoordinates( rightAscension, declination );
				msg += _done;
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		public void SyncToTarget()
		{
			string name = "SyncToTarget:";
			CheckCapabilityForMethod( name, "CanSync", TelescopeManager.Capabilities.CanSync );
			CheckRange( name, TelescopeManager.Status.TargetRightAscension, 0.0, 24.0 );
			CheckRange( name, TelescopeManager.Status.TargetDeclination, -90.0, 90.0 );
			CheckParked( name, TelescopeManager.Status.AtPark );
			CheckParkingStatus( name, TelescopeManager.Status.ParkingState, ParkingStateEnum.Unparked );
			CheckTracking( name, true );

			string msg = "";

			try
			{
				TelescopeManager.SyncToTarget();
				msg += _done;
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		public double TargetDeclination
		{
			get
			{
				double retval = Double.NaN;

				string name = "Get TargetDeclination:";
				string msg = String.Empty;

				CheckCapabilityForProperty( name, "CanSlew", TelescopeManager.Capabilities.CanSlew );

				try
				{
					Exception xcp = TelescopeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Status.TargetDeclination;

					CheckRange( name, retval, -90.0, 90.0 );

					msg = Utilities.DegreesToDMS( retval );
				}
				catch ( Exception ex )
				{
					msg += $"{_failed} -- {ex.Message}";

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}

				return retval;
			}
			set
			{
				string name = "Set TargetDeclination:";
				CheckCapabilityForProperty( name, "CanSlew", TelescopeManager.Capabilities.CanSlew );
				CheckRange( name, value, -90.0, +90.0 );

				string msg = Utilities.DegreesToDMS( value );

				try
				{
					TelescopeManager.SetTargetDeclination( value );
					msg += _done;
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}
			}
		}

		public double TargetRightAscension
		{
			get
			{
				double retval = Double.NaN;

				string name = "Get TargetRightAscension:";
				CheckCapabilityForProperty( name, "CanSlew", TelescopeManager.Capabilities.CanSlew );

				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Status.TargetRightAscension;

					CheckRange( name, retval, 0.0, 24.0 );

					msg += Utilities.DegreesToDMS( retval );
				}
				catch ( Exception ex )
				{
					msg += $"{_failed} -- {ex.Message}";

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}

				return retval;
			}
			set
			{
				string name = "Set TargetRightAscension:";
				CheckCapabilityForProperty( name, "CanSlew", TelescopeManager.Capabilities.CanSlew );
				CheckRange( name, value, 0.0, 24.0 );

				string msg = Utilities.DegreesToDMS( value );

				try
				{
					TelescopeManager.SetTargetRightAscension( value );
					msg += _done;
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}
			}
		}
	
		public bool Tracking
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Status.Tracking;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get Tracking:", msg );
				}

				return retval;
			}
			set
			{
				string msg = value.ToString();

				try
				{
					TelescopeManager.Tracking = value;
					msg += _done;
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Set Tracking:", msg );
				}
			}
		}

		public DriveRates TrackingRate
		{
			get
			{
				DriveRates retval;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Status.TrackingRate;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get TrackingRate:", msg );
				}

				return retval;
			}
			set
			{
				string msg = value.ToString();

				try
				{
					TelescopeManager.TrackingRate = value;
					msg += _done;
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Set TrackingRate:", msg );
				}
			}
		}

		public ITrackingRates TrackingRates
		{
			get
			{
				ITrackingRates retval;
				StringBuilder msg = new StringBuilder();

				try
				{
					retval = new TrackingRates();

					foreach ( DriveRates rate in retval )
					{
						if ( msg.Length > 0 )
						{
							msg.Append( ", " );
						}

						msg.Append( rate.ToString() );
					}
				}
				catch ( Exception )
				{
					msg.Append( _failed );

					throw;
				}
				finally
				{
					LogMessage( "Get TrackingRates:", msg.ToString() );
				}

				return retval;
			}
		}

		public DateTime UTCDate
		{
			get
			{
				DateTime retval;
				string msg = "";

				try
				{
					Exception xcp = TelescopeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = TelescopeManager.Status.UTCDate;
					msg += $"{retval:MM/dd/yy HH:mm:ss}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get UTCDate:", msg );
				}

				return retval;
			}
			set
			{
				string msg = value.ToString( "MM/dd/yy HH:mm:ss" );

				try
				{
					TelescopeManager.UTCDate = value;
					msg += _done;
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Set UTCDate:", msg );
				}
			}
		}

		public void Unpark()
		{
			string name = "Unpark:";

			string msg = "";

			CheckCapabilityForMethod( name, "CanUnpark", TelescopeManager.Capabilities.CanUnpark );

			try
			{
				TelescopeManager.SetParkingState( ParkingStateEnum.Unparked );
				msg += _done;
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		#endregion

		#region Private Methods

		private void CheckConnected( string identifier )
		{
			CheckConnected( identifier, ConnectedState );
		}

		private void CheckRate( string ident, TelescopeAxes axis, double rate )
		{
			if ( rate == 0.0 )
			{
				return;
			}

			IRate[] rates = null;

			switch ( axis )
			{
				case TelescopeAxes.axisPrimary:

					rates = TelescopeManager.Capabilities.PrimaryAxisRates;
					break;

				case TelescopeAxes.axisSecondary:
					rates = TelescopeManager.Capabilities.SecondaryAxisRates;
					break;

				case TelescopeAxes.axisTertiary:
					rates = TelescopeManager.Capabilities.TertiaryAxisRates;
					break;

			}

			StringBuilder sb = new StringBuilder();

			foreach ( IRate rateItem in rates )
			{
				if ( Math.Abs( rate ) >= rateItem.Minimum && Math.Abs( rate ) <= rateItem.Maximum )
				{
					return;
				}

				if ( sb.Length > 0 )
				{
					sb.Append( ", " );
				}

				sb.Append( $"{rateItem.Minimum:f5} to {rateItem.Maximum:f5}" );
			}

			throw new InvalidValueException( ident, rate.ToString( CultureInfo.InvariantCulture ), sb.ToString() );
		}

		private void CheckTracking( string ident,  bool raDecSlew )
		{
			bool tracking = TelescopeManager.Status.Tracking;

			if ( raDecSlew != tracking )
			{
				LogMessage( ident, "{0} not possible when tracking is {1}", ident, tracking );

				throw new InvalidOperationException( $"{ident} is not allowed when tracking is {tracking}" );
			}
		}

		/// <summary>
		/// Helper method to read the driver description from the custom attribute.
		/// </summary>
		/// <returns>the display name for the class</returns>
		private string GetDriverDescriptionFromAttribute()
		{
			string retval = "Unknown driver";

			Type t = this.GetType(); ;
			Type attributeType = typeof( ServedClassNameAttribute );
			object[] customAtts = t.GetCustomAttributes( attributeType, false );

			if ( customAtts != null && customAtts.Length == 1 )
			{
				if ( customAtts[0] is ServedClassNameAttribute attr )
				{
					retval = attr.DisplayName;
				}
			}

			return retval;
		}

		/// <summary>
		/// Read the device configuration from the ASCOM Profile store
		/// </summary>
		internal void ReadProfile()
		{
			TelescopeSettings settings = TelescopeSettings.FromProfile();
			_logger.Enabled = settings.IsLoggingEnabled;
			TelescopeManager.TelescopeID = settings.TelescopeID;
			TelescopeManager.FastPollingPeriod = settings.FastUpdatePeriod;
		}

		/// <summary>
		/// Write the device configuration to the  ASCOM  Profile store
		/// </summary>
		internal void SaveProfile()
		{
			TelescopeSettings settings = new TelescopeSettings
			{
				TelescopeID = TelescopeManager.TelescopeID,
				FastUpdatePeriod = TelescopeManager.FastPollingPeriod,
				IsLoggingEnabled = _logger.Enabled
			};

			settings.ToProfile();
		}

		#endregion  Private Methods
	}
}
