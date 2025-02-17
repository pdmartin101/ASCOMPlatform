﻿Imports System.Runtime.InteropServices
Imports System.Environment
Imports ASCOM.Utilities
Imports ASCOM.Utilities.Exceptions
Imports System.Threading
Imports System.IO

Namespace NOVAS

    ''' <summary>
    ''' NOVAS31: Class presenting the contents of the USNO NOVAS 3.1 library. 
    ''' NOVAS was developed by the Astronomical Applications department of the United States Naval 
    ''' Observatory.
    ''' </summary>
    ''' <remarks>If you wish to explore or utilise NOVAS3.1 please see USNO's extensive help document "NOVAS 3.1 Users Guide" 
    ''' (NOVAS C3.1 Guide.pdf) included in the ASCOM Platform Docs start menu folder. The latest revision is also available on the USNO web site at
    ''' <href>http://www.usno.navy.mil/USNO/astronomical-applications/software-products/novas</href>
    ''' in the "C Edition of NOVAS" link. 
    ''' <para>If you use NOVAS, please send an e-mail through this page:
    ''' <href>http://www.usno.navy.mil/help/astronomy-help</href> as this provides evidence to USNO that justifies further 
    ''' improvements and developments of NOVAS capabilities.
    ''' </para>
    '''  </remarks>
    <Guid("B7203C35-B113-472D-9E5D-0602883AC835"), ClassInterface(ClassInterfaceType.None), ComVisible(True)>
    Public Class NOVAS31
        Implements INOVAS31, IDisposable

        Private Const NOVAS32DLL As String = "NOVAS31.dll" 'Names of NOVAS 32 and 64bit DLL files
        Private Const NOVAS64DLL As String = "NOVAS31-64.dll"

        Private Const JPL_EPHEM_FILE_NAME As String = "JPLEPH" 'Name of JPL ephemeredes file
        Private Const JPL_EPHEM_START_DATE As Double = 2305424.5 'First date of data in the ephemeredes file
        Private Const JPL_EPHEM_END_DATE As Double = 2525008.5 'Last date of data in the ephemeredes file

        Private Const NOVAS_DLL_LOCATION As String = "\ASCOM\Astrometry\" 'This is appended to the Common Files path
        Private Const RACIO_FILE As String = "cio_ra.bin" 'Name of the RA of CIO binary data file

        Private Const NOVAS31_MUTEX_NAME As String = "ASCOMNovas31Mutex"

        Private TL As TraceLogger
        Private Utl As Util
        Private Novas31DllHandle As IntPtr

        Private Parameters As EarthRotationParameters

#Region "New and IDisposable"
        ''' <summary>
        ''' Creates a new instance of the NOVAS31 component
        ''' </summary>
        ''' <exception cref="HelperException">Thrown if the NOVAS31 support library DLL cannot be loaded</exception>
        ''' <remarks></remarks>
        Sub New()
            Dim rc As Boolean, rc1 As Short, Novas31DllFile, RACIOFile, JPLEphFile As String, DENumber As Short
            Dim ReturnedPath As New System.Text.StringBuilder(260), LastError As Integer
            Dim Novas31Mutex As Mutex
            Dim gotMutex As Boolean ' Flag indicating whether the NOVAS initialisation mutex was successfully claimed

            TL = New TraceLogger("", "NOVAS31")
            TL.Enabled = GetBool(NOVAS_TRACE, NOVAS_TRACE_DEFAULT) 'Get enabled / disabled state from the user registry
            Novas31Mutex = New Mutex(False, NOVAS31_MUTEX_NAME) ' Create a mutex that will ensure that only one NOVAS31 initialisation can occur at a time
            JPLEphFile = ""

            Try
                TL.LogMessage("New", "Creating EarthRotationParameters object")
                Parameters = New EarthRotationParameters(TL)
                TL.LogMessage("New", "Waiting for mutex")
                gotMutex = Novas31Mutex.WaitOne(10000) ' Wait up to 10 seconds for the mutex to become available
                TL.LogMessage("New", $"Got mutex: {gotMutex}")

                Utl = New Util

                'Find the root location of the common files directory containing the ASCOM support files.
                'On a 32bit system this is \Program Files\Common Files
                'On a 64bit system this is \Program Files (x86)\Common Files
                If Is64Bit() Then ' 64bit application so find the 32bit folder location
                    rc = SHGetSpecialFolderPath(IntPtr.Zero, ReturnedPath, CSIDL_PROGRAM_FILES_COMMONX86, False)
                    Novas31DllFile = ReturnedPath.ToString & NOVAS_DLL_LOCATION & NOVAS64DLL
                    RACIOFile = ReturnedPath.ToString & NOVAS_DLL_LOCATION & RACIO_FILE
                    JPLEphFile = ReturnedPath.ToString & NOVAS_DLL_LOCATION & JPL_EPHEM_FILE_NAME
                Else '32bit application so just go with the .NET returned value
                    Novas31DllFile = GetFolderPath(SpecialFolder.CommonProgramFiles) & NOVAS_DLL_LOCATION & NOVAS32DLL
                    RACIOFile = GetFolderPath(SpecialFolder.CommonProgramFiles) & NOVAS_DLL_LOCATION & RACIO_FILE
                    JPLEphFile = GetFolderPath(SpecialFolder.CommonProgramFiles) & NOVAS_DLL_LOCATION & JPL_EPHEM_FILE_NAME
                End If

                ' Validate that the files exist
                TL.LogMessage("New", $"Path to NOVAS31 DLL: {Novas31DllFile}")
                TL.LogMessage("New", $"Path to RACIO file: {Novas31DllFile}")
                TL.LogMessage("New", $"Path to JPL ephemeris file: {Novas31DllFile}")

                If Not File.Exists(Novas31DllFile) Then
                    TL.LogMessage("New", $"NOVAS31 Initialise - Unable to locate NOVAS support DLL: {Novas31DllFile}")
                    Throw New HelperException($"NOVAS31 Initialise - Unable to locate NOVAS support DLL: {Novas31DllFile}")
                Else
                    TL.LogMessage("New", $"Found NOVAS31 DLL: {Novas31DllFile}")
                End If

                If Not File.Exists(RACIOFile) Then
                    TL.LogMessage("New", $"NOVAS31 Initialise - Unable to locate RACIO file: {Novas31DllFile}")
                    Throw New HelperException($"NOVAS31 Initialise - Unable to locate RACIO file: {RACIOFile}")
                Else
                    TL.LogMessage("New", $"Found RACIO file: {Novas31DllFile}")
                End If

                If Not File.Exists(JPLEphFile) Then
                    TL.LogMessage("New", $"NOVAS31 Initialise - Unable to locate JPL ephemeris file: {Novas31DllFile}")
                    Throw New HelperException($"NOVAS31 Initialise - Unable to locate JPL ephemeris file: {JPLEphFile}")
                Else
                    TL.LogMessage("New", $"Found  JPL ephemeris file: {Novas31DllFile}")
                End If

                TL.LogMessage("New", "Loading NOVAS31 library DLL: " + Novas31DllFile)

                Novas31DllHandle = LoadLibrary(Novas31DllFile)
                LastError = Marshal.GetLastWin32Error

                If Novas31DllHandle <> IntPtr.Zero Then ' Loaded successfully
                    TL.LogMessage("New", "Loaded NOVAS31 library OK")
                Else ' Did not load 
                    TL.LogMessage("New", $"Error loading NOVAS31 library: {LastError:X8} from {Novas31DllFile}")
                    Throw New HelperException($"NOVAS31 Initialisation - Error code {LastError:X8} returned from LoadLibrary when loading NOVAS31 library {Novas31DllFile}")
                End If

                'Establish the location of the file of CIO RAs
                SetRACIOFile(RACIOFile)

                ' Open the ephemerides file and set its applicable date range
                rc1 = Ephem_Open(JPLEphFile, JPL_EPHEM_START_DATE, JPL_EPHEM_END_DATE, DENumber)
            Catch ex As Exception
                TL.LogMessageCrLf("New", "Exception: " & ex.ToString())
                Throw New HelperException($"NOVAS31 Initialisation Exception - {ex.Message} (See inner exception for details)", ex)
            Finally
                If gotMutex Then Try : Novas31Mutex.ReleaseMutex() : Catch : End Try  ' Release the initialisation mutex if we got it in the first place
            End Try

            If rc1 > 0 Then
                TL.LogMessage("New", "Unable to open ephemeris file: " & JPLEphFile & ", RC: " & rc1)
                Throw New HelperException($"NOVAS31 Initialisation - Unable to open ephemeris file: {JPLEphFile} RC: {rc1}")
            End If

            TL.LogMessage("New", "NOVAS31 initialised OK")
        End Sub

        Private disposedValue As Boolean = False        ' To detect redundant calls

        ' IDisposable
        Protected Overridable Sub Dispose(ByVal disposing As Boolean)
            Dim Novas31Mutex As Mutex
            Novas31Mutex = Nothing

            Try
                Novas31Mutex = New Mutex(False, NOVAS31_MUTEX_NAME) ' Create a mutex that will ensure that only one NOVAS31 dispose can occur at a time
                Novas31Mutex.WaitOne(10000) ' Wait up to 10 seconds for the mutex to become available

                If Not Me.disposedValue Then
                    If disposing Then
                        ' Free other state (managed objects).

                        If Not (Parameters Is Nothing) Then
                            Try : Parameters.Dispose() : Catch : End Try
                            Try : Parameters = Nothing : Catch : End Try
                        End If

                        If Not (Utl Is Nothing) Then
                            Try : Utl.Dispose() : Catch : End Try
                            Try : Utl = Nothing : Catch : End Try
                        End If
                        If Not (TL Is Nothing) Then
                            Try : TL.Enabled = False : Catch : End Try
                            Try : TL.Dispose() : Catch : End Try
                            Try : TL = Nothing : Catch : End Try
                        End If
                    End If

                    ' Free your own state (unmanaged objects) and set large fields to null.
                    Try : FreeLibrary(Novas31DllHandle) : Catch : End Try ' Free the NOVAS library but don't return any error value
                End If
                Me.disposedValue = True
            Finally
                If Not (Novas31Mutex Is Nothing) Then Novas31Mutex.ReleaseMutex()
            End Try
        End Sub

        ' This code added by Visual Basic to correctly implement the disposable pattern.
        ''' <summary>
        ''' Cleans up the NOVAS3 object and releases its open file handle on the JPL planetary ephemeris file
        ''' </summary>
        ''' <remarks></remarks>
        Public Sub Dispose() Implements IDisposable.Dispose
            ' Do not change this code.  Put cleanup code in Dispose(ByVal disposing As Boolean) above.
            Dispose(True)
            GC.SuppressFinalize(Me)
        End Sub
#End Region

#Region "Public NOVAS Interface - Ephemeris Members"

        ''' <summary>
        ''' Get position and velocity of target with respect to the centre object. 
        ''' </summary>
        ''' <param name="Tjd"> Two-element array containing the Julian date, which may be split any way (although the first 
        ''' element is usually the "integer" part, and the second element is the "fractional" part).  Julian date is in the 
        ''' TDB or "T_eph" time scale.</param>
        ''' <param name="Target">Target object</param>
        ''' <param name="Center">Centre object</param>
        ''' <param name="Position">Position vector array of target relative to center, measured in AU.</param>
        ''' <param name="Velocity">Velocity vector array of target relative to center, measured in AU/day.</param>
        ''' <returns><pre>
        ''' 0   ...everything OK.
        ''' 1,2 ...error returned from State.</pre>
        ''' </returns>
        ''' <remarks>This function accesses the JPL planetary ephemeris to give the position and velocity of the target 
        ''' object with respect to the center object.</remarks>
        Public Function PlanetEphemeris(ByRef Tjd() As Double,
                                                ByVal Target As Target,
                                                ByVal Center As Target,
                                                ByRef Position() As Double,
                                                ByRef Velocity() As Double) As Short Implements INOVAS31.PlanetEphemeris

            Dim JdHp As New JDHighPrecision, VPos As New PosVector, VVel As New VelVector, rc As Short

            JdHp.JDPart1 = Tjd(0)
            JdHp.JDPart2 = Tjd(1)
            If Is64Bit() Then
                rc = PlanetEphemeris64(JdHp, Target, Center, VPos, VVel)
            Else
                rc = PlanetEphemeris32(JdHp, Target, Center, VPos, VVel)
            End If

            PosVecToArr(VPos, Position)
            VelVecToArr(VVel, Velocity)
            Return rc
        End Function

        ''' <summary>
        ''' Produces the Cartesian heliocentric equatorial coordinates of the asteroid for the J2000.0 epoch 
        ''' coordinate system from a set of Chebyshev polynomials read from a file.
        ''' </summary>
        ''' <param name="Mp">The number of the asteroid for which the position in desired.</param>
        ''' <param name="Name">The name of the asteroid.</param>
        ''' <param name="Jd"> The Julian date on which to find the position and velocity.</param>
        ''' <param name="Err"><pre>
        ''' = 0 ( No error )
        ''' = 1 ( Memory allocation error )
        ''' = 2 ( Mismatch between asteroid name and number )
        ''' = 3 ( Julian date out of bounds )
        ''' = 4 ( Cannot find Chebyshev polynomial file )
        ''' </pre>
        ''' </param>
        ''' <returns> 6-element array of double containing position and velocity vector values.</returns>
        ''' <remarks>The file name of the asteroid is taken from the name given.  It is	assumed that the name 
        ''' is all in lower case characters.
        ''' <para>
        ''' This routine will search in the application's current directory for a file of Chebyshev 
        ''' polynomial coefficients whose name is based on the provided Name parameter: Name.chby 
        ''' </para>
        ''' <para>Further information on using NOVAS with minor planet data is given here: 
        ''' http://www.usno.navy.mil/USNO/astronomical-applications/software-products/usnoae98</para>
        ''' </remarks>
        Public Function ReadEph(ByVal Mp As Integer,
                                        ByVal Name As String,
                                        ByVal Jd As Double,
                                        ByRef Err As Integer) As Double() Implements INOVAS31.ReadEph

            Const DOUBLE_LENGTH As Integer = 8
            Const NUM_RETURN_VALUES As Integer = 6

            Dim PosVec(NUM_RETURN_VALUES - 1) As Double
            Dim EphPtr As IntPtr
            Dim Bytes(NUM_RETURN_VALUES * DOUBLE_LENGTH) As Byte

            If Is64Bit() Then
                EphPtr = ReadEph64(Mp, Name, Jd, Err)
            Else
                EphPtr = ReadEph32(Mp, Name, Jd, Err)
            End If

            If Err = 0 Then ' Get the returned values if the call was successful
                If EphPtr <> IntPtr.Zero Then 'Only copy if the pointer is not NULL
                    ' Safely marshal unmanaged buffer to byte()
                    Marshal.Copy(EphPtr, Bytes, 0, NUM_RETURN_VALUES * DOUBLE_LENGTH)

                    ' Convert to double()
                    For i As Integer = 0 To NUM_RETURN_VALUES - 1
                        PosVec(i) = BitConverter.ToDouble(Bytes, i * DOUBLE_LENGTH)
                    Next
                Else
                    For i As Integer = 0 To NUM_RETURN_VALUES - 1
                        PosVec(i) = Double.NaN 'Return invalid values
                    Next
                End If
            End If
            Return PosVec
        End Function

        ''' <summary>
        ''' Interface between the JPL direct-access solar system ephemerides and NOVAS-C.
        ''' </summary>
        ''' <param name="Tjd">Julian date of the desired time, on the TDB time scale.</param>
        ''' <param name="Body">Body identification number for the solar system object of interest; 
        ''' Mercury = 1, ..., Pluto= 9, Sun= 10, Moon = 11.</param>
        ''' <param name="Origin">Origin code; solar system barycenter= 0, center of mass of the Sun = 1, center of Earth = 2.</param>
        ''' <param name="Pos">Position vector of 'body' at tjd; equatorial rectangular coordinates in AU referred to the ICRS.</param>
        ''' <param name="Vel">Velocity vector of 'body' at tjd; equatorial rectangular system referred to the ICRS.</param>
        ''' <returns>Always returns 0</returns>
        ''' <remarks></remarks>
        Public Function SolarSystem(ByVal Tjd As Double,
                                            ByVal Body As Body,
                                            ByVal Origin As Origin,
                                            ByRef Pos() As Double,
                                            ByRef Vel() As Double) As Short Implements INOVAS31.SolarSystem

            Dim VPos As New PosVector, VVel As New VelVector, rc As Short

            If Is64Bit() Then
                rc = SolarSystem64(Tjd, CShort(Body), CShort(Origin), VPos, VVel)
            Else
                rc = SolarSystem32(Tjd, CShort(Body), CShort(Origin), VPos, VVel)
            End If

            PosVecToArr(VPos, Pos)
            VelVecToArr(VVel, Vel)
            Return rc
        End Function

        ''' <summary>
        '''  Read and interpolate the JPL planetary ephemeris file.
        ''' </summary>
        ''' <param name="Jed">2-element Julian date (TDB) at which interpolation is wanted. Any combination of jed[0]+jed[1] which falls within the time span on the file is a permissible epoch.  See Note 1 below.</param>
        ''' <param name="Target">The requested body to get data for from the ephemeris file.</param>
        ''' <param name="TargetPos">The barycentric position vector array of the requested object, in AU. (If target object is the Moon, then the vector is geocentric.)</param>
        ''' <param name="TargetVel">The barycentric velocity vector array of the requested object, in AU/Day.</param>
        ''' <returns>
        ''' <pre>
        ''' 0 ...everything OK
        ''' 1 ...error reading ephemeris file
        ''' 2 ...epoch out of range.
        ''' </pre></returns>
        ''' <remarks>
        ''' The target number designation of the astronomical bodies is:
        ''' <pre>
        '''         = 0: Mercury,               1: Venus, 
        '''         = 2: Earth-Moon barycenter, 3: Mars, 
        '''         = 4: Jupiter,               5: Saturn, 
        '''         = 6: Uranus,                7: Neptune, 
        '''         = 8: Pluto,                 9: geocentric Moon, 
        '''         =10: Sun.
        ''' </pre>
        ''' <para>
        '''  NOTE 1. For ease in programming, the user may put the entire epoch in jed[0] and set jed[1] = 0. 
        ''' For maximum interpolation accuracy,  set jed[0] = the most recent midnight at or before interpolation epoch, 
        ''' and set jed[1] = fractional part of a day elapsed between jed[0] and epoch. As an alternative, it may prove 
        ''' convenient to set jed[0] = some fixed epoch, such as start of the integration and jed[1] = elapsed interval 
        ''' between then and epoch.
        ''' </para>
        ''' </remarks>
        Public Function State(ByRef Jed() As Double,
                                      ByVal Target As Target,
                                      ByRef TargetPos() As Double,
                                      ByRef TargetVel() As Double) As Short Implements INOVAS31.State

            Dim JdHp As New JDHighPrecision, VPos As New PosVector, VVel As New VelVector, rc As Short

            JdHp.JDPart1 = Jed(0)
            JdHp.JDPart2 = Jed(1)
            If Is64Bit() Then
                rc = State64(JdHp, Target, VPos, VVel)
            Else
                rc = State32(JdHp, Target, VPos, VVel)
            End If

            PosVecToArr(VPos, TargetPos)
            VelVecToArr(VVel, TargetVel)
            Return rc
        End Function
#End Region

#Region "Public NOVAS Interface - NOVAS Members"
        ''' <summary>
        '''  Corrects position vector for aberration of light.  Algorithm includes relativistic terms.
        ''' </summary>
        ''' <param name="Pos"> Position vector, referred to origin at center of mass of the Earth, components in AU.</param>
        ''' <param name="Vel"> Velocity vector of center of mass of the Earth, referred to origin at solar system barycenter, components in AU/day.</param>
        ''' <param name="LightTime"> Light time from object to Earth in days.</param>
        ''' <param name="Pos2"> Position vector, referred to origin at center of mass of the Earth, corrected for aberration, components in AU</param>
        ''' <remarks>If 'lighttime' = 0 on input, this function will compute it.</remarks>
        Public Sub Aberration(ByVal Pos() As Double,
                              ByVal Vel() As Double,
                              ByVal LightTime As Double,
                              ByRef Pos2() As Double) Implements INOVAS31.Aberration
            Dim VPos2 As PosVector
            If Is64Bit() Then
                Aberration64(ArrToPosVec(Pos), ArrToVelVec(Vel), LightTime, VPos2)
            Else
                Aberration32(ArrToPosVec(Pos), ArrToVelVec(Vel), LightTime, VPos2)
            End If
            PosVecToArr(VPos2, Pos2)

        End Sub

        ''' <summary>
        ''' Compute the apparent place of a planet or other solar system body.
        ''' </summary>
        ''' <param name="JdTt"> TT Julian date for apparent place.</param>
        ''' <param name="SsBody"> Pointer to structure containing the body designation for the solar system body </param>
        ''' <param name="Accuracy"> Code specifying the relative accuracy of the output position.</param>
        ''' <param name="Ra">Apparent right ascension in hours, referred to true equator and equinox of date.</param>
        ''' <param name="Dec"> Apparent declination in degrees, referred to true equator and equinox of date.</param>
        ''' <param name="Dis"> True distance from Earth to planet at 'JdTt' in AU.</param>
        ''' <returns><pre>
        '''    0 ... Everything OK
        '''    1 ... Invalid value of 'Type' in structure 'SsBody'
        ''' > 10 ... Error code from function 'Place'.
        ''' </pre></returns>
        ''' <remarks></remarks>
        Public Function AppPlanet(ByVal JdTt As Double,
                                  ByVal SsBody As Object3,
                                  ByVal Accuracy As Accuracy,
                                  ByRef Ra As Double,
                                  ByRef Dec As Double,
                                  ByRef Dis As Double) As Short Implements INOVAS31.AppPlanet
            If Is64Bit() Then
                Return AppPlanet64(JdTt, O3IFromObject3(SsBody), Accuracy, Ra, Dec, Dis)
            Else
                Return AppPlanet32(JdTt, O3IFromObject3(SsBody), Accuracy, Ra, Dec, Dis)
            End If

        End Function

        ''' <summary>
        '''  Computes the apparent place of a star at date 'JdTt', given its catalog mean place, proper motion, parallax, and radial velocity.
        ''' </summary>
        ''' <param name="JdTt">TT Julian date for apparent place.</param>
        ''' <param name="Star">Catalog entry structure containing catalog data forthe object in the ICRS </param>
        ''' <param name="Accuracy">Code specifying the relative accuracy of the output position.</param>
        ''' <param name="Ra">Apparent right ascension in hours, referred to true equator and equinox of date 'JdTt'.</param>
        ''' <param name="Dec">Apparent declination in degrees, referred to true equator and equinox of date 'JdTt'.</param>
        ''' <returns>
        ''' <pre>
        '''    0 ... Everything OK
        ''' > 10 ... Error code from function 'MakeObject'
        ''' > 20 ... Error code from function 'Place'.
        ''' </pre></returns>
        ''' <remarks></remarks>
        Public Function AppStar(ByVal JdTt As Double,
                                ByVal Star As CatEntry3,
                                ByVal Accuracy As Accuracy,
                                ByRef Ra As Double,
                                ByRef Dec As Double) As Short Implements INOVAS31.AppStar

            Dim rc As Short
            Try
                TL.LogMessage("AppStar", "JD Accuracy:        " & JdTt & " " & Accuracy.ToString)
                TL.LogMessage("AppStar", "  Star.RA:          " & Utl.HoursToHMS(Star.RA, ":", ":", "", 3))
                TL.LogMessage("AppStar", "  Dec:              " & Utl.DegreesToDMS(Star.Dec, ":", ":", "", 3))
                TL.LogMessage("AppStar", "  Catalog:          " & Star.Catalog)
                TL.LogMessage("AppStar", "  Parallax:         " & Star.Parallax)
                TL.LogMessage("AppStar", "  ProMoDec:         " & Star.ProMoDec)
                TL.LogMessage("AppStar", "  ProMoRA:          " & Star.ProMoRA)
                TL.LogMessage("AppStar", "  RadialVelocity:   " & Star.RadialVelocity)
                TL.LogMessage("AppStar", "  StarName:         " & Star.StarName)
                TL.LogMessage("AppStar", "  StarNumber:       " & Star.StarNumber)
            Catch ex As Exception
                TL.LogMessageCrLf("AppStar", "Exception: " & ex.ToString)
            End Try

            If Is64Bit() Then
                rc = AppStar64(JdTt, Star, Accuracy, Ra, Dec)
                TL.LogMessage("AppStar", "  64bit - Return Code: " & rc & ", RA Dec: " & Utl.HoursToHMS(Ra, ":", ":", "", 3) & " " & Utl.DegreesToDMS(Dec, ":", ":", "", 3))
                Return rc
            Else
                rc = AppStar32(JdTt, Star, Accuracy, Ra, Dec)
                TL.LogMessage("AppStar", "  32bit - Return Code: " & rc & ", RA Dec: " & Utl.HoursToHMS(Ra, ":", ":", "", 3) & " " & Utl.DegreesToDMS(Dec, ":", ":", "", 3))
                Return rc
            End If

        End Function

        ''' <summary>
        ''' Compute the astrometric place of a planet or other solar system body.
        ''' </summary>
        ''' <param name="JdTt">TT Julian date for astrometric place.</param>
        ''' <param name="SsBody">structure containing the body designation for the solar system body </param>
        ''' <param name="Accuracy">Code specifying the relative accuracy of the output position.</param>
        ''' <param name="Ra">Astrometric right ascension in hours (referred to the ICRS, without light deflection or aberration).</param>
        ''' <param name="Dec">Astrometric declination in degrees (referred to the ICRS, without light deflection or aberration).</param>
        ''' <param name="Dis">True distance from Earth to planet in AU.</param>
        ''' <returns>
        ''' <pre>
        '''    0 ... Everything OK
        '''    1 ... Invalid value of 'Type' in structure 'SsBody'
        ''' > 10 ... Error code from function 'Place'.
        ''' </pre></returns>
        ''' <remarks></remarks>
        Public Function AstroPlanet(ByVal JdTt As Double,
                                    ByVal SsBody As Object3,
                                    ByVal Accuracy As Accuracy,
                                    ByRef Ra As Double,
                                    ByRef Dec As Double,
                                    ByRef Dis As Double) As Short Implements INOVAS31.AstroPlanet
            If Is64Bit() Then
                Return AstroPlanet64(JdTt, O3IFromObject3(SsBody), Accuracy, Ra, Dec, Dis)
            Else
                Return AstroPlanet32(JdTt, O3IFromObject3(SsBody), Accuracy, Ra, Dec, Dis)
            End If
        End Function

        ''' <summary>
        '''  Computes the astrometric place of a star at date 'JdTt', given its catalog mean place, proper motion, parallax, and radial velocity.
        ''' </summary>
        ''' <param name="JdTt">TT Julian date for astrometric place.</param>
        ''' <param name="Star">Catalog entry structure containing catalog data for the object in the ICRS</param>
        ''' <param name="Accuracy">Code specifying the relative accuracy of the output position.</param>
        ''' <param name="Ra">Astrometric right ascension in hours (referred to the ICRS, without light deflection or aberration).</param>
        ''' <param name="Dec">Astrometric declination in degrees (referred to the ICRS, without light deflection or aberration).</param>
        ''' <returns><pre>
        '''    0 ... Everything OK
        ''' > 10 ... Error code from function 'MakeObject'
        ''' > 20 ... Error code from function 'Place'.
        ''' </pre></returns>
        ''' <remarks></remarks>
        Public Function AstroStar(ByVal JdTt As Double,
                                  ByVal Star As CatEntry3,
                                  ByVal Accuracy As Accuracy,
                                  ByRef Ra As Double,
                                  ByRef Dec As Double) As Short Implements INOVAS31.AstroStar
            Dim rc As Short
            Try
                TL.LogMessage("AstroStar", "JD Accuracy:        " & JdTt & " " & Accuracy.ToString)
                TL.LogMessage("AstroStar", "  Star.RA:          " & Utl.HoursToHMS(Star.RA, ":", ":", "", 3))
                TL.LogMessage("AstroStar", "  Dec:              " & Utl.DegreesToDMS(Star.Dec, ":", ":", "", 3))
                TL.LogMessage("AstroStar", "  Catalog:          " & Star.Catalog)
                TL.LogMessage("AstroStar", "  Parallax:         " & Star.Parallax)
                TL.LogMessage("AstroStar", "  ProMoDec:         " & Star.ProMoDec)
                TL.LogMessage("AstroStar", "  ProMoRA:          " & Star.ProMoRA)
                TL.LogMessage("AstroStar", "  RadialVelocity:   " & Star.RadialVelocity)
                TL.LogMessage("AstroStar", "  StarName:         " & Star.StarName)
                TL.LogMessage("AstroStar", "  StarNumber:       " & Star.StarNumber)
            Catch ex As Exception
                TL.LogMessageCrLf("AstroStar", "Exception: " & ex.ToString)
            End Try

            If Is64Bit() Then
                rc = AstroStar64(JdTt, Star, Accuracy, Ra, Dec)
                TL.LogMessage("AstroStar", "  64bit - Return Code: " & rc & ", RA Dec: " & Utl.HoursToHMS(Ra, ":", ":", "", 3) & " " & Utl.DegreesToDMS(Dec, ":", ":", "", 3))
                Return rc
            Else
                rc = AstroStar32(JdTt, Star, Accuracy, Ra, Dec)
                TL.LogMessage("AstroStar", "  32bit - Return Code: " & rc & ", RA Dec: " & Utl.HoursToHMS(Ra, ":", ":", "", 3) & " " & Utl.DegreesToDMS(Dec, ":", ":", "", 3))
                Return rc
            End If
        End Function

        ''' <summary>
        ''' Move the origin of coordinates from the barycenter of the solar system to the observer (or the geocenter); i.e., this function accounts for parallax (annual+geocentric or justannual).
        ''' </summary>
        ''' <param name="Pos">Position vector, referred to origin at solar system barycenter, components in AU.</param>
        ''' <param name="PosObs">Position vector of observer (or the geocenter), with respect to origin at solar system barycenter, components in AU.</param>
        ''' <param name="Pos2"> Position vector, referred to origin at center of mass of the Earth, components in AU.</param>
        ''' <param name="Lighttime">Light time from object to Earth in days.</param>
        ''' <remarks></remarks>
        Public Sub Bary2Obs(ByVal Pos() As Double,
                            ByVal PosObs() As Double,
                            ByRef Pos2() As Double,
                            ByRef Lighttime As Double) Implements INOVAS31.Bary2Obs
            Dim PosV As New PosVector
            If Is64Bit() Then
                Bary2Obs64(ArrToPosVec(Pos), ArrToPosVec(PosObs), PosV, Lighttime)
                PosVecToArr(PosV, Pos2)
            Else
                Bary2Obs32(ArrToPosVec(Pos), ArrToPosVec(PosObs), PosV, Lighttime)
                PosVecToArr(PosV, Pos2)
            End If
        End Sub

        ''' <summary>
        ''' This function will compute a date on the Gregorian calendar given the Julian date.
        ''' </summary>
        ''' <param name="Tjd">Julian date.</param>
        ''' <param name="Year">Year</param>
        ''' <param name="Month">Month number</param>
        ''' <param name="Day">day number</param>
        ''' <param name="Hour">Fractional hour of the day</param>
        ''' <remarks></remarks>
        Public Sub CalDate(ByVal Tjd As Double,
                           ByRef Year As Short,
                           ByRef Month As Short,
                           ByRef Day As Short,
                           ByRef Hour As Double) Implements INOVAS31.CalDate
            If Is64Bit() Then
                CalDate64(Tjd, Year, Month, Day, Hour)
            Else
                CalDate32(Tjd, Year, Month, Day, Hour)
            End If
        End Sub

        ''' <summary>
        ''' This function rotates a vector from the celestial to the terrestrial system.  Specifically, it transforms a vector in the
        ''' GCRS (a local space-fixed system) to the ITRS (a rotating earth-fixed system) by applying rotations for the GCRS-to-dynamical
        ''' frame tie, precession, nutation, Earth rotation, and polar motion.
        ''' </summary>
        ''' <param name="JdHigh">High-order part of UT1 Julian date.</param>
        ''' <param name="JdLow">Low-order part of UT1 Julian date.</param>
        ''' <param name="DeltaT">Value of Delta T (= TT - UT1) at the input UT1 Julian date.</param>
        ''' <param name="Method"> Selection for method: 0 ... CIO-based method; 1 ... equinox-based method</param>
        ''' <param name="Accuracy">Selection for accuracy</param>
        ''' <param name="OutputOption">0 ... The output vector is referred to GCRS axes; 1 ... The output 
        ''' vector is produced with respect to the equator and equinox of date. (See note 2 below)</param>
        ''' <param name="xp">Conventionally-defined X coordinate of celestial intermediate pole with respect to 
        ''' ITRS pole, in arcseconds.</param>
        ''' <param name="yp">Conventionally-defined Y coordinate of celestial intermediate pole with respect to 
        ''' ITRS pole, in arcseconds.</param>
        ''' <param name="VecT">Position vector, geocentric equatorial rectangular coordinates,
        ''' referred to GCRS axes (celestial system) or with respect to
        ''' the equator and equinox of date, depending on 'option'.</param>
        ''' <param name="VecC">Position vector, geocentric equatorial rectangular coordinates,
        ''' referred to ITRS axes (terrestrial system).</param>
        ''' <returns><pre>
        '''    0 ... everything is ok
        '''    1 ... invalid value of 'Accuracy'
        '''    2 ... invalid value of 'Method'
        ''' > 10 ... 10 + error from function 'CioLocation'
        ''' > 20 ... 20 + error from function 'CioBasis'
        ''' </pre></returns>
        ''' <remarks>Note 1: 'x' = 'y' = 0 means no polar motion transformation.
        ''' <para>
        ''' Note2: 'option' = 1 only works for the equinox-based method.
        '''</para></remarks>
        Public Function Cel2Ter(ByVal JdHigh As Double,
                                        ByVal JdLow As Double,
                                        ByVal DeltaT As Double,
                                        ByVal Method As Method,
                                        ByVal Accuracy As Accuracy,
                                        ByVal OutputOption As OutputVectorOption,
                                        ByVal xp As Double,
                                        ByVal yp As Double,
                                        ByVal VecT() As Double,
                                        ByRef VecC() As Double) As Short Implements INOVAS31.Cel2Ter
            Dim VVecC As New PosVector, rc As Short
            If Is64Bit() Then
                rc = Cel2Ter64(JdHigh, JdLow, DeltaT, Method, Accuracy, OutputOption, xp, yp, ArrToPosVec(VecT), VVecC)
            Else
                rc = Cel2Ter32(JdHigh, JdLow, DeltaT, Method, Accuracy, OutputOption, xp, yp, ArrToPosVec(VecT), VVecC)
            End If

            PosVecToArr(VVecC, VecC)
            Return rc
        End Function


        ''' <summary>
        '''  This function allows for the specification of celestial pole offsets for high-precision applications.  Each set of offsets is a correction to the modeled position of the pole for a specific date, derived from observations and published by the IERS.
        ''' </summary>
        ''' <param name="Tjd">TDB or TT Julian date for pole offsets.</param>
        ''' <param name="Type"> Type of pole offset. 1 for corrections to angular coordinates of modeled pole referred to mean ecliptic of date, that is, delta-delta-psi and delta-delta-epsilon.  2 for corrections to components of modeled pole unit vector referred to GCRS axes, that is, dx and dy.</param>
        ''' <param name="Dpole1">Value of celestial pole offset in first coordinate, (delta-delta-psi or dx) in milliarcseconds.</param>
        ''' <param name="Dpole2">Value of celestial pole offset in second coordinate, (delta-delta-epsilon or dy) in milliarcseconds.</param>
        ''' <returns><pre>
        ''' 0 ... Everything OK
        ''' 1 ... Invalid value of 'Type'.
        ''' </pre></returns>
        ''' <remarks></remarks>
        Public Function CelPole(ByVal Tjd As Double,
                                ByVal Type As PoleOffsetCorrection,
                                ByVal Dpole1 As Double,
                                ByVal Dpole2 As Double) As Short Implements INOVAS31.CelPole
            If Is64Bit() Then
                Return CelPole64(Tjd, Type, Dpole1, Dpole2)
            Else
                Return CelPole32(Tjd, Type, Dpole1, Dpole2)
            End If
        End Function

        ''' <summary>
        ''' Calaculate an array of CIO RA values around a given date
        ''' </summary>
        ''' <param name="JdTdb">TDB Julian date.</param>
        ''' <param name="NPts"> Number of Julian dates and right ascension values requested (not less than 2 or more than 20).</param>
        ''' <param name="Cio"> An arraylist of RaOfCIO structures containing a time series of the right ascension of the 
        ''' Celestial Intermediate Origin (CIO) with respect to the GCRS.</param>
        ''' <returns><pre>
        ''' 0 ... everything OK
        ''' 1 ... error opening the 'cio_ra.bin' file
        ''' 2 ... 'JdTdb' not in the range of the CIO file; 
        ''' 3 ... 'NPts' out of range
        ''' 4 ... unable to allocate memory for the internal 't' array; 
        ''' 5 ... unable to allocate memory for the internal 'ra' array; 
        ''' 6 ... 'JdTdb' is too close to either end of the CIO file; unable to put 'NPts' data points into the output object.
        ''' </pre></returns>
        ''' <remarks>
        ''' <para>
        ''' Given an input TDB Julian date and the number of data points desired, this function returns a set of 
        ''' Julian dates and corresponding values of the GCRS right ascension of the celestial intermediate origin (CIO).  
        ''' The range of dates is centered (at least approximately) on the requested date.  The function obtains 
        ''' the data from an external data file.</para>
        ''' <example>How to create and retrieve values from the arraylist
        ''' <code>
        ''' Dim CioList As New ArrayList, Nov3 As New ASCOM.Astrometry.NOVAS3
        ''' 
        ''' rc = Nov3.CioArray(2455251.5, 20, CioList) ' Get 20 values around date 00:00:00 February 24th 2010
        ''' MsgBox("Nov3 RC= " <![CDATA[&]]>  rc)
        ''' 
        ''' For Each CioA As ASCOM.Astrometry.RAOfCio In CioList
        '''     MsgBox("CIO Array " <![CDATA[&]]> CioA.JdTdb <![CDATA[&]]> " " <![CDATA[&]]> CioA.RACio)
        ''' Next
        ''' </code>
        ''' </example>
        ''' </remarks>
        Public Function CioArray(ByVal JdTdb As Double,
                                 ByVal NPts As Integer,
                                 ByRef Cio As ArrayList) As Short Implements INOVAS31.CioArray
            Dim CioStruct As New RAOfCioArray, rc As Short

            CioStruct.Initialise() 'Set internal default values so we can see which elements are changed by the NOVAS DLL.
            If Is64Bit() Then
                rc = CioArray64(JdTdb, NPts, CioStruct)
            Else
                rc = CioArray32(JdTdb, NPts, CioStruct)
            End If

            RACioArrayStructureToArr(CioStruct, Cio) 'Copy data from the CioStruct structure to the returning arraylist
            Return rc
        End Function

        ''' <summary>
        ''' Compute the orthonormal basis vectors of the celestial intermediate system.
        ''' </summary>
        ''' <param name="JdTdbEquionx">TDB Julian date of epoch.</param>
        ''' <param name="RaCioEquionx">Right ascension of the CIO at epoch (hours).</param>
        ''' <param name="RefSys">Reference system in which right ascension is given. 1 ... GCRS; 2 ... True equator and equinox of date.</param>
        ''' <param name="Accuracy">Accuracy</param>
        ''' <param name="x">Unit vector toward the CIO, equatorial rectangular coordinates, referred to the GCRS.</param>
        ''' <param name="y">Unit vector toward the y-direction, equatorial rectangular coordinates, referred to the GCRS.</param>
        ''' <param name="z">Unit vector toward north celestial pole (CIP), equatorial rectangular coordinates, referred to the GCRS.</param>
        ''' <returns><pre>
        ''' 0 ... everything OK
        ''' 1 ... invalid value of input variable 'RefSys'.
        ''' </pre></returns>
        ''' <remarks>
        ''' To compute the orthonormal basis vectors, with respect to the GCRS (geocentric ICRS), of the celestial 
        ''' intermediate system defined by the celestial intermediate pole (CIP) (in the z direction) and 
        ''' the celestial intermediate origin (CIO) (in the x direction).  A TDB Julian date and the 
        ''' right ascension of the CIO at that date is required as input.  The right ascension of the CIO 
        ''' can be with respect to either the GCRS origin or the true equinox of date -- different algorithms 
        ''' are used in the two cases.</remarks>
        Public Function CioBasis(ByVal JdTdbEquionx As Double,
                                 ByVal RaCioEquionx As Double,
                                 ByVal RefSys As ReferenceSystem,
                                 ByVal Accuracy As Accuracy,
                                 ByRef x As Double,
                                 ByRef y As Double,
                                 ByRef z As Double) As Short Implements INOVAS31.CioBasis
            If Is64Bit() Then
                Return CioBasis64(JdTdbEquionx, RaCioEquionx, RefSys, Accuracy, x, y, z)
            Else
                Return CioBasis32(JdTdbEquionx, RaCioEquionx, RefSys, Accuracy, x, y, z)
            End If
        End Function

        ''' <summary>
        ''' Returns the location of the celestial intermediate origin (CIO) for a given Julian date, as a right ascension 
        ''' </summary>
        ''' <param name="JdTdb">TDB Julian date.</param>
        ''' <param name="Accuracy">Selection for accuracy</param>
        ''' <param name="RaCio">Right ascension of the CIO, in hours.</param>
        ''' <param name="RefSys">Reference system in which right ascension is given</param>
        ''' <returns><pre>
        '''    0 ... everything OK
        '''    1 ... unable to allocate memory for the 'cio' array
        ''' > 10 ... 10 + the error code from function 'CioArray'.
        ''' </pre></returns>
        ''' <remarks>  This function returns the location of the celestial intermediate origin (CIO) for a given Julian date, as a right ascension with respect to either the GCRS (geocentric ICRS) origin or the true equinox of date.  The CIO is always located on the true equator (= intermediate equator) of date.</remarks>
        Public Function CioLocation(ByVal JdTdb As Double,
                                    ByVal Accuracy As Accuracy,
                                    ByRef RaCio As Double,
                                    ByRef RefSys As ReferenceSystem) As Short Implements INOVAS31.CioLocation
            If Is64Bit() Then
                Return CioLocation64(JdTdb, Accuracy, RaCio, RefSys)
            Else
                Return CioLocation32(JdTdb, Accuracy, RaCio, RefSys)
            End If
        End Function

        ''' <summary>
        ''' Computes the true right ascension of the celestial intermediate origin (CIO) at a given TT Julian date.  This is -(equation of the origins).
        ''' </summary>
        ''' <param name="JdTt">TT Julian date</param>
        ''' <param name="Accuracy">Selection for accuracy</param>
        ''' <param name="RaCio"> Right ascension of the CIO, with respect to the true equinox of date, in hours (+ or -).</param>
        ''' <returns>
        ''' <pre>
        '''   0  ... everything OK
        '''   1  ... invalid value of 'Accuracy'
        ''' > 10 ... 10 + the error code from function 'CioLocation'
        ''' > 20 ... 20 + the error code from function 'CioBasis'.
        ''' </pre></returns>
        ''' <remarks></remarks>
        Public Function CioRa(ByVal JdTt As Double,
                                      ByVal Accuracy As Accuracy,
                                      ByRef RaCio As Double) As Short Implements INOVAS31.CioRa
            If Is64Bit() Then
                Return CioRa64(JdTt, Accuracy, RaCio)
            Else
                Return CioRa32(JdTt, Accuracy, RaCio)
            End If
        End Function

        ''' <summary>
        ''' Returns the difference in light-time, for a star, between the barycenter of the solar system and the observer (or the geocenter).
        ''' </summary>
        ''' <param name="Pos1">Position vector of star, with respect to origin at solar system barycenter.</param>
        ''' <param name="PosObs">Position vector of observer (or the geocenter), with respect to origin at solar system barycenter, components in AU.</param>
        ''' <returns>Difference in light time, in the sense star to barycenter minus star to earth, in days.</returns>
        ''' <remarks>
        ''' Alternatively, this function returns the light-time from the observer (or the geocenter) to a point on a 
        ''' light ray that is closest to a specific solar system body.  For this purpose, 'Pos1' is the position 
        ''' vector toward observed object, with respect to origin at observer (or the geocenter); 'PosObs' is 
        ''' the position vector of solar system body, with respect to origin at observer (or the geocenter), 
        ''' components in AU; and the returned value is the light time to point on line defined by 'Pos1' 
        ''' that is closest to solar system body (positive if light passes body before hitting observer, i.e., if 
        ''' 'Pos1' is within 90 degrees of 'PosObs').
        ''' </remarks>
        Public Function DLight(ByVal Pos1() As Double,
                               ByVal PosObs() As Double) As Double Implements INOVAS31.DLight
            If Is64Bit() Then
                Return DLight64(ArrToPosVec(Pos1), ArrToPosVec(PosObs))
            Else
                Return DLight32(ArrToPosVec(Pos1), ArrToPosVec(PosObs))
            End If
        End Function

        ''' <summary>
        ''' Converts an ecliptic position vector to an equatorial position vector.
        ''' </summary>
        ''' <param name="JdTt">TT Julian date of equator, equinox, and ecliptic used for coordinates.</param>
        ''' <param name="CoordSys">Coordinate system selection. 0 ... mean equator and equinox of date; 1 ... true equator and equinox of date; 2 ... ICRS</param>
        ''' <param name="Accuracy">Selection for accuracy</param>
        ''' <param name="Pos1"> Position vector, referred to specified ecliptic and equinox of date.  If 'CoordSys' = 2, 'pos1' must be on mean ecliptic and equinox of J2000.0; see Note 1 below.</param>
        ''' <param name="Pos2">Position vector, referred to specified equator and equinox of date.</param>
        ''' <returns><pre>
        ''' 0 ... everything OK
        ''' 1 ... invalid value of 'CoordSys'
        ''' </pre></returns>
        ''' <remarks>
        ''' To convert an ecliptic vector (mean ecliptic and equinox of J2000.0 only) to an ICRS vector, 
        ''' set 'CoordSys' = 2; the value of 'JdTt' can be set to anything, since J2000.0 is assumed. 
        ''' Except for the output from this case, all vectors are assumed to be with respect to a dynamical system.
        ''' </remarks>
        Public Function Ecl2EquVec(ByVal JdTt As Double,
                                   ByVal CoordSys As CoordSys,
                                   ByVal Accuracy As Accuracy,
                                   ByVal Pos1() As Double,
                                   ByRef Pos2() As Double) As Short Implements INOVAS31.Ecl2EquVec
            Dim VPos2 As New PosVector, rc As Short
            If Is64Bit() Then
                rc = Ecl2EquVec64(JdTt, CoordSys, Accuracy, ArrToPosVec(Pos1), VPos2)
            Else
                rc = Ecl2EquVec32(JdTt, CoordSys, Accuracy, ArrToPosVec(Pos1), VPos2)
            End If

            PosVecToArr(VPos2, Pos2)
            Return rc
        End Function

        ''' <summary>
        '''  Compute the "complementary terms" of the equation of the equinoxes consistent with IAU 2000 resolutions.
        ''' </summary>
        ''' <param name="JdHigh">High-order part of TT Julian date.</param>
        ''' <param name="JdLow">Low-order part of TT Julian date.</param>
        ''' <param name="Accuracy">Selection for accuracy</param>
        ''' <returns>Complementary terms, in radians.</returns>
        ''' <remarks>
        ''' 1. The series used in this function was derived from Series from IERS Conventions (2003), Chapter 5, Table 5.2C.
        ''' This same series was also adopted for use in the IAU's Standards of Fundamental Astronomy (SOFA) software (i.e., subroutine 
        ''' eect00.for and function eect00.c).
        ''' <para>2. The low-accuracy series used in this function is a simple implementation derived from the first reference, in which terms
        ''' smaller than 2 microarcseconds have been omitted.</para>
        ''' <para>3. This function is based on NOVAS Fortran routine 'eect2000', with the low-accuracy formula taken from NOVAS Fortran routine 'etilt'.</para>
        ''' </remarks>
        Public Function EeCt(ByVal JdHigh As Double,
                             ByVal JdLow As Double,
                             ByVal Accuracy As Accuracy) As Double Implements INOVAS31.EeCt
            If Is64Bit() Then
                Return EeCt64(JdHigh, JdLow, Accuracy)
            Else
                Return EeCt32(JdHigh, JdLow, Accuracy)
            End If
        End Function

        ''' <summary>
        '''  Retrieves the position and velocity of a solar system body from a fundamental ephemeris.
        ''' </summary>
        ''' <param name="Jd"> TDB Julian date split into two parts, where the sum jd[0] + jd[1] is the TDB Julian date.</param>
        ''' <param name="CelObj">Structure containing the designation of the body of interest </param>
        ''' <param name="Origin"> Origin code; solar system barycenter = 0, center of mass of the Sun = 1.</param>
        ''' <param name="Accuracy">Slection for accuracy</param>
        ''' <param name="Pos">Position vector of the body at 'Jd'; equatorial rectangular coordinates in AU referred to the ICRS.</param>
        ''' <param name="Vel">Velocity vector of the body at 'Jd'; equatorial rectangular system referred to the mean equator and equinox of the ICRS, in AU/Day.</param>
        ''' <returns><pre>
        '''    0 ... Everything OK
        '''    1 ... Invalid value of 'Origin'
        '''    2 ... Invalid value of 'Type' in 'CelObj'; 
        '''    3 ... Unable to allocate memory
        ''' 10+n ... where n is the error code from 'SolarSystem'; 
        ''' 20+n ... where n is the error code from 'ReadEph'.
        ''' </pre></returns>
        ''' <remarks>It is recommended that the input structure 'cel_obj' be created using function 'MakeObject' in file novas.c.</remarks>
        Public Function Ephemeris(ByVal Jd() As Double,
                                  ByVal CelObj As Object3,
                                  ByVal Origin As Origin,
                                  ByVal Accuracy As Accuracy,
                                  ByRef Pos() As Double,
                                  ByRef Vel() As Double) As Short Implements INOVAS31.Ephemeris
            Dim VPos As New PosVector, VVel As New VelVector, JdHp As JDHighPrecision, rc As Short
            JdHp.JDPart1 = Jd(0)
            JdHp.JDPart2 = Jd(1)
            If Is64Bit() Then
                rc = Ephemeris64(JdHp, O3IFromObject3(CelObj), Origin, Accuracy, VPos, VVel)
            Else
                rc = Ephemeris32(JdHp, O3IFromObject3(CelObj), Origin, Accuracy, VPos, VVel)
            End If

            PosVecToArr(VPos, Pos)
            VelVecToArr(VVel, Vel)
            Return rc
        End Function

        ''' <summary>
        ''' To convert right ascension and declination to ecliptic longitude and latitude.
        ''' </summary>
        ''' <param name="JdTt">TT Julian date of equator, equinox, and ecliptic used for coordinates.</param>
        ''' <param name="CoordSys"> Coordinate system: 0 ... mean equator and equinox of date 'JdTt'; 1 ... true equator and equinox of date 'JdTt'; 2 ... ICRS</param>
        ''' <param name="Accuracy">Selection for accuracy</param>
        ''' <param name="Ra">Right ascension in hours, referred to specified equator and equinox of date.</param>
        ''' <param name="Dec">Declination in degrees, referred to specified equator and equinox of date.</param>
        ''' <param name="ELon">Ecliptic longitude in degrees, referred to specified ecliptic and equinox of date.</param>
        ''' <param name="ELat">Ecliptic latitude in degrees, referred to specified ecliptic and equinox of date.</param>
        ''' <returns><pre>
        ''' 0 ... everything OK
        ''' 1 ... invalid value of 'CoordSys'
        ''' </pre></returns>
        ''' <remarks>
        ''' To convert ICRS RA and dec to ecliptic coordinates (mean ecliptic and equinox of J2000.0), 
        ''' set 'CoordSys' = 2; the value of 'JdTt' can be set to anything, since J2000.0 is assumed. 
        ''' Except for the input to this case, all input coordinates are dynamical.
        ''' </remarks>
        Public Function Equ2Ecl(ByVal JdTt As Double,
                                        ByVal CoordSys As CoordSys,
                                        ByVal Accuracy As Accuracy,
                                        ByVal Ra As Double,
                                        ByVal Dec As Double,
                                        ByRef ELon As Double,
                                        ByRef ELat As Double) As Short Implements INOVAS31.Equ2Ecl
            If Is64Bit() Then
                Return Equ2Ecl64(JdTt, CoordSys, Accuracy, Ra, Dec, ELon, ELat)
            Else
                Return Equ2Ecl32(JdTt, CoordSys, Accuracy, Ra, Dec, ELon, ELat)
            End If
        End Function

        ''' <summary>
        ''' Converts an equatorial position vector to an ecliptic position vector.
        ''' </summary>
        ''' <param name="JdTt">TT Julian date of equator, equinox, and ecliptic used for</param>
        ''' <param name="CoordSys"> Coordinate system selection. 0 ... mean equator and equinox of date 'JdTt'; 1 ... true equator and equinox of date 'JdTt'; 2 ... ICRS</param>
        ''' <param name="Accuracy">Selection for accuracy</param>
        ''' <param name="Pos1">Position vector, referred to specified equator and equinox of date.</param>
        ''' <param name="Pos2">Position vector, referred to specified ecliptic and equinox of date.</param>
        ''' <returns><pre>
        ''' 0 ... everything OK
        ''' 1 ... invalid value of 'CoordSys'
        ''' </pre></returns>
        ''' <remarks>To convert an ICRS vector to an ecliptic vector (mean ecliptic and equinox of J2000.0 only), 
        ''' set 'CoordSys' = 2; the value of 'JdTt' can be set to anything, since J2000.0 is assumed. Except for 
        ''' the input to this case, all vectors are assumed to be with respect to a dynamical system.</remarks>
        Public Function Equ2EclVec(ByVal JdTt As Double,
                                           ByVal CoordSys As CoordSys,
                                           ByVal Accuracy As Accuracy,
                                           ByVal Pos1() As Double,
                                           ByRef Pos2() As Double) As Short Implements INOVAS31.Equ2EclVec
            Dim VPos2 As New PosVector, rc As Short
            If Is64Bit() Then
                rc = Equ2EclVec64(JdTt, CoordSys, Accuracy, ArrToPosVec(Pos1), VPos2)
            Else
                rc = Equ2EclVec32(JdTt, CoordSys, Accuracy, ArrToPosVec(Pos1), VPos2)
            End If

            PosVecToArr(VPos2, Pos2)
            Return rc
        End Function

        ''' <summary>
        ''' Converts ICRS right ascension and declination to galactic longitude and latitude.
        ''' </summary>
        ''' <param name="RaI">ICRS right ascension in hours.</param>
        ''' <param name="DecI">ICRS declination in degrees.</param>
        ''' <param name="GLon">Galactic longitude in degrees.</param>
        ''' <param name="GLat">Galactic latitude in degrees.</param>
        ''' <remarks></remarks>
        Public Sub Equ2Gal(ByVal RaI As Double,
                                   ByVal DecI As Double,
                                   ByRef GLon As Double,
                                   ByRef GLat As Double) Implements INOVAS31.Equ2Gal
            If Is64Bit() Then
                Equ2Gal64(RaI, DecI, GLon, GLat)
            Else
                Equ2Gal32(RaI, DecI, GLon, GLat)
            End If
        End Sub

        ''' <summary>
        ''' Transforms topocentric right ascension and declination to zenith distance and azimuth.  
        ''' </summary>
        ''' <param name="Jd_Ut1">UT1 Julian date.</param>
        ''' <param name="DeltT">Difference TT-UT1 at 'jd_ut1', in seconds.</param>
        ''' <param name="Accuracy">Selection for accuracy</param>
        ''' <param name="xp">onventionally-defined x coordinate of celestial intermediate pole with respect to ITRS reference pole, in arcseconds.</param>
        ''' <param name="yp">Conventionally-defined y coordinate of celestial intermediate pole with respect to ITRS reference pole, in arcseconds.</param>
        ''' <param name="Location">Structure containing observer's location </param>
        ''' <param name="Ra">Topocentric right ascension of object of interest, in hours, referred to true equator and equinox of date.</param>
        ''' <param name="Dec">Topocentric declination of object of interest, in degrees, referred to true equator and equinox of date.</param>
        ''' <param name="RefOption">Refraction option. 0 ... no refraction; 1 ... include refraction, using 'standard' atmospheric conditions;
        ''' 2 ... include refraction, using atmospheric parametersinput in the 'Location' structure.</param>
        ''' <param name="Zd">Topocentric zenith distance in degrees, affected by refraction if 'ref_option' is non-zero.</param>
        ''' <param name="Az">Topocentric azimuth (measured east from north) in degrees.</param>
        ''' <param name="RaR"> Topocentric right ascension of object of interest, in hours, referred to true equator and 
        ''' equinox of date, affected by refraction if 'ref_option' is non-zero.</param>
        ''' <param name="DecR">Topocentric declination of object of interest, in degrees, referred to true equator and 
        ''' equinox of date, affected by refraction if 'ref_option' is non-zero.</param>
        ''' <remarks>This function transforms topocentric right ascension and declination to zenith distance and azimuth.  
        ''' It uses a method that properly accounts for polar motion, which is significant at the sub-arcsecond level.  
        ''' This function can also adjust coordinates for atmospheric refraction.</remarks>
        Public Sub Equ2Hor(ByVal Jd_Ut1 As Double,
                                   ByVal DeltT As Double,
                                   ByVal Accuracy As Accuracy,
                                   ByVal xp As Double,
                                   ByVal yp As Double,
                                   ByVal Location As OnSurface,
                                   ByVal Ra As Double,
                                   ByVal Dec As Double,
                                   ByVal RefOption As RefractionOption,
                                   ByRef Zd As Double,
                                   ByRef Az As Double,
                                   ByRef RaR As Double,
                                   ByRef DecR As Double) Implements INOVAS31.Equ2Hor
            Try

                TL.LogMessage("Equ2Hor", "JD Accuracy RA DEC:     " & Jd_Ut1 & " " & Accuracy.ToString & " " & Utl.HoursToHMS(Ra, ":", ":", "", 3) & " " & Utl.DegreesToDMS(Dec, ":", ":", "", 3))
                TL.LogMessage("Equ2Hor", "  DeltaT:               " & DeltT)
                TL.LogMessage("Equ2Hor", "  xp:                   " & xp)
                TL.LogMessage("Equ2Hor", "  yp:                   " & yp)
                TL.LogMessage("Equ2Hor", "  Refraction:           " & RefOption.ToString)
                TL.LogMessage("Equ2Hor", "  Location.Height:      " & Location.Height)
                TL.LogMessage("Equ2Hor", "  Location.Latitude:    " & Location.Latitude)
                TL.LogMessage("Equ2Hor", "  Location.Longitude:   " & Location.Longitude)
                TL.LogMessage("Equ2Hor", "  Location.Pressure:    " & Location.Pressure)
                TL.LogMessage("Equ2Hor", "  Location.Temperature: " & Location.Temperature)
            Catch ex As Exception
                TL.LogMessageCrLf("Equ2Hor", "Exception: " & ex.ToString)
            End Try

            If Is64Bit() Then
                Equ2Hor64(Jd_Ut1, DeltT, Accuracy, xp, yp, Location, Ra, Dec, RefOption, Zd, Az, RaR, DecR)
                TL.LogMessage("Equ2Hor", "  64bit - RA Dec: " & Utl.HoursToHMS(RaR, ":", ":", "", 3) & " " & Utl.DegreesToDMS(DecR, ":", ":", "", 3))
            Else
                Equ2Hor32(Jd_Ut1, DeltT, Accuracy, xp, yp, Location, Ra, Dec, RefOption, Zd, Az, RaR, DecR)
                TL.LogMessage("Equ2Hor", "  32bit - RA Dec: " & Utl.HoursToHMS(RaR, ":", ":", "", 3) & " " & Utl.DegreesToDMS(DecR, ":", ":", "", 3))
            End If

        End Sub

        ''' <summary>
        ''' Returns the value of the Earth Rotation Angle (theta) for a given UT1 Julian date. 
        ''' </summary>
        ''' <param name="JdHigh">High-order part of UT1 Julian date.</param>
        ''' <param name="JdLow">Low-order part of UT1 Julian date.</param>
        ''' <returns>The Earth Rotation Angle (theta) in degrees.</returns>
        ''' <remarks> The expression used is taken from the note to IAU Resolution B1.8 of 2000.  1. The algorithm used 
        ''' here is equivalent to the canonical theta = 0.7790572732640 + 1.00273781191135448 * t, where t is the time 
        ''' in days from J2000 (t = JdHigh + JdLow - T0), but it avoids many two-PI 'wraps' that 
        ''' decrease precision (adopted from SOFA Fortran routine iau_era00; see also expression at top 
        ''' of page 35 of IERS Conventions (1996)).</remarks>
        Public Function Era(ByVal JdHigh As Double,
                                    ByVal JdLow As Double) As Double Implements INOVAS31.Era
            If Is64Bit() Then
                Return Era64(JdHigh, JdLow)
            Else
                Return Era32(JdHigh, JdLow)
            End If
        End Function

        ''' <summary>
        ''' Computes quantities related to the orientation of the Earth's rotation axis at Julian date 'JdTdb'.
        ''' </summary>
        ''' <param name="JdTdb">TDB Julian Date.</param>
        ''' <param name="Accuracy">Selection for accuracy</param>
        ''' <param name="Mobl">Mean obliquity of the ecliptic in degrees at 'JdTdb'.</param>
        ''' <param name="Tobl">True obliquity of the ecliptic in degrees at 'JdTdb'.</param>
        ''' <param name="Ee">Equation of the equinoxes in seconds of time at 'JdTdb'.</param>
        ''' <param name="Dpsi">Nutation in longitude in arcseconds at 'JdTdb'.</param>
        ''' <param name="Deps">Nutation in obliquity in arcseconds at 'JdTdb'.</param>
        ''' <remarks>Values of the celestial pole offsets 'PSI_COR' and 'EPS_COR' are set using function 'cel_pole', 
        ''' if desired.  See the prolog of 'cel_pole' for details.</remarks>
        Public Sub ETilt(ByVal JdTdb As Double,
                                 ByVal Accuracy As Accuracy,
                                 ByRef Mobl As Double,
                                 ByRef Tobl As Double,
                                 ByRef Ee As Double,
                                 ByRef Dpsi As Double,
                                 ByRef Deps As Double) Implements INOVAS31.ETilt
            If Is64Bit() Then
                ETilt64(JdTdb, Accuracy, Mobl, Tobl, Ee, Dpsi, Deps)
            Else
                ETilt32(JdTdb, Accuracy, Mobl, Tobl, Ee, Dpsi, Deps)
            End If
        End Sub

        ''' <summary>
        '''  To transform a vector from the dynamical reference system to the International Celestial Reference System (ICRS), or vice versa.
        ''' </summary>
        ''' <param name="Pos1">Position vector, equatorial rectangular coordinates.</param>
        ''' <param name="Direction">Set 'direction' <![CDATA[<]]> 0 for dynamical to ICRS transformation. Set 'direction' <![CDATA[>=]]> 0 for 
        ''' ICRS to dynamical transformation.</param>
        ''' <param name="Pos2">Position vector, equatorial rectangular coordinates.</param>
        ''' <remarks></remarks>
        Public Sub FrameTie(ByVal Pos1() As Double,
                                    ByVal Direction As FrameConversionDirection,
                                    ByRef Pos2() As Double) Implements INOVAS31.FrameTie
            Dim VPos2 As New PosVector

            If Is64Bit() Then
                FrameTie64(ArrToPosVec(Pos1), Direction, VPos2)
            Else
                FrameTie32(ArrToPosVec(Pos1), Direction, VPos2)
            End If
            PosVecToArr(VPos2, Pos2)
        End Sub

        ''' <summary>
        ''' To compute the fundamental arguments (mean elements) of the Sun and Moon.
        ''' </summary>
        ''' <param name="t">TDB time in Julian centuries since J2000.0</param>
        ''' <param name="a">Double array of fundamental arguments</param>
        ''' <remarks>
        ''' Fundamental arguments, in radians:
        ''' <pre>
        '''   a[0] = l (mean anomaly of the Moon)
        '''   a[1] = l' (mean anomaly of the Sun)
        '''   a[2] = F (mean argument of the latitude of the Moon)
        '''   a[3] = D (mean elongation of the Moon from the Sun)
        '''   a[4] = a[4] (mean longitude of the Moon's ascending node);
        '''                from Simon section 3.4(b.3),
        '''                precession = 5028.8200 arcsec/cy)
        ''' </pre>
        ''' </remarks>
        Public Sub FundArgs(ByVal t As Double,
                                    ByRef a() As Double) Implements INOVAS31.FundArgs
            Dim va As New FundamentalArgs

            If Is64Bit() Then
                FundArgs64(t, va)
            Else
                FundArgs32(t, va)
            End If

            a(0) = va.l
            a(1) = va.ldash
            a(2) = va.F
            a(3) = va.D
            a(4) = va.Omega

        End Sub

        ''' <summary>
        ''' Converts GCRS right ascension and declination to coordinates with respect to the equator of date (mean or true).
        ''' </summary>
        ''' <param name="JdTt">TT Julian date of equator to be used for output coordinates.</param>
        ''' <param name="CoordSys"> Coordinate system selection for output coordinates.; 0 ... mean equator and 
        ''' equinox of date; 1 ... true equator and equinox of date; 2 ... true equator and CIO of date</param>
        ''' <param name="Accuracy">Selection for accuracy</param>
        ''' <param name="RaG">GCRS right ascension in hours.</param>
        ''' <param name="DecG">GCRS declination in degrees.</param>
        ''' <param name="Ra"> Right ascension in hours, referred to specified equator and right ascension origin of date.</param>
        ''' <param name="Dec">Declination in degrees, referred to specified equator of date.</param>
        ''' <returns>
        ''' <pre>
        '''    0 ... everything OK
        ''' >  0 ... error from function 'Vector2RaDec'' 
        ''' > 10 ... 10 + error from function 'CioLocation'
        ''' > 20 ... 20 + error from function 'CioBasis'
        ''' </pre>></returns>
        ''' <remarks>For coordinates with respect to the true equator of date, the origin of right ascension can be either the true equinox or the celestial intermediate origin (CIO).
        ''' <para> This function only supports the CIO-based method.</para></remarks>
        Public Function Gcrs2Equ(ByVal JdTt As Double,
                                         ByVal CoordSys As CoordSys,
                                         ByVal Accuracy As Accuracy,
                                         ByVal RaG As Double,
                                         ByVal DecG As Double,
                                         ByRef Ra As Double,
                                         ByRef Dec As Double) As Short Implements INOVAS31.Gcrs2Equ
            If Is64Bit() Then
                Return Gcrs2Equ64(JdTt, CoordSys, Accuracy, RaG, DecG, Ra, Dec)
            Else
                Return Gcrs2Equ32(JdTt, CoordSys, Accuracy, RaG, DecG, Ra, Dec)
            End If
        End Function

        ''' <summary>
        ''' This function computes the geocentric position and velocity of an observer on 
        ''' the surface of the earth or on a near-earth spacecraft.</summary>
        ''' <param name="JdTt">TT Julian date.</param>
        ''' <param name="DeltaT">Value of Delta T (= TT - UT1) at 'JdTt'.</param>
        ''' <param name="Accuracy">Selection for accuracy</param>
        ''' <param name="Obs">Data specifying the location of the observer</param>
        ''' <param name="Pos">Position vector of observer, with respect to origin at geocenter, 
        ''' referred to GCRS axes, components in AU.</param>
        ''' <param name="Vel">Velocity vector of observer, with respect to origin at geocenter, 
        ''' referred to GCRS axes, components in AU/day.</param>
        ''' <returns>
        ''' <pre>
        ''' 0 ... everything OK
        ''' 1 ... invalid value of 'Accuracy'.
        ''' </pre></returns>
        ''' <remarks>The final vectors are expressed in the GCRS.</remarks>
        Public Function GeoPosVel(ByVal JdTt As Double,
                                          ByVal DeltaT As Double,
                                          ByVal Accuracy As Accuracy,
                                          ByVal Obs As Observer,
                                          ByRef Pos() As Double,
                                          ByRef Vel() As Double) As Short Implements INOVAS31.GeoPosVel
            Dim VPos As New PosVector, VVel As New VelVector, rc As Short

            If Is64Bit() Then
                rc = GeoPosVel64(JdTt, DeltaT, Accuracy, Obs, VPos, VVel)
            Else
                rc = GeoPosVel32(JdTt, DeltaT, Accuracy, Obs, VPos, VVel)
            End If

            PosVecToArr(VPos, Pos)
            VelVecToArr(VVel, Vel)
            Return rc
        End Function

        ''' <summary>
        ''' Computes the total gravitational deflection of light for the observed object due to the major gravitating bodies in the solar system.
        ''' </summary>
        ''' <param name="JdTdb">TDB Julian date of observation.</param>
        ''' <param name="LocCode">Code for location of observer, determining whether the gravitational deflection due to the earth itself is applied.</param>
        ''' <param name="Accuracy">Selection for accuracy</param>
        ''' <param name="Pos1"> Position vector of observed object, with respect to origin at observer (or the geocenter), 
        ''' referred to ICRS axes, components in AU.</param>
        ''' <param name="PosObs">Position vector of observer (or the geocenter), with respect to origin at solar 
        ''' system barycenter, referred to ICRS axes, components in AU.</param>
        ''' <param name="Pos2">Position vector of observed object, with respect to origin at observer (or the geocenter), 
        ''' referred to ICRS axes, corrected for gravitational deflection, components in AU.</param>
        ''' <returns><pre>
        '''    0 ... Everything OK
        ''' <![CDATA[<]]> 30 ... Error from function 'Ephemeris'; 
        ''' > 30 ... Error from function 'MakeObject'.
        ''' </pre></returns>
        ''' <remarks>This function valid for an observed body within the solar system as well as for a star.
        ''' <para>
        ''' If 'Accuracy' is set to zero (full accuracy), three bodies (Sun, Jupiter, and Saturn) are 
        ''' used in the calculation.  If the reduced-accuracy option is set, only the Sun is used in the 
        ''' calculation.  In both cases, if the observer is not at the geocenter, the deflection due to the Earth is included.
        ''' </para>
        ''' </remarks>
        Public Function GravDef(ByVal JdTdb As Double,
                                        ByVal LocCode As EarthDeflection,
                                        ByVal Accuracy As Accuracy,
                                        ByVal Pos1() As Double,
                                        ByVal PosObs() As Double,
                                        ByRef Pos2() As Double) As Short Implements INOVAS31.GravDef
            Dim VPos2 As New PosVector, rc As Short

            If Is64Bit() Then
                rc = GravDef64(JdTdb, LocCode, Accuracy, ArrToPosVec(Pos1), ArrToPosVec(PosObs), VPos2)
            Else
                rc = GravDef32(JdTdb, LocCode, Accuracy, ArrToPosVec(Pos1), ArrToPosVec(PosObs), VPos2)
            End If

            PosVecToArr(VPos2, Pos2)
            Return rc
        End Function

        ''' <summary>
        ''' Corrects position vector for the deflection of light in the gravitational field of an arbitrary body.
        ''' </summary>
        ''' <param name="Pos1">Position vector of observed object, with respect to origin at observer 
        ''' (or the geocenter), components in AU.</param>
        ''' <param name="PosObs">Position vector of observer (or the geocenter), with respect to origin at 
        ''' solar system barycenter, components in AU.</param>
        ''' <param name="PosBody">Position vector of gravitating body, with respect to origin at solar system 
        ''' barycenter, components in AU.</param>
        ''' <param name="RMass">Reciprocal mass of gravitating body in solar mass units, that is, 
        ''' Sun mass / body mass.</param>
        ''' <param name="Pos2">Position vector of observed object, with respect to origin at observer 
        ''' (or the geocenter), corrected for gravitational deflection, components in AU.</param>
        ''' <remarks>This function valid for an observed body within the solar system as well as for a star.</remarks>
        Public Sub GravVec(ByVal Pos1() As Double,
                                   ByVal PosObs() As Double,
                                   ByVal PosBody() As Double,
                                   ByVal RMass As Double,
                                   ByRef Pos2() As Double) Implements INOVAS31.GravVec
            Dim VPos2 As New PosVector

            If Is64Bit() Then
                GravVec64(ArrToPosVec(Pos1), ArrToPosVec(PosObs), ArrToPosVec(PosBody), RMass, VPos2)
            Else
                GravVec32(ArrToPosVec(Pos1), ArrToPosVec(PosObs), ArrToPosVec(PosBody), RMass, VPos2)
            End If

            PosVecToArr(VPos2, Pos2)
        End Sub

        ''' <summary>
        ''' Compute the intermediate right ascension of the equinox at the input Julian date
        ''' </summary>
        ''' <param name="JdTdb">TDB Julian date.</param>
        ''' <param name="Equinox">Equinox selection flag: mean pr true</param>
        ''' <param name="Accuracy">Selection for accuracy</param>
        ''' <returns>Intermediate right ascension of the equinox, in hours (+ or -). If 'equinox' = 1 
        ''' (i.e true equinox), then the returned value is the equation of the origins.</returns>
        ''' <remarks></remarks>
        Public Function IraEquinox(ByVal JdTdb As Double,
                                           ByVal Equinox As EquinoxType,
                                           ByVal Accuracy As Accuracy) As Double Implements INOVAS31.IraEquinox
            If Is64Bit() Then
                Return IraEquinox64(JdTdb, Equinox, Accuracy)
            Else
                Return IraEquinox32(JdTdb, Equinox, Accuracy)
            End If
        End Function

        ''' <summary>
        ''' Compute the Julian date for a given calendar date (year, month, day, hour).
        ''' </summary>
        ''' <param name="Year">Year number</param>
        ''' <param name="Month">Month number</param>
        ''' <param name="Day">Day number</param>
        ''' <param name="Hour">Fractional hour of the day</param>
        ''' <returns>Computed Julian date.</returns>
        ''' <remarks>This function makes no checks for a valid input calendar date. The input calendar date 
        ''' must be Gregorian. The input time value can be based on any UT-like time scale (UTC, UT1, TT, etc.) 
        ''' - output Julian date will have the same basis.</remarks>
        Public Function JulianDate(ByVal Year As Short,
                                           ByVal Month As Short,
                                           ByVal Day As Short,
                                           ByVal Hour As Double) As Double Implements INOVAS31.JulianDate
            If Is64Bit() Then
                Return JulianDate64(Year, Month, Day, Hour)
            Else
                Return JulianDate32(Year, Month, Day, Hour)
            End If
        End Function

        ''' <summary>
        ''' Computes the geocentric position of a solar system body, as antedated for light-time.
        ''' </summary>
        ''' <param name="JdTdb">TDB Julian date of observation.</param>
        ''' <param name="SsObject">Structure containing the designation for thesolar system body</param>
        ''' <param name="PosObs">Position vector of observer (or the geocenter), with respect to origin 
        ''' at solar system barycenter, referred to ICRS axes, components in AU.</param>
        ''' <param name="TLight0">First approximation to light-time, in days (can be set to 0.0 if unknown)</param>
        ''' <param name="Accuracy">Selection for accuracy</param>
        ''' <param name="Pos">Position vector of body, with respect to origin at observer (or the geocenter), 
        ''' referred to ICRS axes, components in AU.</param>
        ''' <param name="TLight">Final light-time, in days.</param>
        ''' <returns><pre>
        '''    0 ... everything OK
        '''    1 ... algorithm failed to converge after 10 iterations
        ''' <![CDATA[>]]> 10 ... error is 10 + error from function 'SolarSystem'.
        ''' </pre></returns>
        ''' <remarks></remarks>
        Public Function LightTime(ByVal JdTdb As Double,
                                          ByVal SsObject As Object3,
                                          ByVal PosObs() As Double,
                                          ByVal TLight0 As Double,
                                          ByVal Accuracy As Accuracy,
                                          ByRef Pos() As Double,
                                          ByRef TLight As Double) As Short Implements INOVAS31.LightTime
            Dim VPos As New PosVector, rc As Short
            If Is64Bit() Then
                rc = LightTime64(JdTdb, O3IFromObject3(SsObject), ArrToPosVec(PosObs), TLight0, Accuracy, VPos, TLight)
            Else
                rc = LightTime32(JdTdb, O3IFromObject3(SsObject), ArrToPosVec(PosObs), TLight0, Accuracy, VPos, TLight)
            End If

            PosVecToArr(VPos, Pos)
            Return rc
        End Function

        ''' <summary>
        ''' Determines the angle of an object above or below the Earth's limb (horizon).
        ''' </summary>
        ''' <param name="PosObj">Position vector of observed object, with respect to origin at 
        ''' geocenter, components in AU.</param>
        ''' <param name="PosObs">Position vector of observer, with respect to origin at geocenter, 
        ''' components in AU.</param>
        ''' <param name="LimbAng">Angle of observed object above (+) or below (-) limb in degrees.</param>
        ''' <param name="NadirAng">Nadir angle of observed object as a fraction of apparent radius of limb: <![CDATA[<]]> 1.0 ... 
        ''' below the limb; = 1.0 ... on the limb;  <![CDATA[>]]> 1.0 ... above the limb</param>
        ''' <remarks>The geometric limb is computed, assuming the Earth to be an airless sphere (no 
        ''' refraction or oblateness is included).  The observer can be on or above the Earth.  
        ''' For an observer on the surface of the Earth, this function returns the approximate unrefracted 
        ''' altitude.</remarks>
        Public Sub LimbAngle(ByVal PosObj() As Double,
                             ByVal PosObs() As Double,
                             ByRef LimbAng As Double,
                             ByRef NadirAng As Double) Implements INOVAS31.LimbAngle
            If Is64Bit() Then
                LimbAngle64(ArrToPosVec(PosObj), ArrToPosVec(PosObs), LimbAng, NadirAng)
            Else
                LimbAngle32(ArrToPosVec(PosObj), ArrToPosVec(PosObs), LimbAng, NadirAng)
            End If
        End Sub

        ''' <summary>
        ''' Computes the local place of a solar system body.
        ''' </summary>
        ''' <param name="JdTt">TT Julian date for local place.</param>
        ''' <param name="SsBody">structure containing the body designation for the solar system body</param>
        ''' <param name="DeltaT">Difference TT-UT1 at 'JdTt', in seconds of time.</param>
        ''' <param name="Position">Specifies the position of the observer</param>
        ''' <param name="Accuracy">Specifies accuracy level</param>
        ''' <param name="Ra">Local right ascension in hours, referred to the 'local GCRS'.</param>
        ''' <param name="Dec">Local declination in degrees, referred to the 'local GCRS'.</param>
        ''' <param name="Dis">True distance from Earth to planet in AU.</param>
        ''' <returns><pre>
        '''    0 ... Everything OK
        '''    1 ... Invalid value of 'Where' in structure 'Location'; 
        ''' <![CDATA[>]]> 10 ... Error code from function 'Place'.
        ''' </pre></returns>
        ''' <remarks></remarks>
        Public Function LocalPlanet(ByVal JdTt As Double,
                                            ByVal SsBody As Object3,
                                            ByVal DeltaT As Double,
                                            ByVal Position As OnSurface,
                                            ByVal Accuracy As Accuracy,
                                            ByRef Ra As Double,
                                            ByRef Dec As Double,
                                            ByRef Dis As Double) As Short Implements INOVAS31.LocalPlanet
            If Is64Bit() Then
                Return LocalPlanet64(JdTt, O3IFromObject3(SsBody), DeltaT, Position, Accuracy, Ra, Dec, Dis)
            Else
                Return LocalPlanet32(JdTt, O3IFromObject3(SsBody), DeltaT, Position, Accuracy, Ra, Dec, Dis)
            End If
        End Function

        ''' <summary>
        '''  Computes the local place of a star at date 'JdTt', given its catalog mean place, proper motion, parallax, and radial velocity.
        ''' </summary>
        ''' <param name="JdTt">TT Julian date for local place. delta_t (double)</param>
        ''' <param name="DeltaT">Difference TT-UT1 at 'JdTt', in seconds of time.</param>
        ''' <param name="Star">catalog entry structure containing catalog data for the object in the ICRS</param>
        ''' <param name="Position">Structure specifying the position of the observer </param>
        ''' <param name="Accuracy">Specifies accuracy level.</param>
        ''' <param name="Ra">Local right ascension in hours, referred to the 'local GCRS'.</param>
        ''' <param name="Dec">Local declination in degrees, referred to the 'local GCRS'.</param>
        ''' <returns><pre>
        '''    0 ... Everything OK
        '''    1 ... Invalid value of 'Where' in structure 'Location'
        ''' > 10 ... Error code from function 'MakeObject'
        ''' > 20 ... Error code from function 'Place'.
        ''' </pre></returns>
        ''' <remarks></remarks>
        Public Function LocalStar(ByVal JdTt As Double,
                                          ByVal DeltaT As Double,
                                          ByVal Star As CatEntry3,
                                          ByVal Position As OnSurface,
                                          ByVal Accuracy As Accuracy,
                                          ByRef Ra As Double,
                                          ByRef Dec As Double) As Short Implements INOVAS31.LocalStar
            If Is64Bit() Then
                Return LocalStar64(JdTt, DeltaT, Star, Position, Accuracy, Ra, Dec)
            Else
                Return LocalStar32(JdTt, DeltaT, Star, Position, Accuracy, Ra, Dec)
            End If
        End Function

        ''' <summary>
        ''' Create a structure of type 'cat_entry' containing catalog data for a star or "star-like" object.
        ''' </summary>
        ''' <param name="StarName">Object name (50 characters maximum).</param>
        ''' <param name="Catalog">Three-character catalog identifier (e.g. HIP = Hipparcos, TY2 = Tycho-2)</param>
        ''' <param name="StarNum">Object number in the catalog.</param>
        ''' <param name="Ra">Right ascension of the object (hours).</param>
        ''' <param name="Dec">Declination of the object (degrees).</param>
        ''' <param name="PmRa">Proper motion in right ascension (milliarcseconds/year).</param>
        ''' <param name="PmDec">Proper motion in declination (milliarcseconds/year).</param>
        ''' <param name="Parallax">Parallax (milliarcseconds).</param>
        ''' <param name="RadVel">Radial velocity (kilometers/second).</param>
        ''' <param name="Star">CatEntry3 structure containing the input data</param>
        ''' <remarks></remarks>
        Public Sub MakeCatEntry(ByVal StarName As String,
                                ByVal Catalog As String,
                                ByVal StarNum As Integer,
                                ByVal Ra As Double,
                                ByVal Dec As Double,
                                ByVal PmRa As Double,
                                ByVal PmDec As Double,
                                ByVal Parallax As Double,
                                ByVal RadVel As Double,
                                ByRef Star As CatEntry3) Implements INOVAS31.MakeCatEntry
            If Is64Bit() Then
                MakeCatEntry64(StarName, Catalog, StarNum, Ra, Dec, PmRa, PmDec, Parallax, RadVel, Star)
            Else
                MakeCatEntry32(StarName, Catalog, StarNum, Ra, Dec, PmRa, PmDec, Parallax, RadVel, Star)
            End If
        End Sub

        ''' <summary>
        ''' Makes a structure of type 'InSpace' - specifying the position and velocity of an observer situated 
        ''' on a near-Earth spacecraft.
        ''' </summary>
        ''' <param name="ScPos">Geocentric position vector (x, y, z) in km.</param>
        ''' <param name="ScVel">Geocentric velocity vector (x_dot, y_dot, z_dot) in km/s.</param>
        ''' <param name="ObsSpace">InSpace structure containing the position and velocity of an observer situated 
        ''' on a near-Earth spacecraft</param>
        ''' <remarks></remarks>
        Public Sub MakeInSpace(ByVal ScPos() As Double,
                               ByVal ScVel() As Double,
                               ByRef ObsSpace As InSpace) Implements INOVAS31.MakeInSpace
            If Is64Bit() Then
                MakeInSpace64(ArrToPosVec(ScPos), ArrToVelVec(ScVel), ObsSpace)
            Else
                MakeInSpace32(ArrToPosVec(ScPos), ArrToVelVec(ScVel), ObsSpace)
            End If
        End Sub

        ''' <summary>
        ''' Makes a structure of type 'object' - specifying a celestial object - based on the input parameters.
        ''' </summary>
        ''' <param name="Type">Type of object: 0 ... major planet, Sun, or Moon;  1 ... minor planet; 
        ''' 2 ... object located outside the solar system (e.g. star, galaxy, nebula, etc.)</param>
        ''' <param name="Number">Body number: For 'Type' = 0: Mercury = 1,...,Pluto = 9, Sun = 10, Moon = 11; 
        ''' For 'Type' = 1: minor planet numberFor 'Type' = 2: set to 0 (zero)</param>
        ''' <param name="Name">Name of the object (50 characters maximum).</param>
        ''' <param name="StarData">Structure containing basic astrometric data for any celestial object 
        ''' located outside the solar system; the catalog data for a star</param>
        ''' <param name="CelObj">Structure containing the object definition</param>
        ''' <returns><pre>
        ''' 0 ... everything OK
        ''' 1 ... invalid value of 'Type'
        ''' 2 ... 'Number' out of range
        ''' </pre></returns>
        ''' <remarks></remarks>
        Public Function MakeObject(ByVal Type As ObjectType,
                                   ByVal Number As Short,
                                   ByVal Name As String,
                                   ByVal StarData As CatEntry3,
                                   ByRef CelObj As Object3) As Short Implements INOVAS31.MakeObject
            Dim O3I As New Object3Internal, rc As Short

            If Is64Bit() Then
                rc = MakeObject64(Type, Number, Name, StarData, O3I)
            Else
                rc = MakeObject32(Type, Number, Name, StarData, O3I)
            End If
            O3FromO3Internal(O3I, CelObj)
            Return rc
        End Function

        ''' <summary>
        '''  Makes a structure of type 'observer' - specifying the location of the observer.
        ''' </summary>
        ''' <param name="Where">Integer code specifying location of observer: 0: observer at geocenter; 
        ''' 1: observer on surface of earth; 2: observer on near-earth spacecraft</param>
        ''' <param name="ObsSurface">Structure containing data for an observer's location on the surface 
        ''' of the Earth; used when 'Where' = 1</param>
        ''' <param name="ObsSpace"> Structure containing an observer's location on a near-Earth spacecraft; 
        ''' used when 'Where' = 2 </param>
        ''' <param name="Obs">Structure specifying the location of the observer </param>
        ''' <returns><pre>
        ''' 0 ... everything OK
        ''' 1 ... input value of 'Where' is out-of-range.
        ''' </pre></returns>
        ''' <remarks></remarks>
        Public Function MakeObserver(ByVal Where As ObserverLocation,
                                     ByVal ObsSurface As OnSurface,
                                     ByVal ObsSpace As InSpace,
                                     ByRef Obs As Observer) As Short Implements INOVAS31.MakeObserver
            If Is64Bit() Then
                Return MakeObserver64(Where, ObsSurface, ObsSpace, Obs)
            Else
                Return MakeObserver32(Where, ObsSurface, ObsSpace, Obs)
            End If
        End Function

        ''' <summary>
        '''  Makes a structure of type 'observer' specifying an observer at the geocenter.
        ''' </summary>
        ''' <param name="ObsAtGeocenter">Structure specifying the location of the observer at the geocenter</param>
        ''' <remarks></remarks>
        Public Sub MakeObserverAtGeocenter(ByRef ObsAtGeocenter As Observer) Implements INOVAS31.MakeObserverAtGeocenter
            If Is64Bit() Then
                MakeObserverAtGeocenter64(ObsAtGeocenter)
            Else
                MakeObserverAtGeocenter32(ObsAtGeocenter)
            End If
        End Sub

        ''' <summary>
        '''  Makes a structure of type 'observer' specifying the position and velocity of an observer 
        ''' situated on a near-Earth spacecraft.
        ''' </summary>
        ''' <param name="ScPos">Geocentric position vector (x, y, z) in km.</param>
        ''' <param name="ScVel">Geocentric position vector (x, y, z) in km.</param>
        ''' <param name="ObsInSpace">Structure containing the position and velocity of an observer 
        ''' situated on a near-Earth spacecraft</param>
        ''' <remarks>Both input vectors are with respect to true equator and equinox of date.</remarks>
        Public Sub MakeObserverInSpace(ByVal ScPos() As Double,
                                       ByVal ScVel() As Double,
                                       ByRef ObsInSpace As Observer) Implements INOVAS31.MakeObserverInSpace
            If Is64Bit() Then
                MakeObserverInSpace64(ArrToPosVec(ScPos), ArrToVelVec(ScVel), ObsInSpace)
            Else
                MakeObserverInSpace32(ArrToPosVec(ScPos), ArrToVelVec(ScVel), ObsInSpace)
            End If
        End Sub

        ''' <summary>
        ''' Makes a structure of type 'observer' specifying the location of and weather for an observer 
        ''' on the surface of the Earth.
        ''' </summary>
        ''' <param name="Latitude">Geodetic (ITRS) latitude in degrees; north positive.</param>
        ''' <param name="Longitude">Geodetic (ITRS) longitude in degrees; east positive.</param>
        ''' <param name="Height">Height of the observer (meters).</param>
        ''' <param name="Temperature">Temperature (degrees Celsius).</param>
        ''' <param name="Pressure">Atmospheric pressure (millibars).</param>
        ''' <param name="ObsOnSurface">Structure containing the location of and weather for an observer on 
        ''' the surface of the Earth</param>
        ''' <remarks></remarks>
        Public Sub MakeObserverOnSurface(ByVal Latitude As Double,
                                         ByVal Longitude As Double,
                                         ByVal Height As Double,
                                         ByVal Temperature As Double,
                                         ByVal Pressure As Double,
                                         ByRef ObsOnSurface As Observer) Implements INOVAS31.MakeObserverOnSurface
            If Is64Bit() Then
                MakeObserverOnSurface64(Latitude, Longitude, Height, Temperature, Pressure, ObsOnSurface)
            Else
                MakeObserverOnSurface32(Latitude, Longitude, Height, Temperature, Pressure, ObsOnSurface)
            End If
        End Sub

        ''' <summary>
        ''' Makes a structure of type 'on_surface' - specifying the location of and weather for an 
        ''' observer on the surface of the Earth.
        ''' </summary>
        ''' <param name="Latitude">Geodetic (ITRS) latitude in degrees; north positive.</param>
        ''' <param name="Longitude">Geodetic (ITRS) latitude in degrees; north positive.</param>
        ''' <param name="Height">Height of the observer (meters).</param>
        ''' <param name="Temperature">Temperature (degrees Celsius).</param>
        ''' <param name="Pressure">Atmospheric pressure (millibars).</param>
        ''' <param name="ObsSurface">Structure containing the location of and weather for an 
        ''' observer on the surface of the Earth.</param>
        ''' <remarks></remarks>
        Public Sub MakeOnSurface(ByVal Latitude As Double,
                                 ByVal Longitude As Double,
                                 ByVal Height As Double,
                                 ByVal Temperature As Double,
                                 ByVal Pressure As Double,
                                 ByRef ObsSurface As OnSurface) Implements INOVAS31.MakeOnSurface
            If Is64Bit() Then
                MakeOnSurface64(Latitude, Longitude, Height, Temperature, Pressure, ObsSurface)
            Else
                MakeOnSurface32(Latitude, Longitude, Height, Temperature, Pressure, ObsSurface)
            End If
        End Sub

        ''' <summary>
        ''' Compute the mean obliquity of the ecliptic.
        ''' </summary>
        ''' <param name="JdTdb">TDB Julian Date.</param>
        ''' <returns>Mean obliquity of the ecliptic in arcseconds.</returns>
        ''' <remarks></remarks>
        Public Function MeanObliq(ByVal JdTdb As Double) As Double Implements INOVAS31.MeanObliq
            If Is64Bit() Then
                Return MeanObliq64(JdTdb)
            Else
                Return MeanObliq32(JdTdb)
            End If

        End Function

        ''' <summary>
        ''' Computes the ICRS position of a star, given its apparent place at date 'JdTt'.  
        ''' Proper motion, parallax and radial velocity are assumed to be zero.
        ''' </summary>
        ''' <param name="JdTt">TT Julian date of apparent place.</param>
        ''' <param name="Ra">Apparent right ascension in hours, referred to true equator and equinox of date.</param>
        ''' <param name="Dec">Apparent declination in degrees, referred to true equator and equinox of date.</param>
        ''' <param name="Accuracy">Specifies accuracy level</param>
        ''' <param name="IRa">ICRS right ascension in hours.</param>
        ''' <param name="IDec">ICRS declination in degrees.</param>
        ''' <returns><pre>
        '''    0 ... Everything OK
        '''    1 ... Iterative process did not converge after 30 iterations; 
        ''' > 10 ... Error from function 'Vector2RaDec'
        ''' > 20 ... Error from function 'AppStar'.
        ''' </pre></returns>
        ''' <remarks></remarks>
        Public Function MeanStar(ByVal JdTt As Double,
                                         ByVal Ra As Double,
                                         ByVal Dec As Double,
                                         ByVal Accuracy As Accuracy,
                                         ByRef IRa As Double,
                                         ByRef IDec As Double) As Short Implements INOVAS31.MeanStar
            If Is64Bit() Then
                Return MeanStar64(JdTt, Ra, Dec, Accuracy, IRa, IDec)
            Else
                Return MeanStar32(JdTt, Ra, Dec, Accuracy, IRa, IDec)
            End If
        End Function

        ''' <summary>
        ''' Normalize angle into the range 0 <![CDATA[<=]]> angle <![CDATA[<]]> (2 * pi).
        ''' </summary>
        ''' <param name="Angle">Input angle (radians).</param>
        ''' <returns>The input angle, normalized as described above (radians).</returns>
        ''' <remarks></remarks>
        Public Function NormAng(ByVal Angle As Double) As Double Implements INOVAS31.NormAng
            If Is64Bit() Then
                Return NormAng64(Angle)
            Else
                Return NormAng32(Angle)
            End If
        End Function

        ''' <summary>
        ''' Nutates equatorial rectangular coordinates from mean equator and equinox of epoch to true equator and equinox of epoch.
        ''' </summary>
        ''' <param name="JdTdb">TDB Julian date of epoch.</param>
        ''' <param name="Direction">Flag determining 'direction' of transformation; direction  = 0 
        ''' transformation applied, mean to true; direction != 0 inverse transformation applied, true to mean.</param>
        ''' <param name="Accuracy">Selection for accuracy</param>
        ''' <param name="Pos">Position vector, geocentric equatorial rectangular coordinates, referred to 
        ''' mean equator and equinox of epoch.</param>
        ''' <param name="Pos2">Position vector, geocentric equatorial rectangular coordinates, referred to 
        ''' true equator and equinox of epoch.</param>
        ''' <remarks> Inverse transformation may be applied by setting flag 'direction'</remarks>
        Public Sub Nutation(ByVal JdTdb As Double,
                                    ByVal Direction As NutationDirection,
                                    ByVal Accuracy As Accuracy,
                                    ByVal Pos() As Double,
                                    ByRef Pos2() As Double) Implements INOVAS31.Nutation
            Dim VPOs2 As New PosVector

            If Is64Bit() Then
                Nutation64(JdTdb, Direction, Accuracy, ArrToPosVec(Pos), VPOs2)
            Else
                Nutation32(JdTdb, Direction, Accuracy, ArrToPosVec(Pos), VPOs2)
            End If
            PosVecToArr(VPOs2, Pos2)
        End Sub

        ''' <summary>
        ''' Returns the values for nutation in longitude and nutation in obliquity for a given TDB Julian date.
        ''' </summary>
        ''' <param name="t">TDB time in Julian centuries since J2000.0</param>
        ''' <param name="Accuracy">Selection for accuracy</param>
        ''' <param name="DPsi">Nutation in longitude in arcseconds.</param>
        ''' <param name="DEps">Nutation in obliquity in arcseconds.</param>
        ''' <remarks>The nutation model selected depends upon the input value of 'Accuracy'.  See notes below for important details.
        ''' <para>
        ''' This function selects the nutation model depending first upon the input value of 'Accuracy'.  
        ''' If 'Accuracy' = 0 (full accuracy), the IAU 2000A nutation model is used.  If 'Accuracy' = 1 
        ''' a specially truncated (and therefore faster) version of IAU 2000A, called 'NU2000K' is used.
        ''' </para>
        ''' </remarks>
        Public Sub NutationAngles(ByVal t As Double,
                                          ByVal Accuracy As Accuracy,
                                          ByRef DPsi As Double,
                                          ByRef DEps As Double) Implements INOVAS31.NutationAngles
            If Is64Bit() Then
                NutationAngles64(t, Accuracy, DPsi, DEps)
            Else
                NutationAngles32(t, Accuracy, DPsi, DEps)
            End If
        End Sub

        ''' <summary>
        ''' Computes the apparent direction of a star or solar system body at a specified time 
        ''' and in a specified coordinate system.
        ''' </summary>
        ''' <param name="JdTt">TT Julian date for place.</param>
        ''' <param name="CelObject"> Specifies the celestial object of interest</param>
        ''' <param name="Location">Specifies the location of the observer</param>
        ''' <param name="DeltaT"> Difference TT-UT1 at 'JdTt', in seconds of time.</param>
        ''' <param name="CoordSys">Code specifying coordinate system of the output position. 0 ... GCRS or 
        ''' "local GCRS"; 1 ... true equator and equinox of date; 2 ... true equator and CIO of date; 
        ''' 3 ... astrometric coordinates, i.e., without light deflection or aberration.</param>
        ''' <param name="Accuracy">Selection for accuracy</param>
        ''' <param name="Output">Structure specifying object's place on the sky at time 'JdTt', 
        ''' with respect to the specified output coordinate system</param>
        ''' <returns>
        ''' <pre>
        ''' = 0         ... No problems.
        ''' = 1         ... invalid value of 'CoordSys'
        ''' = 2         ... invalid value of 'Accuracy'
        ''' = 3         ... Earth is the observed object, and the observer is either at the geocenter or on the Earth's surface (not permitted)
        ''' > 10, <![CDATA[<]]> 40  ... 10 + error from function 'Ephemeris'
        ''' > 40, <![CDATA[<]]> 50  ... 40 + error from function 'GeoPosVel'
        ''' > 50, <![CDATA[<]]> 70  ... 50 + error from function 'LightTime'
        ''' > 70, <![CDATA[<]]> 80  ... 70 + error from function 'GravDef'
        ''' > 80, <![CDATA[<]]> 90  ... 80 + error from function 'CioLocation'
        ''' > 90, <![CDATA[<]]> 100 ... 90 + error from function 'CioBasis'
        ''' </pre>
        ''' </returns>
        ''' Values of 'location->where' and 'CoordSys' dictate the various standard kinds of place:
        ''' <pre>
        '''     Location->Where = 0 and CoordSys = 1: apparent place
        '''     Location->Where = 1 and CoordSys = 1: topocentric place
        '''     Location->Where = 0 and CoordSys = 0: virtual place
        '''     Location->Where = 1 and CoordSys = 0: local place
        '''     Location->Where = 0 and CoordSys = 3: astrometric place
        '''     Location->Where = 1 and CoordSys = 3: topocentric astrometric place
        ''' </pre>
        ''' <para>Input value of 'DeltaT' is used only when 'Location->Where' equals 1 or 2 (observer is 
        ''' on surface of Earth or in a near-Earth satellite). </para>
        ''' <remarks>
        ''' </remarks>
        Public Function Place(ByVal JdTt As Double,
                                   ByVal CelObject As Object3,
                                   ByVal Location As Observer,
                                   ByVal DeltaT As Double,
                                   ByVal CoordSys As CoordSys,
                                   ByVal Accuracy As Accuracy,
                                   ByRef Output As SkyPos) As Short Implements INOVAS31.Place
            If Is64Bit() Then
                Return Place64(JdTt, O3IFromObject3(CelObject), Location, DeltaT, CoordSys, Accuracy, Output)
            Else
                Return Place32(JdTt, O3IFromObject3(CelObject), Location, DeltaT, CoordSys, Accuracy, Output)
            End If
        End Function

        ''' <summary>
        '''  Precesses equatorial rectangular coordinates from one epoch to another.
        ''' </summary>
        ''' <param name="JdTdb1">TDB Julian date of first epoch.  See remarks below.</param>
        ''' <param name="Pos1">Position vector, geocentric equatorial rectangular coordinates, referred to mean dynamical equator and equinox of first epoch.</param>
        ''' <param name="JdTdb2">TDB Julian date of second epoch.  See remarks below.</param>
        ''' <param name="Pos2">Position vector, geocentric equatorial rectangular coordinates, referred to mean dynamical equator and equinox of second epoch.</param>
        ''' <returns><pre>
        ''' 0 ... everything OK
        ''' 1 ... Precession not to or from J2000.0; 'JdTdb1' or 'JdTdb2' not 2451545.0.
        ''' </pre></returns>
        ''' <remarks> One of the two epochs must be J2000.0.  The coordinates are referred to the mean dynamical equator and equinox of the two respective epochs.</remarks>
        Public Function Precession(ByVal JdTdb1 As Double,
                                           ByVal Pos1() As Double,
                                           ByVal JdTdb2 As Double,
                                           ByRef Pos2() As Double) As Short Implements INOVAS31.Precession

            Dim VPos2 As New PosVector, rc As Short

            If Is64Bit() Then
                rc = Precession64(JdTdb1, ArrToPosVec(Pos1), JdTdb2, VPos2)
            Else
                rc = Precession32(JdTdb1, ArrToPosVec(Pos1), JdTdb2, VPos2)
            End If
            PosVecToArr(VPos2, Pos2)
            Return rc
        End Function

        ''' <summary>
        ''' Applies proper motion, including foreshortening effects, to a star's position.
        ''' </summary>
        ''' <param name="JdTdb1">TDB Julian date of first epoch.</param>
        ''' <param name="Pos">Position vector at first epoch.</param>
        ''' <param name="Vel">Velocity vector at first epoch.</param>
        ''' <param name="JdTdb2">TDB Julian date of second epoch.</param>
        ''' <param name="Pos2">Position vector at second epoch.</param>
        ''' <remarks></remarks>
        Public Sub ProperMotion(ByVal JdTdb1 As Double,
                                        ByVal Pos() As Double,
                                        ByVal Vel() As Double,
                                        ByVal JdTdb2 As Double,
                                        ByRef Pos2() As Double) Implements INOVAS31.ProperMotion
            Dim VPos2 As New PosVector

            If Is64Bit() Then
                ProperMotion64(JdTdb1, ArrToPosVec(Pos), ArrToVelVec(Vel), JdTdb2, VPos2)
            Else
                ProperMotion32(JdTdb1, ArrToPosVec(Pos), ArrToVelVec(Vel), JdTdb2, VPos2)
            End If

            PosVecToArr(VPos2, Pos2)
        End Sub

        ''' <summary>
        ''' Converts equatorial spherical coordinates to a vector (equatorial rectangular coordinates).
        ''' </summary>
        ''' <param name="Ra">Right ascension (hours).</param>
        ''' <param name="Dec">Declination (degrees).</param>
        ''' <param name="Dist">Distance in AU</param>
        ''' <param name="Vector">Position vector, equatorial rectangular coordinates (AU).</param>
        ''' <remarks></remarks>
        Public Sub RaDec2Vector(ByVal Ra As Double,
                                        ByVal Dec As Double,
                                        ByVal Dist As Double,
                                        ByRef Vector() As Double) Implements INOVAS31.RaDec2Vector
            Dim VVector As New PosVector

            If Is64Bit() Then
                RaDec2Vector64(Ra, Dec, Dist, VVector)
            Else
                RaDec2Vector32(Ra, Dec, Dist, VVector)
            End If
            PosVecToArr(VVector, Vector)
        End Sub

        ''' <summary>
        ''' Predicts the radial velocity of the observed object as it would be measured by spectroscopic means.
        ''' </summary>
        ''' <param name="CelObject">Specifies the celestial object of interest</param>
        ''' <param name="Pos"> Geometric position vector of object with respect to observer, corrected for light-time, in AU.</param>
        ''' <param name="Vel">Velocity vector of object with respect to solar system barycenter, in AU/day.</param>
        ''' <param name="VelObs">Velocity vector of observer with respect to solar system barycenter, in AU/day.</param>
        ''' <param name="DObsGeo">Distance from observer to geocenter, in AU.</param>
        ''' <param name="DObsSun">Distance from observer to Sun, in AU.</param>
        ''' <param name="DObjSun">Distance from object to Sun, in AU.</param>
        ''' <param name="Rv">The observed radial velocity measure times the speed of light, in kilometers/second.</param>
        ''' <remarks> Radial velocity is here defined as the radial velocity measure (z) times the speed of light.  
        ''' For a solar system body, it applies to a fictitious emitter at the center of the observed object, 
        ''' assumed massless (no gravitational red shift), and does not in general apply to reflected light.  
        ''' For stars, it includes all effects, such as gravitational red shift, contained in the catalog 
        ''' barycentric radial velocity measure, a scalar derived from spectroscopy.  Nearby stars with a known 
        ''' kinematic velocity vector (obtained independently of spectroscopy) can be treated like 
        ''' solar system objects.</remarks>
        Public Sub RadVel(ByVal CelObject As Object3,
                                  ByVal Pos() As Double,
                                  ByVal Vel() As Double,
                                  ByVal VelObs() As Double,
                                  ByVal DObsGeo As Double,
                                  ByVal DObsSun As Double,
                                  ByVal DObjSun As Double,
                                  ByRef Rv As Double) Implements INOVAS31.RadVel
            If Is64Bit() Then
                RadVel64(O3IFromObject3(CelObject), ArrToPosVec(Pos), ArrToVelVec(Vel), ArrToVelVec(VelObs), DObsGeo, DObsSun, DObjSun, Rv)
            Else
                RadVel32(O3IFromObject3(CelObject), ArrToPosVec(Pos), ArrToVelVec(Vel), ArrToVelVec(VelObs), DObsGeo, DObsSun, DObjSun, Rv)
            End If
        End Sub

        ''' <summary>
        ''' Computes atmospheric refraction in zenith distance. 
        ''' </summary>
        ''' <param name="Location">Structure containing observer's location.</param>
        ''' <param name="RefOption">1 ... Use 'standard' atmospheric conditions; 2 ... Use atmospheric 
        ''' parameters input in the 'Location' structure.</param>
        ''' <param name="ZdObs">Observed zenith distance, in degrees.</param>
        ''' <returns>Atmospheric refraction, in degrees.</returns>
        ''' <remarks>This version computes approximate refraction for optical wavelengths. This function 
        ''' can be used for planning observations or telescope pointing, but should not be used for the 
        ''' reduction of precise observations.</remarks>
        Public Function Refract(ByVal Location As OnSurface,
                                        ByVal RefOption As RefractionOption,
                                        ByVal ZdObs As Double) As Double Implements INOVAS31.Refract
            If Is64Bit() Then
                Return Refract64(Location, RefOption, ZdObs)
            Else
                Return Refract32(Location, RefOption, ZdObs)
            End If
        End Function

        ''' <summary>
        ''' Computes the Greenwich sidereal time, either mean or apparent, at Julian date 'JdHigh' + 'JdLow'.
        ''' </summary>
        ''' <param name="JdHigh">High-order part of UT1 Julian date.</param>
        ''' <param name="JdLow">Low-order part of UT1 Julian date.</param>
        ''' <param name="DeltaT"> Difference TT-UT1 at 'JdHigh'+'JdLow', in seconds of time.</param>
        ''' <param name="GstType">0 ... compute Greenwich mean sidereal time; 1 ... compute Greenwich apparent sidereal time</param>
        ''' <param name="Method">Selection for method: 0 ... CIO-based method; 1 ... equinox-based method</param>
        ''' <param name="Accuracy">Selection for accuracy</param>
        ''' <param name="Gst">Greenwich apparent sidereal time, in hours.</param>
        ''' <returns><pre>
        '''          0 ... everything OK
        '''          1 ... invalid value of 'Accuracy'
        '''          2 ... invalid value of 'Method'
        ''' > 10, <![CDATA[<]]> 30 ... 10 + error from function 'CioRai'
        ''' </pre></returns>
        ''' <remarks> The Julian date may be split at any point, but for highest precision, set 'JdHigh' 
        ''' to be the integral part of the Julian date, and set 'JdLow' to be the fractional part.</remarks>
        Public Function SiderealTime(ByVal JdHigh As Double,
                                            ByVal JdLow As Double,
                                            ByVal DeltaT As Double,
                                            ByVal GstType As GstType,
                                            ByVal Method As Method,
                                            ByVal Accuracy As Accuracy,
                                            ByRef Gst As Double) As Short Implements INOVAS31.SiderealTime

            If Is64Bit() Then
                Return SiderealTime64(JdHigh, JdLow, DeltaT, GstType, Method, Accuracy, Gst)
            Else
                Return SiderealTime32(JdHigh, JdLow, DeltaT, GstType, Method, Accuracy, Gst)
            End If
        End Function

        ''' <summary>
        ''' Transforms a vector from one coordinate system to another with same origin and axes rotated about the z-axis.
        ''' </summary>
        ''' <param name="Angle"> Angle of coordinate system rotation, positive counterclockwise when viewed from +z, in degrees.</param>
        ''' <param name="Pos1">Position vector.</param>
        ''' <param name="Pos2">Position vector expressed in new coordinate system rotated about z by 'angle'.</param>
        ''' <remarks></remarks>
        Public Sub Spin(ByVal Angle As Double,
                                ByVal Pos1() As Double,
                                ByRef Pos2() As Double) Implements INOVAS31.Spin
            Dim VPOs2 As New PosVector
            If Is64Bit() Then
                Spin64(Angle, ArrToPosVec(Pos1), VPOs2)
            Else
                Spin32(Angle, ArrToPosVec(Pos1), VPOs2)
            End If

            PosVecToArr(VPOs2, Pos2)
        End Sub

        ''' <summary>
        ''' Converts angular quantities for stars to vectors.
        ''' </summary>
        ''' <param name="Star">Catalog entry structure containing ICRS catalog data </param>
        ''' <param name="Pos">Position vector, equatorial rectangular coordinates, components in AU.</param>
        ''' <param name="Vel">Velocity vector, equatorial rectangular coordinates, components in AU/Day.</param>
        ''' <remarks></remarks>
        Public Sub StarVectors(ByVal Star As CatEntry3,
                                       ByRef Pos() As Double,
                                       ByRef Vel() As Double) Implements INOVAS31.StarVectors
            Dim VPos As New PosVector, VVel As New VelVector
            If Is64Bit() Then
                StarVectors64(Star, VPos, VVel)
            Else
                StarVectors32(Star, VPos, VVel)
            End If

            PosVecToArr(VPos, Pos)
            VelVecToArr(VVel, Vel)
        End Sub

        ''' <summary>
        ''' Computes the Terrestrial Time (TT) or Terrestrial Dynamical Time (TDT) Julian date corresponding 
        ''' to a Barycentric Dynamical Time (TDB) Julian date.
        ''' </summary>
        ''' <param name="TdbJd">TDB Julian date.</param>
        ''' <param name="TtJd">TT Julian date.</param>
        ''' <param name="SecDiff">Difference 'tdb_jd'-'tt_jd', in seconds.</param>
        ''' <remarks>Expression used in this function is a truncated form of a longer and more precise 
        ''' series given in: Explanatory Supplement to the Astronomical Almanac, pp. 42-44 and p. 316. 
        ''' The result is good to about 10 microseconds.</remarks>
        Public Sub Tdb2Tt(ByVal TdbJd As Double,
                                  ByRef TtJd As Double,
                                  ByRef SecDiff As Double) Implements INOVAS31.Tdb2Tt
            If Is64Bit() Then
                Tdb2Tt64(TdbJd, TtJd, SecDiff)
            Else
                Tdb2Tt32(TdbJd, TtJd, SecDiff)
            End If
        End Sub

        ''' <summary>
        ''' This function rotates a vector from the terrestrial to the celestial system. 
        ''' </summary>
        ''' <param name="JdHigh">High-order part of UT1 Julian date.</param>
        ''' <param name="JdLow">Low-order part of UT1 Julian date.</param>
        ''' <param name="DeltaT">Value of Delta T (= TT - UT1) at the input UT1 Julian date.</param>
        ''' <param name="Method"> Selection for method: 0 ... CIO-based method; 1 ... equinox-based method</param>
        ''' <param name="Accuracy">Selection for accuracy</param>
        ''' <param name="OutputOption">0 ... The output vector is referred to GCRS axes; 1 ... The output 
        ''' vector is produced with respect to the equator and equinox of date.</param>
        ''' <param name="xp">Conventionally-defined X coordinate of celestial intermediate pole with respect to 
        ''' ITRF pole, in arcseconds.</param>
        ''' <param name="yp">Conventionally-defined Y coordinate of celestial intermediate pole with respect to 
        ''' ITRF pole, in arcseconds.</param>
        ''' <param name="VecT">Position vector, geocentric equatorial rectangular coordinates, referred to ITRF 
        ''' axes (terrestrial system) in the normal case where 'option' = 0.</param>
        ''' <param name="VecC"> Position vector, geocentric equatorial rectangular coordinates, referred to GCRS 
        ''' axes (celestial system) or with respect to the equator and equinox of date, depending on 'Option'.</param>
        ''' <returns><pre>
        '''    0 ... everything is ok
        '''    1 ... invalid value of 'Accuracy'
        '''    2 ... invalid value of 'Method'
        ''' > 10 ... 10 + error from function 'CioLocation'
        ''' > 20 ... 20 + error from function 'CioBasis'
        ''' </pre></returns>
        ''' <remarks>'x' = 'y' = 0 means no polar motion transformation.
        ''' <para>
        ''' The 'option' flag only works for the equinox-based method.
        '''</para></remarks>
        Public Function Ter2Cel(ByVal JdHigh As Double,
                                        ByVal JdLow As Double,
                                        ByVal DeltaT As Double,
                                        ByVal Method As Method,
                                        ByVal Accuracy As Accuracy,
                                        ByVal OutputOption As OutputVectorOption,
                                        ByVal xp As Double,
                                        ByVal yp As Double,
                                        ByVal VecT() As Double,
                                        ByRef VecC() As Double) As Short Implements INOVAS31.Ter2Cel
            Dim VVecC As New PosVector, rc As Short
            If Is64Bit() Then
                rc = Ter2Cel64(JdHigh, JdLow, DeltaT, Method, Accuracy, OutputOption, xp, yp, ArrToPosVec(VecT), VVecC)
            Else
                rc = Ter2Cel32(JdHigh, JdLow, DeltaT, Method, Accuracy, OutputOption, xp, yp, ArrToPosVec(VecT), VVecC)
            End If

            PosVecToArr(VVecC, VecC)
            Return rc
        End Function

        ''' <summary>
        ''' Computes the position and velocity vectors of a terrestrial observer with respect to the center of the Earth.
        ''' </summary>
        ''' <param name="Location">Structure containing observer's location </param>
        ''' <param name="St">Local apparent sidereal time at reference meridian in hours.</param>
        ''' <param name="Pos">Position vector of observer with respect to center of Earth, equatorial 
        ''' rectangular coordinates, referred to true equator and equinox of date, components in AU.</param>
        ''' <param name="Vel">Velocity vector of observer with respect to center of Earth, equatorial rectangular 
        ''' coordinates, referred to true equator and equinox of date, components in AU/day.</param>
        ''' <remarks>
        ''' If reference meridian is Greenwich and st=0, 'pos' is effectively referred to equator and Greenwich.
        ''' <para> This function ignores polar motion, unless the observer's longitude and latitude have been 
        ''' corrected for it, and variation in the length of day (angular velocity of earth).</para>
        ''' <para>The true equator and equinox of date do not form an inertial system.  Therefore, with respect 
        ''' to an inertial system, the very small velocity component (several meters/day) due to the precession 
        ''' and nutation of the Earth's axis is not accounted for here.</para>
        ''' </remarks>
        Public Sub Terra(ByVal Location As OnSurface,
                                 ByVal St As Double,
                                 ByRef Pos() As Double,
                                 ByRef Vel() As Double) Implements INOVAS31.Terra
            Dim VPos As New PosVector, VVel As New VelVector
            If Is64Bit() Then
                Terra64(Location, St, VPos, VVel)
            Else
                Terra32(Location, St, VPos, VVel)
            End If

            PosVecToArr(VPos, Pos)
            VelVecToArr(VVel, Vel)
        End Sub

        ''' <summary>
        ''' Computes the topocentric place of a solar system body.
        ''' </summary>
        ''' <param name="JdTt">TT Julian date for topocentric place.</param>
        ''' <param name="SsBody">structure containing the body designation for the solar system body</param>
        ''' <param name="DeltaT">Difference TT-UT1 at 'JdTt', in seconds of time.</param>
        ''' <param name="Position">Specifies the position of the observer</param>
        ''' <param name="Accuracy">Selection for accuracy</param>
        ''' <param name="Ra">Apparent right ascension in hours, referred to true equator and equinox of date.</param>
        ''' <param name="Dec">Apparent declination in degrees, referred to true equator and equinox of date.</param>
        ''' <param name="Dis">True distance from Earth to planet at 'JdTt' in AU.</param>
        ''' <returns><pre>
        ''' =  0 ... Everything OK.
        ''' =  1 ... Invalid value of 'Where' in structure 'Location'.
        ''' > 10 ... Error code from function 'Place'.
        '''</pre></returns>
        ''' <remarks></remarks>
        Public Function TopoPlanet(ByVal JdTt As Double,
                                           ByVal SsBody As Object3,
                                           ByVal DeltaT As Double,
                                           ByVal Position As OnSurface,
                                           ByVal Accuracy As Accuracy,
                                           ByRef Ra As Double,
                                           ByRef Dec As Double,
                                           ByRef Dis As Double) As Short Implements INOVAS31.TopoPlanet
            If Is64Bit() Then
                Return TopoPlanet64(JdTt, O3IFromObject3(SsBody), DeltaT, Position, Accuracy, Ra, Dec, Dis)
            Else
                Return TopoPlanet32(JdTt, O3IFromObject3(SsBody), DeltaT, Position, Accuracy, Ra, Dec, Dis)
            End If
        End Function

        ''' <summary>
        ''' Computes the topocentric place of a star at date 'JdTt', given its catalog mean place, proper motion, parallax, and radial velocity.
        ''' </summary>
        ''' <param name="JdTt">TT Julian date for topocentric place.</param>
        ''' <param name="DeltaT">Difference TT-UT1 at 'JdTt', in seconds of time.</param>
        ''' <param name="Star">Catalog entry structure containing catalog data for the object in the ICRS</param>
        ''' <param name="Position">Specifies the position of the observer</param>
        ''' <param name="Accuracy">Code specifying the relative accuracy of the output position.</param>
        ''' <param name="Ra"> Topocentric right ascension in hours, referred to true equator and equinox of date 'JdTt'.</param>
        ''' <param name="Dec">Topocentric declination in degrees, referred to true equator and equinox of date 'JdTt'.</param>
        ''' <returns><pre>
        ''' =  0 ... Everything OK.
        ''' =  1 ... Invalid value of 'Where' in structure 'Location'.
        ''' > 10 ... Error code from function 'MakeObject'.
        ''' > 20 ... Error code from function 'Place'.
        ''' </pre></returns>
        ''' <remarks></remarks>
        Public Function TopoStar(ByVal JdTt As Double,
                                        ByVal DeltaT As Double,
                                        ByVal Star As CatEntry3,
                                        ByVal Position As OnSurface,
                                        ByVal Accuracy As Accuracy,
                                        ByRef Ra As Double,
                                        ByRef Dec As Double) As Short Implements INOVAS31.TopoStar

            Dim rc As Short
            Try
                TL.LogMessage("TopoStar", "JD Accuracy:            " & JdTt & " " & Accuracy.ToString)
                TL.LogMessage("TopoStar", "  Star.RA:              " & Utl.HoursToHMS(Star.RA, ":", ":", "", 3))
                TL.LogMessage("TopoStar", "  Dec:                  " & Utl.DegreesToDMS(Star.Dec, ":", ":", "", 3))
                TL.LogMessage("TopoStar", "  Catalog:              " & Star.Catalog)
                TL.LogMessage("TopoStar", "  Parallax:             " & Star.Parallax)
                TL.LogMessage("TopoStar", "  ProMoDec:             " & Star.ProMoDec)
                TL.LogMessage("TopoStar", "  ProMoRA:              " & Star.ProMoRA)
                TL.LogMessage("TopoStar", "  RadialVelocity:       " & Star.RadialVelocity)
                TL.LogMessage("TopoStar", "  StarName:             " & Star.StarName)
                TL.LogMessage("TopoStar", "  StarNumber:           " & Star.StarNumber)
                TL.LogMessage("TopoStar", "  Position.Height:      " & Position.Height)
                TL.LogMessage("TopoStar", "  Position.Latitude:    " & Position.Latitude)
                TL.LogMessage("TopoStar", "  Position.Longitude:   " & Position.Longitude)
                TL.LogMessage("TopoStar", "  Position.Pressure:    " & Position.Pressure)
                TL.LogMessage("TopoStar", "  Position.Temperature: " & Position.Temperature)
                If Is64Bit() Then
                    rc = TopoStar64(JdTt, DeltaT, Star, Position, Accuracy, Ra, Dec)
                    TL.LogMessage("TopoStar", "  64bit - Return Code: " & rc & ", RA Dec: " & Utl.HoursToHMS(Ra, ":", ":", "", 3) & " " & Utl.DegreesToDMS(Dec, ":", ":", "", 3))
                    Return rc
                Else
                    rc = TopoStar32(JdTt, DeltaT, Star, Position, Accuracy, Ra, Dec)
                    TL.LogMessage("TopoStar", "  32bit - Return Code: " & rc & ", RA Dec: " & Utl.HoursToHMS(Ra, ":", ":", "", 3) & " " & Utl.DegreesToDMS(Dec, ":", ":", "", 3))
                    Return rc
                End If
            Catch ex As Exception
                TL.LogMessageCrLf("TopoStar", "Exception: " & ex.ToString)
            End Try
        End Function

        ''' <summary>
        '''  To transform a star's catalog quantities for a change of epoch and/or equator and equinox.
        ''' </summary>
        ''' <param name="TransformOption">Transformation option</param>
        ''' <param name="DateInCat">TT Julian date, or year, of input catalog data.</param>
        ''' <param name="InCat">An entry from the input catalog, with units as given in the struct definition </param>
        ''' <param name="DateNewCat">TT Julian date, or year, of transformed catalog data.</param>
        ''' <param name="NewCatId">Three-character abbreviated name of the transformed catalog. </param>
        ''' <param name="NewCat"> The transformed catalog entry, with units as given in the struct definition </param>
        ''' <returns>
        ''' <pre>
        ''' = 0 ... Everything OK.
        ''' = 1 ... Invalid value of an input date for option 2 or 3 (see Note 1 below).
        ''' = 2 ... Catalogue ID exceeds three characters
        ''' </pre></returns>
        ''' <remarks>Also used to rotate catalog quantities on the dynamical equator and equinox of J2000.0 to the ICRS or vice versa.
        ''' <para>1. 'DateInCat' and 'DateNewCat' may be specified either as a Julian date (e.g., 2433282.5) or 
        ''' a Julian year and fraction (e.g., 1950.0).  Values less than 10000 are assumed to be years. 
        ''' For 'TransformOption' = 2 or 'TransformOption' = 3, either 'DateInCat' or 'DateNewCat' must be 2451545.0 or 
        ''' 2000.0 (J2000.0).  For 'TransformOption' = 4 and 'TransformOption' = 5, 'DateInCat' and 'DateNewCat' are ignored.</para>
        ''' <para>2. 'TransformOption' = 1 updates the star's data to account for the star's space motion between the first 
        ''' and second dates, within a fixed reference frame. 'TransformOption' = 2 applies a rotation of the reference 
        ''' frame corresponding to precession between the first and second dates, but leaves the star fixed in 
        ''' space. 'TransformOption' = 3 provides both transformations. 'TransformOption' = 4 and 'TransformOption' = 5 provide a a 
        ''' fixed rotation about very small angles (<![CDATA[<]]>0.1 arcsecond) to take data from the dynamical system 
        ''' of J2000.0 to the ICRS ('TransformOption' = 4) or vice versa ('TransformOption' = 5).</para>
        '''<para>3. For 'TransformOption' = 1, input data can be in any fixed reference system. for 'TransformOption' = 2 or 
        ''' 'TransformOption' = 3, this function assumes the input data is in the dynamical system and produces output 
        ''' in the dynamical system.  for 'TransformOption' = 4, the input data must be on the dynamical equator and 
        ''' equinox of J2000.0.  for 'TransformOption' = 5, the input data must be in the ICRS.</para>
        '''<para>4. This function cannot be properly used to bring data from old star catalogs into the 
        ''' modern system, because old catalogs were compiled using a set of constants that are incompatible 
        ''' with modern values.  In particular, it should not be used for catalogs whose positions and 
        ''' proper motions were derived by assuming a precession constant significantly different from 
        ''' the value implicit in function 'precession'.</para></remarks>
        Public Function TransformCat(ByVal TransformOption As TransformationOption3,
                                             ByVal DateInCat As Double,
                                             ByVal InCat As CatEntry3,
                                             ByVal DateNewCat As Double,
                                             ByVal NewCatId As String,
                                             ByRef NewCat As CatEntry3) As Short Implements INOVAS31.TransformCat
            If Is64Bit() Then
                Return TransformCat64(TransformOption, DateInCat, InCat, DateNewCat, NewCatId, NewCat)
            Else
                Return TransformCat32(TransformOption, DateInCat, InCat, DateNewCat, NewCatId, NewCat)
            End If
        End Function

        ''' <summary>
        ''' Convert Hipparcos catalog data at epoch J1991.25 to epoch J2000.0, for use within NOVAS.
        ''' </summary>
        ''' <param name="Hipparcos">An entry from the Hipparcos catalog, at epoch J1991.25, with all members 
        ''' having Hipparcos catalog units.  See Note 1 below </param>
        ''' <param name="Hip2000">The transformed input entry, at epoch J2000.0.  See Note 2 below</param>
        ''' <remarks>To be used only for Hipparcos or Tycho stars with linear space motion.  Both input and 
        ''' output data is in the ICRS.
        ''' <para>
        ''' 1. Input (Hipparcos catalog) epoch and units:
        ''' <list type="bullet">
        ''' <item>Epoch: J1991.25</item>
        ''' <item>Right ascension (RA): degrees</item>
        ''' <item>Declination (Dec): degrees</item>
        ''' <item>Proper motion in RA: milliarcseconds per year</item>
        ''' <item>Proper motion in Dec: milliarcseconds per year</item>
        ''' <item>Parallax: milliarcseconds</item>
        ''' <item>Radial velocity: kilometers per second (not in catalog)</item>
        ''' </list>
        ''' </para>
        ''' <para>
        ''' 2. Output (modified Hipparcos) epoch and units:
        ''' <list type="bullet">
        ''' <item>Epoch: J2000.0</item>
        ''' <item>Right ascension: hours</item>
        ''' <item>Declination: degrees</item>
        ''' <item>Proper motion in RA: milliarcseconds per year</item>
        ''' <item>Proper motion in Dec: milliarcseconds per year</item>
        ''' <item>Parallax: milliarcseconds</item>
        ''' <item>Radial velocity: kilometers per second</item>
        ''' </list>>
        ''' </para>
        ''' </remarks>
        Public Sub TransformHip(ByVal Hipparcos As CatEntry3,
                                        ByRef Hip2000 As CatEntry3) Implements INOVAS31.TransformHip
            If Is64Bit() Then
                TransformHip64(Hipparcos, Hip2000)
            Else
                TransformHip32(Hipparcos, Hip2000)
            End If
        End Sub

        ''' <summary>
        ''' Converts a vector in equatorial rectangular coordinates to equatorial spherical coordinates.
        ''' </summary>
        ''' <param name="Pos">Position vector, equatorial rectangular coordinates.</param>
        ''' <param name="Ra">Right ascension in hours.</param>
        ''' <param name="Dec">Declination in degrees.</param>
        ''' <returns>
        ''' <pre>
        ''' = 0 ... Everything OK.
        ''' = 1 ... All vector components are zero; 'Ra' and 'Dec' are indeterminate.
        ''' = 2 ... Both Pos[0] and Pos[1] are zero, but Pos[2] is nonzero; 'Ra' is indeterminate.
        ''' </pre></returns>
        ''' <remarks></remarks>
        Public Function Vector2RaDec(ByVal Pos() As Double,
                                             ByRef Ra As Double,
                                             ByRef Dec As Double) As Short Implements INOVAS31.Vector2RaDec
            If Is64Bit() Then
                Return Vector2RaDec64(ArrToPosVec(Pos), Ra, Dec)
            Else
                Return Vector2RaDec32(ArrToPosVec(Pos), Ra, Dec)
            End If
        End Function

        ''' <summary>
        ''' Compute the virtual place of a planet or other solar system body.
        ''' </summary>
        ''' <param name="JdTt">TT Julian date for virtual place.</param>
        ''' <param name="SsBody">structure containing the body designation for the solar system body(</param>
        ''' <param name="Accuracy">Code specifying the relative accuracy of the output position.</param>
        ''' <param name="Ra">Virtual right ascension in hours, referred to the GCRS.</param>
        ''' <param name="Dec">Virtual declination in degrees, referred to the GCRS.</param>
        ''' <param name="Dis">True distance from Earth to planet in AU.</param>
        ''' <returns>
        ''' <pre>
        ''' =  0 ... Everything OK.
        ''' =  1 ... Invalid value of 'Type' in structure 'SsBody'.
        ''' > 10 ... Error code from function 'Place'.
        ''' </pre></returns>
        ''' <remarks></remarks>
        Public Function VirtualPlanet(ByVal JdTt As Double,
                                              ByVal SsBody As Object3,
                                              ByVal Accuracy As Accuracy,
                                              ByRef Ra As Double,
                                              ByRef Dec As Double,
                                              ByRef Dis As Double) As Short Implements INOVAS31.VirtualPlanet
            If Is64Bit() Then
                Return VirtualPlanet64(JdTt, O3IFromObject3(SsBody), Accuracy, Ra, Dec, Dis)
            Else
                Return VirtualPlanet32(JdTt, O3IFromObject3(SsBody), Accuracy, Ra, Dec, Dis)
            End If
        End Function

        ''' <summary>
        ''' Computes the virtual place of a star at date 'JdTt', given its catalog mean place, proper motion, parallax, and radial velocity.
        ''' </summary>
        ''' <param name="JdTt">TT Julian date for virtual place.</param>
        ''' <param name="Star">catalog entry structure containing catalog data for the object in the ICRS</param>
        ''' <param name="Accuracy">Code specifying the relative accuracy of the output position.</param>
        ''' <param name="Ra">Virtual right ascension in hours, referred to the GCRS.</param>
        ''' <param name="Dec">Virtual declination in degrees, referred to the GCRS.</param>
        ''' <returns>
        ''' <pre>
        ''' =  0 ... Everything OK.
        ''' > 10 ... Error code from function 'MakeObject'.
        ''' > 20 ... Error code from function 'Place'
        ''' </pre></returns>
        ''' <remarks></remarks>
        Public Function VirtualStar(ByVal JdTt As Double,
                                            ByVal Star As CatEntry3,
                                            ByVal Accuracy As Accuracy,
                                            ByRef Ra As Double,
                                            ByRef Dec As Double) As Short Implements INOVAS31.VirtualStar
            If Is64Bit() Then
                Return VirtualStar64(JdTt, Star, Accuracy, Ra, Dec)
            Else
                Return VirtualStar32(JdTt, Star, Accuracy, Ra, Dec)
            End If
        End Function

        ''' <summary>
        ''' Corrects a vector in the ITRF (rotating Earth-fixed system) for polar motion, and also corrects 
        ''' the longitude origin (by a tiny amount) to the Terrestrial Intermediate Origin (TIO).
        ''' </summary>
        ''' <param name="Tjd">TT or UT1 Julian date.</param>
        '''       direction (short int)
        ''' <param name="Direction">Flag determining 'direction' of transformation;
        ''' direction  = 0 transformation applied, ITRS to terrestrial intermediate system
        ''' direction != 0 inverse transformation applied, terrestrial intermediate system to ITRS</param>
        ''' <param name="xp">Conventionally-defined X coordinate of Celestial Intermediate Pole with 
        ''' respect to ITRF pole, in arcseconds.</param>
        ''' <param name="yp">Conventionally-defined Y coordinate of Celestial Intermediate Pole with 
        ''' respect to ITRF pole, in arcseconds.</param>
        ''' <param name="Pos1">Position vector, geocentric equatorial rectangular coordinates, 
        ''' referred to ITRF axes.</param>
        ''' <param name="Pos2">Position vector, geocentric equatorial rectangular coordinates, 
        ''' referred to true equator and TIO.</param>
        ''' <remarks></remarks>
        Public Sub Wobble(ByVal Tjd As Double,
                                  ByVal Direction As TransformationDirection,
                                  ByVal xp As Double,
                                  ByVal yp As Double,
                                  ByVal Pos1() As Double,
                                  ByRef Pos2() As Double) Implements INOVAS31.Wobble
            Dim VPos2 As New PosVector

            If Is64Bit() Then
                Wobble64(Tjd, CShort(Direction), xp, yp, ArrToPosVec(Pos1), VPos2)
            Else
                Wobble32(Tjd, CShort(Direction), xp, yp, ArrToPosVec(Pos1), VPos2)
            End If

            PosVecToArr(VPos2, Pos2)
        End Sub
#End Region

#Region "Private Ephemeris And RACIOFile Routines"
        Private Function Ephem_Open(ByVal Ephem_Name As String,
                                    ByRef JD_Begin As Double,
                                    ByRef JD_End As Double,
                                    ByRef DENumber As Short) As Short

            Dim rc As Short
            If Is64Bit() Then
                rc = EphemOpen64(Ephem_Name, JD_Begin, JD_End, DENumber)
            Else
                rc = EphemOpen32(Ephem_Name, JD_Begin, JD_End, DENumber)
            End If
            Return rc
        End Function

        Private Function Ephem_Close() As Short
            If Is64Bit() Then
                Return EphemClose64()
            Else
                Return EphemClose32()
            End If
        End Function

        Private Sub SetRACIOFile(ByVal FName As String)
            If Is64Bit() Then
                SetRACIOFile64(FName)
            Else
                SetRACIOFile32(FName)
            End If
        End Sub
#End Region

#Region "DLL Entry Points for Ephemeris and RACIOFile (32bit)"
        <DllImport(NOVAS32DLL, EntryPoint:="set_racio_file")>
        Private Shared Sub SetRACIOFile32(<MarshalAs(UnmanagedType.LPStr)> ByVal FName As String)
        End Sub

        <DllImportAttribute(NOVAS32DLL, EntryPoint:="ephem_close")>
        Private Shared Function EphemClose32() As Short
        End Function

        <DllImportAttribute(NOVAS32DLL, EntryPoint:="ephem_open")>
        Private Shared Function EphemOpen32(<MarshalAs(UnmanagedType.LPStr)> ByVal Ephem_Name As String,
                                                                              ByRef JD_Begin As Double,
                                                                              ByRef JD_End As Double,
                                                                              ByRef DENumber As Short) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="planet_ephemeris")>
        Private Shared Function PlanetEphemeris32(ByRef Tjd As JDHighPrecision,
                                                  ByVal Target As Target,
                                                  ByVal Center As Target,
                                                  ByRef Position As PosVector,
                                                  ByRef Velocity As VelVector) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="readeph")>
        Private Shared Function ReadEph32(ByVal Mp As Integer,
                                          <MarshalAs(UnmanagedType.LPStr)> ByVal Name As String,
                                          ByVal Jd As Double,
                                          ByRef Err As Integer) As System.IntPtr
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="cleaneph")>
        Private Shared Sub CleanEph32()
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="solarsystem")>
        Private Shared Function SolarSystem32(ByVal tjd As Double,
                                              ByVal body As Short,
                                              ByVal origin As Short,
                                              ByRef pos As PosVector,
                                              ByRef vel As VelVector) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="state")>
        Private Shared Function State32(ByRef Jed As JDHighPrecision,
                                         ByVal Target As Target,
                                         ByRef TargetPos As PosVector,
                                         ByRef TargetVel As VelVector) As Short
        End Function
#End Region

#Region "DLL Entry Points NOVAS (32bit)"
        <DllImport(NOVAS32DLL, EntryPoint:="aberration")>
        Private Shared Sub Aberration32(ByRef Pos As PosVector,
                                        ByRef Vel As VelVector,
                                        ByVal LightTime As Double,
                                        ByRef Pos2 As PosVector)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="app_planet")>
        Private Shared Function AppPlanet32(ByVal JdTt As Double,
                                            ByRef SsBody As Object3Internal,
                                            ByVal Accuracy As Accuracy,
                                            ByRef Ra As Double,
                                            ByRef Dec As Double,
                                            ByRef Dis As Double) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="app_star")>
        Private Shared Function AppStar32(ByVal JdTt As Double,
                                             ByRef Star As CatEntry3,
                                             ByVal Accuracy As Accuracy,
                                             ByRef Ra As Double,
                                             ByRef Dec As Double) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="astro_planet")>
        Private Shared Function AstroPlanet32(ByVal JdTt As Double,
                                              ByRef SsBody As Object3Internal,
                                              ByVal Accuracy As Accuracy,
                                              ByRef Ra As Double,
                                              ByRef Dec As Double,
                                              ByRef Dis As Double) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="astro_star")>
        Private Shared Function AstroStar32(ByVal JdTt As Double,
                                            ByRef Star As CatEntry3,
                                            ByVal Accuracy As Accuracy,
                                            ByRef Ra As Double,
                                            ByRef Dec As Double) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="bary2obs")>
        Private Shared Sub Bary2Obs32(ByRef Pos As PosVector,
                                      ByRef PosObs As PosVector,
                                      ByRef Pos2 As PosVector,
                                      ByRef Lighttime As Double)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="cal_date")>
        Private Shared Sub CalDate32(ByVal Tjd As Double,
                                     ByRef Year As Short,
                                     ByRef Month As Short,
                                     ByRef Day As Short,
                                     ByRef Hour As Double)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="cel2ter")>
        Private Shared Function Cel2Ter32(ByVal JdHigh As Double,
                                          ByVal JdLow As Double,
                                          ByVal DeltaT As Double,
                                          ByVal Method As Method,
                                          ByVal Accuracy As Accuracy,
                                          ByVal OutputOption As OutputVectorOption,
                                          ByVal x As Double,
                                          ByVal y As Double,
                                          ByRef VecT As PosVector,
                                          ByRef VecC As PosVector) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="cel_pole")>
        Private Shared Function CelPole32(ByVal Tjd As Double,
                                          ByVal Type As PoleOffsetCorrection,
                                          ByVal Dpole1 As Double,
                                          ByVal Dpole2 As Double) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="cio_array")>
        Private Shared Function CioArray32(ByVal JdTdb As Double,
                                           ByVal NPts As Integer,
                                           ByRef Cio As RAOfCioArray) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="cio_basis")>
        Private Shared Function CioBasis32(ByVal JdTdbEquionx As Double,
                                           ByVal RaCioEquionx As Double,
                                           ByVal RefSys As ReferenceSystem,
                                           ByVal Accuracy As Accuracy,
                                           ByRef x As Double,
                                           ByRef y As Double,
                                           ByRef z As Double) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="cio_location")>
        Private Shared Function CioLocation32(ByVal JdTdb As Double,
                                              ByVal Accuracy As Accuracy,
                                              ByRef RaCio As Double,
                                              ByRef RefSys As ReferenceSystem) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="cio_ra")>
        Private Shared Function CioRa32(ByVal JdTt As Double,
                                        ByVal Accuracy As Accuracy,
                                        ByRef RaCio As Double) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="d_light")>
        Private Shared Function DLight32(ByRef Pos1 As PosVector,
                                         ByRef PosObs As PosVector) As Double
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="e_tilt")>
        Private Shared Sub ETilt32(ByVal JdTdb As Double,
                                   ByVal Accuracy As Accuracy,
                                   ByRef Mobl As Double,
                                   ByRef Tobl As Double,
                                   ByRef Ee As Double,
                                   ByRef Dpsi As Double,
                                   ByRef Deps As Double)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="ecl2equ_vec")>
        Private Shared Function Ecl2EquVec32(ByVal JdTt As Double,
                                             ByVal CoordSys As CoordSys,
                                             ByVal Accuracy As Accuracy,
                                             ByRef Pos1 As PosVector,
                                             ByRef Pos2 As PosVector) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="ee_ct")>
        Private Shared Function EeCt32(ByVal JdHigh As Double,
                                       ByVal JdLow As Double,
                                       ByVal Accuracy As Accuracy) As Double
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="ephemeris")>
        Private Shared Function Ephemeris32(ByRef Jd As JDHighPrecision,
                                            ByRef CelObj As Object3Internal,
                                            ByVal Origin As Origin,
                                            ByVal Accuracy As Accuracy,
                                            ByRef Pos As PosVector,
                                            ByRef Vel As VelVector) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="equ2ecl")>
        Private Shared Function Equ2Ecl32(ByVal JdTt As Double,
                                          ByVal CoordSys As CoordSys,
                                          ByVal Accuracy As Accuracy,
                                          ByVal Ra As Double,
                                          ByVal Dec As Double,
                                          ByRef ELon As Double,
                                          ByRef ELat As Double) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="equ2ecl_vec")>
        Private Shared Function Equ2EclVec32(ByVal JdTt As Double,
                                             ByVal CoordSys As CoordSys,
                                             ByVal Accuracy As Accuracy,
                                             ByRef Pos1 As PosVector,
                                             ByRef Pos2 As PosVector) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="equ2gal")>
        Private Shared Sub Equ2Gal32(ByVal RaI As Double,
                                     ByVal DecI As Double,
                                     ByRef GLon As Double,
                                     ByRef GLat As Double)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="equ2hor")>
        Private Shared Sub Equ2Hor32(ByVal Jd_Ut1 As Double,
                                     ByVal DeltT As Double,
                                     ByVal Accuracy As Accuracy,
                                     ByVal x As Double,
                                     ByVal y As Double,
                                     ByRef Location As OnSurface,
                                     ByVal Ra As Double,
                                     ByVal Dec As Double,
                                     ByVal RefOption As RefractionOption,
                                     ByRef Zd As Double,
                                     ByRef Az As Double,
                                     ByRef RaR As Double,
                                     ByRef DecR As Double)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="era")>
        Private Shared Function Era32(ByVal JdHigh As Double,
                                      ByVal JdLow As Double) As Double
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="frame_tie")>
        Private Shared Sub FrameTie32(ByRef Pos1 As PosVector,
                                      ByVal Direction As FrameConversionDirection,
                                      ByRef Pos2 As PosVector)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="fund_args")>
        Private Shared Sub FundArgs32(ByVal t As Double,
                                      ByRef a As FundamentalArgs)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="gcrs2equ")>
        Private Shared Function Gcrs2Equ32(ByVal JdTt As Double,
                                            ByVal CoordSys As CoordSys,
                                            ByVal Accuracy As Accuracy,
                                            ByVal RaG As Double,
                                            ByVal DecG As Double,
                                            ByRef Ra As Double,
                                            ByRef Dec As Double) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="geo_posvel")>
        Private Shared Function GeoPosVel32(ByVal JdTt As Double,
                                            ByVal DeltaT As Double,
                                            ByVal Accuracy As Accuracy,
                                            ByRef Obs As Observer,
                                            ByRef Pos As PosVector,
                                            ByRef Vel As VelVector) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="grav_def")>
        Private Shared Function GravDef32(ByVal JdTdb As Double,
                                          ByVal LocCode As EarthDeflection,
                                          ByVal Accuracy As Accuracy,
                                          ByRef Pos1 As PosVector,
                                          ByRef PosObs As PosVector,
                                          ByRef Pos2 As PosVector) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="grav_vec")>
        Private Shared Sub GravVec32(ByRef Pos1 As PosVector,
                                     ByRef PosObs As PosVector,
                                     ByRef PosBody As PosVector,
                                     ByVal RMass As Double,
                                     ByRef Pos2 As PosVector)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="ira_equinox")>
        Private Shared Function IraEquinox32(ByVal JdTdb As Double,
                                             ByVal Equinox As EquinoxType,
                                             ByVal Accuracy As Accuracy) As Double
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="julian_date")>
        Private Shared Function JulianDate32(ByVal Year As Short,
                                             ByVal Month As Short,
                                             ByVal Day As Short,
                                             ByVal Hour As Double) As Double
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="light_time")>
        Private Shared Function LightTime32(ByVal JdTdb As Double,
                                            ByRef SsObject As Object3Internal,
                                            ByRef PosObs As PosVector,
                                            ByVal TLight0 As Double,
                                            ByVal Accuracy As Accuracy,
                                            ByRef Pos As PosVector,
                                            ByRef TLight As Double) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="limb_angle")>
        Private Shared Sub LimbAngle32(ByRef PosObj As PosVector,
                                       ByRef PosObs As PosVector,
                                       ByRef LimbAng As Double,
                                       ByRef NadirAng As Double)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="local_planet")>
        Private Shared Function LocalPlanet32(ByVal JdTt As Double,
                                               ByRef SsBody As Object3Internal,
                                               ByVal DeltaT As Double,
                                               ByRef Position As OnSurface,
                                               ByVal Accuracy As Accuracy,
                                               ByRef Ra As Double,
                                               ByRef Dec As Double,
                                               ByRef Dis As Double) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="local_star")>
        Private Shared Function LocalStar32(ByVal JdTt As Double,
                                            ByVal DeltaT As Double,
                                            ByRef Star As CatEntry3,
                                            ByRef Position As OnSurface,
                                            ByVal Accuracy As Accuracy,
                                            ByRef Ra As Double,
                                            ByRef Dec As Double) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="make_cat_entry")>
        Private Shared Sub MakeCatEntry32(<MarshalAs(UnmanagedType.LPStr)> ByVal StarName As String,
                                          <MarshalAs(UnmanagedType.LPStr)> ByVal Catalog As String,
                                                                           ByVal StarNum As Integer,
                                                                           ByVal Ra As Double,
                                                                           ByVal Dec As Double,
                                                                           ByVal PmRa As Double,
                                                                           ByVal PmDec As Double,
                                                                           ByVal Parallax As Double,
                                                                           ByVal RadVel As Double,
                                                                           ByRef Star As CatEntry3)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="make_in_space")>
        Private Shared Sub MakeInSpace32(ByRef ScPos As PosVector,
                                         ByRef ScVel As VelVector,
                                         ByRef ObsSpace As InSpace)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="make_object")>
        Private Shared Function MakeObject32(ByVal Type As ObjectType,
                                             ByVal Number As Short,
                                             <MarshalAs(UnmanagedType.LPStr)> ByVal Name As String,
                                             ByRef StarData As CatEntry3,
                                             ByRef CelObj As Object3Internal) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="make_observer")>
        Private Shared Function MakeObserver32(ByVal Where As ObserverLocation,
                                               ByRef ObsSurface As OnSurface,
                                               ByRef ObsSpace As InSpace,
                                               ByRef Obs As Observer) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="make_observer_at_geocenter")>
        Private Shared Sub MakeObserverAtGeocenter32(ByRef ObsAtGeocenter As Observer)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="make_observer_in_space")>
        Private Shared Sub MakeObserverInSpace32(ByRef ScPos As PosVector,
                                                 ByRef ScVel As VelVector,
                                                 ByRef ObsInSpace As Observer)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="make_observer_on_surface")>
        Private Shared Sub MakeObserverOnSurface32(ByVal Latitude As Double,
                                                   ByVal Longitude As Double,
                                                   ByVal Height As Double,
                                                   ByVal Temperature As Double,
                                                   ByVal Pressure As Double,
                                                   ByRef ObsOnSurface As Observer)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="make_on_surface")>
        Private Shared Sub MakeOnSurface32(ByVal Latitude As Double,
                                           ByVal Longitude As Double,
                                           ByVal Height As Double,
                                           ByVal Temperature As Double,
                                           ByVal Pressure As Double,
                                           ByRef ObsSurface As OnSurface)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="mean_obliq")>
        Private Shared Function MeanObliq32(ByVal JdTdb As Double) As Double
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="mean_star")>
        Private Shared Function MeanStar32(ByVal JdTt As Double,
                                           ByVal Ra As Double,
                                           ByVal Dec As Double,
                                           ByVal Accuracy As Accuracy,
                                           ByRef IRa As Double,
                                           ByRef IDec As Double) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="norm_ang")>
        Private Shared Function NormAng32(ByVal Angle As Double) As Double
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="nutation")>
        Private Shared Sub Nutation32(ByVal JdTdb As Double,
                                      ByVal Direction As NutationDirection,
                                      ByVal Accuracy As Accuracy,
                                      ByRef Pos As PosVector,
                                      ByRef Pos2 As PosVector)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="nutation_angles")>
        Private Shared Sub NutationAngles32(ByVal t As Double,
                                            ByVal Accuracy As Accuracy,
                                            ByRef DPsi As Double,
                                            ByRef DEps As Double)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="place")>
        Private Shared Function Place32(ByVal JdTt As Double,
                                        ByRef CelObject As Object3Internal,
                                        ByRef Location As Observer,
                                        ByVal DeltaT As Double,
                                        ByVal CoordSys As CoordSys,
                                        ByVal Accuracy As Accuracy,
                                        ByRef Output As SkyPos) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="precession")>
        Private Shared Function Precession32(ByVal JdTdb1 As Double,
                                             ByRef Pos1 As PosVector,
                                             ByVal JdTdb2 As Double,
                                             ByRef Pos2 As PosVector) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="proper_motion")>
        Private Shared Sub ProperMotion32(ByVal JdTdb1 As Double,
                                          ByRef Pos As PosVector,
                                          ByRef Vel As VelVector,
                                          ByVal JdTdb2 As Double,
                                          ByRef Pos2 As PosVector)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="rad_vel")>
        Private Shared Sub RadVel32(ByRef CelObject As Object3Internal,
                                    ByRef Pos As PosVector,
                                    ByRef Vel As VelVector,
                                    ByRef VelObs As VelVector,
                                    ByVal DObsGeo As Double,
                                    ByVal DObsSun As Double,
                                    ByVal DObjSun As Double,
                                    ByRef Rv As Double)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="radec2vector")>
        Private Shared Sub RaDec2Vector32(ByVal Ra As Double,
                                          ByVal Dec As Double,
                                          ByVal Dist As Double,
                                          ByRef Vector As PosVector)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="refract")>
        Private Shared Function Refract32(ByRef Location As OnSurface,
                                          ByVal RefOption As RefractionOption,
                                          ByVal ZdObs As Double) As Double
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="sidereal_time")>
        Private Shared Function SiderealTime32(ByVal JdHigh As Double,
                                               ByVal JdLow As Double,
                                               ByVal DeltaT As Double,
                                               ByVal GstType As GstType,
                                               ByVal Method As Method,
                                               ByVal Accuracy As Accuracy,
                                               ByRef Gst As Double) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="spin")>
        Private Shared Sub Spin32(ByVal Angle As Double,
                                  ByRef Pos1 As PosVector,
                                  ByRef Pos2 As PosVector)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="starvectors")>
        Private Shared Sub StarVectors32(ByRef Star As CatEntry3,
                                         ByRef Pos As PosVector,
                                         ByRef Vel As VelVector)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="tdb2tt")>
        Private Shared Sub Tdb2Tt32(ByVal TdbJd As Double,
                                    ByRef TtJd As Double,
                                    ByRef SecDiff As Double)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="ter2cel")>
        Private Shared Function Ter2Cel32(ByVal JdHigh As Double,
                                          ByVal JdLow As Double,
                                          ByVal DeltaT As Double,
                                          ByVal Method As Method,
                                          ByVal Accuracy As Accuracy,
                                          ByVal OutputOption As OutputVectorOption,
                                          ByVal x As Double,
                                          ByVal y As Double,
                                          ByRef VecT As PosVector,
                                          ByRef VecC As PosVector) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="terra")>
        Private Shared Sub Terra32(ByRef Location As OnSurface,
                                   ByVal St As Double,
                                   ByRef Pos As PosVector,
                                   ByRef Vel As VelVector)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="topo_planet")>
        Private Shared Function TopoPlanet32(ByVal JdTt As Double,
                                             ByRef SsBody As Object3Internal,
                                             ByVal DeltaT As Double,
                                             ByRef Position As OnSurface,
                                             ByVal Accuracy As Accuracy,
                                             ByRef Ra As Double,
                                             ByRef Dec As Double,
                                             ByRef Dis As Double) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="topo_star")>
        Private Shared Function TopoStar32(ByVal JdTt As Double,
                                           ByVal DeltaT As Double,
                                           ByRef Star As CatEntry3,
                                           ByRef Position As OnSurface,
                                           ByVal Accuracy As Accuracy,
                                           ByRef Ra As Double,
                                           ByRef Dec As Double) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="transform_cat")>
        Private Shared Function TransformCat32(ByVal TransformOption As TransformationOption3,
                                               ByVal DateInCat As Double,
                                               ByRef InCat As CatEntry3,
                                               ByVal DateNewCat As Double,
                                               <MarshalAs(UnmanagedType.LPStr)> ByVal NewCatId As String,
                                               ByRef NewCat As CatEntry3) As Short

        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="transform_hip")>
        Private Shared Sub TransformHip32(ByRef Hipparcos As CatEntry3,
                                          ByRef Hip2000 As CatEntry3)
        End Sub

        <DllImport(NOVAS32DLL, EntryPoint:="vector2radec")>
        Private Shared Function Vector2RaDec32(ByRef Pos As PosVector,
                                               ByRef Ra As Double,
                                               ByRef Dec As Double) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="virtual_planet")>
        Private Shared Function VirtualPlanet32(ByVal JdTt As Double,
                                                ByRef SsBody As Object3Internal,
                                                ByVal Accuracy As Accuracy,
                                                ByRef Ra As Double,
                                                ByRef Dec As Double,
                                                ByRef Dis As Double) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="virtual_star")>
        Private Shared Function VirtualStar32(ByVal JdTt As Double,
                                              ByRef Star As CatEntry3,
                                              ByVal Accuracy As Accuracy,
                                              ByRef Ra As Double,
                                              ByRef Dec As Double) As Short
        End Function

        <DllImport(NOVAS32DLL, EntryPoint:="wobble")>
        Private Shared Sub Wobble32(ByVal Tjd As Double,
                                    ByVal Direction As Short,
                                    ByVal x As Double,
                                    ByVal y As Double,
                                    ByRef Pos1 As PosVector,
                                    ByRef Pos2 As PosVector)
        End Sub
#End Region

#Region "DLL Entry Points for Ephemeris and RACIOFile (64bit)"
        <DllImport(NOVAS64DLL, EntryPoint:="set_racio_file")>
        Private Shared Sub SetRACIOFile64(<MarshalAs(UnmanagedType.LPStr)> ByVal Name As String)
        End Sub

        <DllImportAttribute(NOVAS64DLL, EntryPoint:="ephem_close")>
        Private Shared Function EphemClose64() As Short
        End Function

        <DllImportAttribute(NOVAS64DLL, EntryPoint:="ephem_open")>
        Private Shared Function EphemOpen64(<MarshalAs(UnmanagedType.LPStr)> ByVal Ephem_Name As String,
                                                                              ByRef JD_Begin As Double,
                                                                              ByRef JD_End As Double,
                                                                              ByRef DENumber As Short) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="planet_ephemeris")>
        Private Shared Function PlanetEphemeris64(ByRef Tjd As JDHighPrecision,
                                                  ByVal Target As Target,
                                                  ByVal Center As Target,
                                                  ByRef Position As PosVector,
                                                  ByRef Velocity As VelVector) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="readeph")>
        Private Shared Function ReadEph64(ByVal Mp As Integer,
                                          <MarshalAs(UnmanagedType.LPStr)> ByVal Name As String,
                                          ByVal Jd As Double,
                                          ByRef Err As Integer) As System.IntPtr
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="cleaneph")>
        Private Shared Sub CleanEph64()
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="solarsystem")>
        Private Shared Function SolarSystem64(ByVal tjd As Double,
                                              ByVal body As Short,
                                              ByVal origin As Short,
                                              ByRef pos As PosVector,
                                              ByRef vel As VelVector) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="state")>
        Private Shared Function State64(ByRef Jed As JDHighPrecision,
                                         ByVal Target As Target,
                                         ByRef TargetPos As PosVector,
                                         ByRef TargetVel As VelVector) As Short
        End Function
#End Region

#Region "DLL Entry Points NOVAS (64bit)"
        <DllImport(NOVAS64DLL, EntryPoint:="aberration")>
        Private Shared Sub Aberration64(ByRef Pos As PosVector,
                                        ByRef Vel As VelVector,
                                        ByVal LightTime As Double,
                                        ByRef Pos2 As PosVector)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="app_planet")>
        Private Shared Function AppPlanet64(ByVal JdTt As Double,
                                            ByRef SsBody As Object3Internal,
                                            ByVal Accuracy As Accuracy,
                                            ByRef Ra As Double,
                                            ByRef Dec As Double,
                                            ByRef Dis As Double) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="app_star")>
        Private Shared Function AppStar64(ByVal JdTt As Double,
                                             ByRef Star As CatEntry3,
                                             ByVal Accuracy As Accuracy,
                                             ByRef Ra As Double,
                                             ByRef Dec As Double) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="astro_planet")>
        Private Shared Function AstroPlanet64(ByVal JdTt As Double,
                                              ByRef SsBody As Object3Internal,
                                              ByVal Accuracy As Accuracy,
                                              ByRef Ra As Double,
                                              ByRef Dec As Double,
                                              ByRef Dis As Double) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="astro_star")>
        Private Shared Function AstroStar64(ByVal JdTt As Double,
                                            ByRef Star As CatEntry3,
                                            ByVal Accuracy As Accuracy,
                                            ByRef Ra As Double,
                                            ByRef Dec As Double) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="bary2obs")>
        Private Shared Sub Bary2Obs64(ByRef Pos As PosVector,
                                      ByRef PosObs As PosVector,
                                      ByRef Pos2 As PosVector,
                                      ByRef Lighttime As Double)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="cal_date")>
        Private Shared Sub CalDate64(ByVal Tjd As Double,
                                     ByRef Year As Short,
                                     ByRef Month As Short,
                                     ByRef Day As Short,
                                     ByRef Hour As Double)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="cel2ter")>
        Private Shared Function Cel2Ter64(ByVal JdHigh As Double,
                                          ByVal JdLow As Double,
                                          ByVal DeltaT As Double,
                                          ByVal Method As Method,
                                          ByVal Accuracy As Accuracy,
                                          ByVal OutputOption As OutputVectorOption,
                                          ByVal x As Double,
                                          ByVal y As Double,
                                          ByRef VecT As PosVector,
                                          ByRef VecC As PosVector) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="cel_pole")>
        Private Shared Function CelPole64(ByVal Tjd As Double,
                                          ByVal Type As PoleOffsetCorrection,
                                          ByVal Dpole1 As Double,
                                          ByVal Dpole2 As Double) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="cio_array")>
        Private Shared Function CioArray64(ByVal JdTdb As Double,
                                           ByVal NPts As Integer,
                                           ByRef Cio As RAOfCioArray) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="cio_basis")>
        Private Shared Function CioBasis64(ByVal JdTdbEquionx As Double,
                                           ByVal RaCioEquionx As Double,
                                           ByVal RefSys As ReferenceSystem,
                                           ByVal Accuracy As Accuracy,
                                           ByRef x As Double,
                                           ByRef y As Double,
                                           ByRef z As Double) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="cio_location")>
        Private Shared Function CioLocation64(ByVal JdTdb As Double,
                                              ByVal Accuracy As Accuracy,
                                              ByRef RaCio As Double,
                                              ByRef RefSys As ReferenceSystem) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="cio_ra")>
        Private Shared Function CioRa64(ByVal JdTt As Double,
                                        ByVal Accuracy As Accuracy,
                                        ByRef RaCio As Double) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="d_light")>
        Private Shared Function DLight64(ByRef Pos1 As PosVector,
                                         ByRef PosObs As PosVector) As Double
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="e_tilt")>
        Private Shared Sub ETilt64(ByVal JdTdb As Double,
                                   ByVal Accuracy As Accuracy,
                                   ByRef Mobl As Double,
                                   ByRef Tobl As Double,
                                   ByRef Ee As Double,
                                   ByRef Dpsi As Double,
                                   ByRef Deps As Double)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="ecl2equ_vec")>
        Private Shared Function Ecl2EquVec64(ByVal JdTt As Double,
                                             ByVal CoordSys As CoordSys,
                                             ByVal Accuracy As Accuracy,
                                             ByRef Pos1 As PosVector,
                                             ByRef Pos2 As PosVector) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="ee_ct")>
        Private Shared Function EeCt64(ByVal JdHigh As Double,
                                       ByVal JdLow As Double,
                                       ByVal Accuracy As Accuracy) As Double
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="ephemeris")>
        Private Shared Function Ephemeris64(ByRef Jd As JDHighPrecision,
                                            ByRef CelObj As Object3Internal,
                                            ByVal Origin As Origin,
                                            ByVal Accuracy As Accuracy,
                                            ByRef Pos As PosVector,
                                            ByRef Vel As VelVector) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="equ2ecl")>
        Private Shared Function Equ2Ecl64(ByVal JdTt As Double,
                                          ByVal CoordSys As CoordSys,
                                          ByVal Accuracy As Accuracy,
                                          ByVal Ra As Double,
                                          ByVal Dec As Double,
                                          ByRef ELon As Double,
                                          ByRef ELat As Double) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="equ2ecl_vec")>
        Private Shared Function Equ2EclVec64(ByVal JdTt As Double,
                                             ByVal CoordSys As CoordSys,
                                             ByVal Accuracy As Accuracy,
                                             ByRef Pos1 As PosVector,
                                             ByRef Pos2 As PosVector) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="equ2gal")>
        Private Shared Sub Equ2Gal64(ByVal RaI As Double,
                                     ByVal DecI As Double,
                                     ByRef GLon As Double,
                                     ByRef GLat As Double)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="equ2hor")>
        Private Shared Sub Equ2Hor64(ByVal Jd_Ut1 As Double,
                                     ByVal DeltT As Double,
                                     ByVal Accuracy As Accuracy,
                                     ByVal x As Double,
                                     ByVal y As Double,
                                     ByRef Location As OnSurface,
                                     ByVal Ra As Double,
                                     ByVal Dec As Double,
                                     ByVal RefOption As RefractionOption,
                                     ByRef Zd As Double,
                                     ByRef Az As Double,
                                     ByRef RaR As Double,
                                     ByRef DecR As Double)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="era")>
        Private Shared Function Era64(ByVal JdHigh As Double,
                                      ByVal JdLow As Double) As Double
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="frame_tie")>
        Private Shared Sub FrameTie64(ByRef Pos1 As PosVector,
                                      ByVal Direction As FrameConversionDirection,
                                      ByRef Pos2 As PosVector)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="fund_args")>
        Private Shared Sub FundArgs64(ByVal t As Double,
                                      ByRef a As FundamentalArgs)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="gcrs2equ")>
        Private Shared Function Gcrs2Equ64(ByVal JdTt As Double,
                                            ByVal CoordSys As CoordSys,
                                            ByVal Accuracy As Accuracy,
                                            ByVal RaG As Double,
                                            ByVal DecG As Double,
                                            ByRef Ra As Double,
                                            ByRef Dec As Double) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="geo_posvel")>
        Private Shared Function GeoPosVel64(ByVal JdTt As Double,
                                            ByVal DeltaT As Double,
                                            ByVal Accuracy As Accuracy,
                                            ByRef Obs As Observer,
                                            ByRef Pos As PosVector,
                                            ByRef Vel As VelVector) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="grav_def")>
        Private Shared Function GravDef64(ByVal JdTdb As Double,
                                          ByVal LocCode As EarthDeflection,
                                          ByVal Accuracy As Accuracy,
                                          ByRef Pos1 As PosVector,
                                          ByRef PosObs As PosVector,
                                          ByRef Pos2 As PosVector) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="grav_vec")>
        Private Shared Sub GravVec64(ByRef Pos1 As PosVector,
                                     ByRef PosObs As PosVector,
                                     ByRef PosBody As PosVector,
                                     ByVal RMass As Double,
                                     ByRef Pos2 As PosVector)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="ira_equinox")>
        Private Shared Function IraEquinox64(ByVal JdTdb As Double,
                                             ByVal Equinox As EquinoxType,
                                             ByVal Accuracy As Accuracy) As Double
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="julian_date")>
        Private Shared Function JulianDate64(ByVal Year As Short,
                                             ByVal Month As Short,
                                             ByVal Day As Short,
                                             ByVal Hour As Double) As Double
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="light_time")>
        Private Shared Function LightTime64(ByVal JdTdb As Double,
                                            ByRef SsObject As Object3Internal,
                                            ByRef PosObs As PosVector,
                                            ByVal TLight0 As Double,
                                            ByVal Accuracy As Accuracy,
                                            ByRef Pos As PosVector,
                                            ByRef TLight As Double) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="limb_angle")>
        Private Shared Sub LimbAngle64(ByRef PosObj As PosVector,
                                       ByRef PosObs As PosVector,
                                       ByRef LimbAng As Double,
                                       ByRef NadirAng As Double)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="local_planet")>
        Private Shared Function LocalPlanet64(ByVal JdTt As Double,
                                               ByRef SsBody As Object3Internal,
                                               ByVal DeltaT As Double,
                                               ByRef Position As OnSurface,
                                               ByVal Accuracy As Accuracy,
                                               ByRef Ra As Double,
                                               ByRef Dec As Double,
                                               ByRef Dis As Double) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="local_star")>
        Private Shared Function LocalStar64(ByVal JdTt As Double,
                                            ByVal DeltaT As Double,
                                            ByRef Star As CatEntry3,
                                            ByRef Position As OnSurface,
                                            ByVal Accuracy As Accuracy,
                                            ByRef Ra As Double,
                                            ByRef Dec As Double) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="make_cat_entry")>
        Private Shared Sub MakeCatEntry64(<MarshalAs(UnmanagedType.LPStr)> ByVal StarName As String,
                                          <MarshalAs(UnmanagedType.LPStr)> ByVal Catalog As String,
                                                                           ByVal StarNum As Integer,
                                                                           ByVal Ra As Double,
                                                                           ByVal Dec As Double,
                                                                           ByVal PmRa As Double,
                                                                           ByVal PmDec As Double,
                                                                           ByVal Parallax As Double,
                                                                           ByVal RadVel As Double,
                                                                           ByRef Star As CatEntry3)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="make_in_space")>
        Private Shared Sub MakeInSpace64(ByRef ScPos As PosVector,
                                         ByRef ScVel As VelVector,
                                         ByRef ObsSpace As InSpace)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="make_object")>
        Private Shared Function MakeObject64(ByVal Type As ObjectType,
                                             ByVal Number As Short,
                                             <MarshalAs(UnmanagedType.LPStr)> ByVal Name As String,
                                             ByRef StarData As CatEntry3,
                                             ByRef CelObj As Object3Internal) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="make_observer")>
        Private Shared Function MakeObserver64(ByVal Where As ObserverLocation,
                                               ByRef ObsSurface As OnSurface,
                                               ByRef ObsSpace As InSpace,
                                               ByRef Obs As Observer) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="make_observer_at_geocenter")>
        Private Shared Sub MakeObserverAtGeocenter64(ByRef ObsAtGeocenter As Observer)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="make_observer_in_space")>
        Private Shared Sub MakeObserverInSpace64(ByRef ScPos As PosVector,
                                                 ByRef ScVel As VelVector,
                                                 ByRef ObsInSpace As Observer)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="make_observer_on_surface")>
        Private Shared Sub MakeObserverOnSurface64(ByVal Latitude As Double,
                                                   ByVal Longitude As Double,
                                                   ByVal Height As Double,
                                                   ByVal Temperature As Double,
                                                   ByVal Pressure As Double,
                                                   ByRef ObsOnSurface As Observer)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="make_on_surface")>
        Private Shared Sub MakeOnSurface64(ByVal Latitude As Double,
                                           ByVal Longitude As Double,
                                           ByVal Height As Double,
                                           ByVal Temperature As Double,
                                           ByVal Pressure As Double,
                                           ByRef ObsSurface As OnSurface)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="mean_obliq")>
        Private Shared Function MeanObliq64(ByVal JdTdb As Double) As Double
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="mean_star")>
        Private Shared Function MeanStar64(ByVal JdTt As Double,
                                           ByVal Ra As Double,
                                           ByVal Dec As Double,
                                           ByVal Accuracy As Accuracy,
                                           ByRef IRa As Double,
                                           ByRef IDec As Double) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="norm_ang")>
        Private Shared Function NormAng64(ByVal Angle As Double) As Double
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="nutation")>
        Private Shared Sub Nutation64(ByVal JdTdb As Double,
                                      ByVal Direction As NutationDirection,
                                      ByVal Accuracy As Accuracy,
                                      ByRef Pos As PosVector,
                                      ByRef Pos2 As PosVector)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="nutation_angles")>
        Private Shared Sub NutationAngles64(ByVal t As Double,
                                            ByVal Accuracy As Accuracy,
                                            ByRef DPsi As Double,
                                            ByRef DEps As Double)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="place")>
        Private Shared Function Place64(ByVal JdTt As Double,
                                        ByRef CelObject As Object3Internal,
                                        ByRef Location As Observer,
                                        ByVal DeltaT As Double,
                                        ByVal CoordSys As CoordSys,
                                        ByVal Accuracy As Accuracy,
                                        ByRef Output As SkyPos) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="precession")>
        Private Shared Function Precession64(ByVal JdTdb1 As Double,
                                             ByRef Pos1 As PosVector,
                                             ByVal JdTdb2 As Double,
                                             ByRef Pos2 As PosVector) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="proper_motion")>
        Private Shared Sub ProperMotion64(ByVal JdTdb1 As Double,
                                          ByRef Pos As PosVector,
                                          ByRef Vel As VelVector,
                                          ByVal JdTdb2 As Double,
                                          ByRef Pos2 As PosVector)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="rad_vel")>
        Private Shared Sub RadVel64(ByRef CelObject As Object3Internal,
                                    ByRef Pos As PosVector,
                                    ByRef Vel As VelVector,
                                    ByRef VelObs As VelVector,
                                    ByVal DObsGeo As Double,
                                    ByVal DObsSun As Double,
                                    ByVal DObjSun As Double,
                                    ByRef Rv As Double)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="radec2vector")>
        Private Shared Sub RaDec2Vector64(ByVal Ra As Double,
                                          ByVal Dec As Double,
                                          ByVal Dist As Double,
                                          ByRef Vector As PosVector)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="refract")>
        Private Shared Function Refract64(ByRef Location As OnSurface,
                                          ByVal RefOption As RefractionOption,
                                          ByVal ZdObs As Double) As Double
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="sidereal_time")>
        Private Shared Function SiderealTime64(ByVal JdHigh As Double,
                                               ByVal JdLow As Double,
                                               ByVal DeltaT As Double,
                                               ByVal GstType As GstType,
                                               ByVal Method As Method,
                                               ByVal Accuracy As Accuracy,
                                               ByRef Gst As Double) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="spin")>
        Private Shared Sub Spin64(ByVal Angle As Double,
                                  ByRef Pos1 As PosVector,
                                  ByRef Pos2 As PosVector)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="starvectors")>
        Private Shared Sub StarVectors64(ByRef Star As CatEntry3,
                                         ByRef Pos As PosVector,
                                         ByRef Vel As VelVector)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="tdb2tt")>
        Private Shared Sub Tdb2Tt64(ByVal TdbJd As Double,
                                    ByRef TtJd As Double,
                                    ByRef SecDiff As Double)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="ter2cel")>
        Private Shared Function Ter2Cel64(ByVal JdHigh As Double,
                                          ByVal JdLow As Double,
                                          ByVal DeltaT As Double,
                                          ByVal Method As Method,
                                          ByVal Accuracy As Accuracy,
                                          ByVal OutputOption As OutputVectorOption,
                                          ByVal x As Double,
                                          ByVal y As Double,
                                          ByRef VecT As PosVector,
                                          ByRef VecC As PosVector) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="terra")>
        Private Shared Sub Terra64(ByRef Location As OnSurface,
                                   ByVal St As Double,
                                   ByRef Pos As PosVector,
                                   ByRef Vel As VelVector)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="topo_planet")>
        Private Shared Function TopoPlanet64(ByVal JdTt As Double,
                                             ByRef SsBody As Object3Internal,
                                             ByVal DeltaT As Double,
                                             ByRef Position As OnSurface,
                                             ByVal Accuracy As Accuracy,
                                             ByRef Ra As Double,
                                             ByRef Dec As Double,
                                             ByRef Dis As Double) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="topo_star")>
        Private Shared Function TopoStar64(ByVal JdTt As Double,
                                           ByVal DeltaT As Double,
                                           ByRef Star As CatEntry3,
                                           ByRef Position As OnSurface,
                                           ByVal Accuracy As Accuracy,
                                           ByRef Ra As Double,
                                           ByRef Dec As Double) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="transform_cat")>
        Private Shared Function TransformCat64(ByVal TransformOption As TransformationOption3,
                                               ByVal DateInCat As Double,
                                               ByRef InCat As CatEntry3,
                                               ByVal DateNewCat As Double,
                                               <MarshalAs(UnmanagedType.LPStr)> ByVal NewCatId As String,
                                               ByRef NewCat As CatEntry3) As Short

        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="transform_hip")>
        Private Shared Sub TransformHip64(ByRef Hipparcos As CatEntry3,
                                          ByRef Hip2000 As CatEntry3)
        End Sub

        <DllImport(NOVAS64DLL, EntryPoint:="vector2radec")>
        Private Shared Function Vector2RaDec64(ByRef Pos As PosVector,
                                               ByRef Ra As Double,
                                               ByRef Dec As Double) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="virtual_planet")>
        Private Shared Function VirtualPlanet64(ByVal JdTt As Double,
                                                ByRef SsBody As Object3Internal,
                                                ByVal Accuracy As Accuracy,
                                                ByRef Ra As Double,
                                                ByRef Dec As Double,
                                                ByRef Dis As Double) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="virtual_star")>
        Private Shared Function VirtualStar64(ByVal JdTt As Double,
                                              ByRef Star As CatEntry3,
                                              ByVal Accuracy As Accuracy,
                                              ByRef Ra As Double,
                                              ByRef Dec As Double) As Short
        End Function

        <DllImport(NOVAS64DLL, EntryPoint:="wobble")>
        Private Shared Sub Wobble64(ByVal Tjd As Double,
                                    ByVal Direction As Short,
                                    ByVal x As Double,
                                    ByVal y As Double,
                                    ByRef Pos1 As PosVector,
                                    ByRef Pos2 As PosVector)
        End Sub
#End Region

#Region "Private Support Code"
        'Constants for SHGetSpecialFolderPath shell call
        Private Const CSIDL_PROGRAM_FILES As Integer = 38 '0x0026
        Private Const CSIDL_PROGRAM_FILESX86 As Integer = 42 '0x002a,
        Private Const CSIDL_WINDOWS As Integer = 36 ' 0x0024,
        Private Const CSIDL_PROGRAM_FILES_COMMONX86 As Integer = 44 ' 0x002c,

        'DLL to provide the path to Program Files(x86)\Common Files folder location that is not avialable through the .NET framework
        ''' <summary>
        ''' Get path to a system folder
        ''' </summary>
        ''' <param name="hwndOwner">SUpply null / nothing to use "current user"</param>
        ''' <param name="lpszPath">returned string folder path</param>
        ''' <param name="nFolder">Folder Number from CSIDL enumeration e.g. CSIDL_PROGRAM_FILES_COMMONX86 = 44 = 0x2c</param>
        ''' <param name="fCreate">Indicates whether the folder should be created if it does not already exist. If this value is nonzero, 
        ''' the folder is created. If this value is zero, the folder is not created</param>
        ''' <returns>TRUE if successful; otherwise, FALSE.</returns>
        ''' <remarks></remarks>
        <DllImport("shell32.dll")>
        Private Shared Function SHGetSpecialFolderPath(ByVal hwndOwner As IntPtr,
                                               <Out()> ByVal lpszPath As System.Text.StringBuilder,
                                               ByVal nFolder As Integer,
                                               ByVal fCreate As Boolean) As Boolean
        End Function

        ''' <summary>
        ''' Loads a library DLL
        ''' </summary>
        ''' <param name="lpFileName">Full path to the file to load</param>
        ''' <returns>A pointer to the loaded DLL image</returns>
        ''' <remarks>This is a wrapper for the Windows kernel32 function LoadLibraryA</remarks>
        <DllImport("kernel32.dll", SetLastError:=True, EntryPoint:="LoadLibraryA")>
        Private Shared Function LoadLibrary(ByVal lpFileName As String) As IntPtr
        End Function

        ''' <summary>
        ''' Unloads a library DLL
        ''' </summary>
        ''' <param name="hModule">Pointer to the loaded library returned by the LoadLibrary function.</param>
        ''' <returns>True or false depending on whether the library was released.</returns>
        ''' <remarks></remarks>
        <DllImport("kernel32.dll", SetLastError:=True, EntryPoint:="FreeLibrary")>
        Private Shared Function FreeLibrary(ByVal hModule As IntPtr) As Boolean
        End Function

        Private Shared Function Is64Bit() As Boolean
            If IntPtr.Size = 8 Then 'Check whether we are running on a 32 or 64bit system.
                Return True
            Else
                Return False
            End If
        End Function

        Private Shared Function ArrToPosVec(ByVal Arr As Double()) As PosVector
            'Create a new vector having the values in the supplied double array
            Dim V As New PosVector
            V.x = Arr(0)
            V.y = Arr(1)
            V.z = Arr(2)
            Return V
        End Function

        Private Shared Sub PosVecToArr(ByVal V As PosVector, ByRef Ar As Double())
            'Copy a vector structure to a returned double array
            Ar(0) = V.x
            Ar(1) = V.y
            Ar(2) = V.z
        End Sub

        Private Shared Function ArrToVelVec(ByVal Arr As Double()) As VelVector
            'Create a new vector having the values in the supplied double array
            Dim V As New VelVector
            V.x = Arr(0)
            V.y = Arr(1)
            V.z = Arr(2)
            Return V
        End Function

        Private Shared Sub VelVecToArr(ByVal V As VelVector, ByRef Ar As Double())
            'Copy a vector structure to a returned double array
            Ar(0) = V.x
            Ar(1) = V.y
            Ar(2) = V.z
        End Sub

        Private Shared Sub RACioArrayStructureToArr(ByVal C As RAOfCioArray, ByRef Ar As ArrayList)
            'Transfer all RACio values that have actually been set by the NOVAS DLL to the arraylist for return to the client
            If C.Value1.RACio <> RACIO_DEFAULT_VALUE Then Ar.Add(C.Value1)
            If C.Value2.RACio <> RACIO_DEFAULT_VALUE Then Ar.Add(C.Value2)
            If C.Value3.RACio <> RACIO_DEFAULT_VALUE Then Ar.Add(C.Value3)
            If C.Value4.RACio <> RACIO_DEFAULT_VALUE Then Ar.Add(C.Value4)
            If C.Value5.RACio <> RACIO_DEFAULT_VALUE Then Ar.Add(C.Value5)
            If C.Value6.RACio <> RACIO_DEFAULT_VALUE Then Ar.Add(C.Value6)
            If C.Value7.RACio <> RACIO_DEFAULT_VALUE Then Ar.Add(C.Value7)
            If C.Value8.RACio <> RACIO_DEFAULT_VALUE Then Ar.Add(C.Value8)
            If C.Value9.RACio <> RACIO_DEFAULT_VALUE Then Ar.Add(C.Value9)
            If C.Value10.RACio <> RACIO_DEFAULT_VALUE Then Ar.Add(C.Value10)
            If C.Value11.RACio <> RACIO_DEFAULT_VALUE Then Ar.Add(C.Value11)
            If C.Value12.RACio <> RACIO_DEFAULT_VALUE Then Ar.Add(C.Value12)
            If C.Value13.RACio <> RACIO_DEFAULT_VALUE Then Ar.Add(C.Value13)
            If C.Value14.RACio <> RACIO_DEFAULT_VALUE Then Ar.Add(C.Value14)
            If C.Value15.RACio <> RACIO_DEFAULT_VALUE Then Ar.Add(C.Value15)
            If C.Value16.RACio <> RACIO_DEFAULT_VALUE Then Ar.Add(C.Value16)
            If C.Value17.RACio <> RACIO_DEFAULT_VALUE Then Ar.Add(C.Value17)
            If C.Value18.RACio <> RACIO_DEFAULT_VALUE Then Ar.Add(C.Value18)
            If C.Value19.RACio <> RACIO_DEFAULT_VALUE Then Ar.Add(C.Value19)
            If C.Value20.RACio <> RACIO_DEFAULT_VALUE Then Ar.Add(C.Value20)
        End Sub

        Private Sub O3FromO3Internal(ByVal O3I As Object3Internal, ByRef O3 As Object3)
            O3.Name = O3I.Name
            O3.Number = CType(O3I.Number, Body)
            O3.Star = O3I.Star
            O3.Type = O3I.Type
        End Sub

        Private Function O3IFromObject3(ByVal O3 As Object3) As Object3Internal
            Dim O3I As New Object3Internal
            O3I.Name = O3.Name
            O3I.Number = CShort(O3.Number)
            O3I.Star = O3.Star
            O3I.Type = O3.Type
            Return O3I
        End Function
#End Region

#Region "DeltaT Member"
        ''' <summary>
        ''' Return the value of DeltaT for the given Julian date
        ''' </summary>
        ''' <param name="Tjd">Julian date for which the delta T value is required</param>
        ''' <returns>Double value of DeltaT (seconds)</returns>
        ''' <remarks>Valid between the years 1650 and 2050</remarks>
        Public Function DeltaT(ByVal Tjd As Double) As Double Implements INOVAS31.DeltaT
            Return Parameters.DeltaT(Tjd)
        End Function
#End Region

    End Class

End Namespace
