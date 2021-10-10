using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;



namespace ASCOM.DynamicRemoteClients
{

    class Program
    {
        static void Main(string[] args)
        {
        }
    }

    [ProgId(DRIVER_PROGID)]
    [ClassInterface(ClassInterfaceType.None)]
    public class Camera : CameraBaseClass
    {
        private const string DRIVER_NUMBER = "1";
        private const string DEVICE_TYPE = "Camera";
        private const string DRIVER_DISPLAY_NAME = "Camera V2 simulator";
        private const string DRIVER_PROGID = "ASCOM.AlpacaDynamic1.Camera";
        public Camera() : base(DRIVER_NUMBER, DRIVER_DISPLAY_NAME, DRIVER_PROGID)
        {
        }

    }
}

