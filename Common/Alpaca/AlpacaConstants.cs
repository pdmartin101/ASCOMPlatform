
using ASCOM.Common.Alpaca;

namespace ASCOM.Common.Alpaca2
{
    public static class AlpacaConstants2
    {
        // Regular expressions to validate IP addresses and host names
        public const string ValidIpAddressRegex = @"^(([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])\.){3}([0-9]|[1-9][0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])$";
        public const string ValidHostnameRegex = @"^(([a-zA-Z0-9]|[a-zA-Z0-9][a-zA-Z0-9\-]*[a-zA-Z0-9])\.)*([A-Za-z0-9]|[A-Za-z0-9][A-Za-z0-9\-]*[A-Za-z0-9])$";


        public const string LOCALHOST_NAME_IPV4 = "localhost";
        public const string LOCALHOST_ADDRESS_IPV4 = "127.0.0.1"; // Get the localhost loop back address

        // Dynamic client configuration constants
        public const int SOCKET_ERROR_MAXIMUM_RETRIES = 2; // The number of retries that the client will make when it receives a socket actively refused error from the remote device
        public const int SOCKET_ERROR_RETRY_DELAY_TIME = 1000; // The delay time (milliseconds) between socket actively refused retries

        public const string STRONG_WILDCARD_NAME = "+"; // Get the localhost loop back address

        public const string ACCEPT_HEADER_NAME = "Accept";
        public const string IMAGE_BYTES_ACCEPT_HEADER = "application/imagebytes, application/json, text/json";
        public const string CONTENT_TYPE_HEADER_NAME = "Content-Type";
//        public const string IMAGE_ARRAY_METHOD_NAME = "PdmPdm";

        
        public const ImageArrayTransferType IMAGE_ARRAY_TRANSFER_TYPE_DEFAULT = DEFAULT_IMAGE_ARRAY_TRANSFER_TYPE;
        public const ImageArrayCompression IMAGE_ARRAY_COMPRESSION_DEFAULT = DEFAULT_IMAGE_ARRAY_COMPRESSION;
        // Enum used by the dynamic client to indicate what type of image array transfer should be used
        //public enum ImageArrayTransferType
        //{
        //    JSON = 0,
        //    Base64HandOff = 1,
        //}

        // Enum used by the dynamic client to indicate what type of compression should be used in responses
        //public enum ImageArrayCompression
        //{
        //    None = 0,
        //    Deflate = 1,
        //    GZip = 2,
        //    GZipOrDeflate = 3
        //}

        // Default image array transfer constants
        public const ImageArrayCompression DEFAULT_IMAGE_ARRAY_COMPRESSION = ImageArrayCompression.None;
        public const ImageArrayTransferType DEFAULT_IMAGE_ARRAY_TRANSFER_TYPE = ImageArrayTransferType.Base64HandOff;
    }
}
