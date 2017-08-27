namespace MgmtApi
{
    /// <summary>
    ///  This class provides arguments for the ApiClient configuration.
    ///  All the arguments are configured with their default values.
    ///  As default, the debug file won't be created, the checkFingerprint is set to True, and the port is set to 443.
    /// </summary>
    public class ApiClientArgs
    {
        #region Constants

        private const int DefaultPort = 443;
        private const string DefaultFingerprintFile = "./fingerprints.txt";

        #endregion

        #region Private Members

        /// <summary>
        /// The debug file holds the data of all the communication
        /// between this client and Check Point's Management Server.
        /// </summary>
        private string _debugFile;

        /// <summary>
        /// The fingerprint file holds all of the data regarding the Check Point's Management Server fingerprint.
        /// </summary>
        private string _fingerprintFile  = DefaultFingerprintFile;

        /// <summary>
        ///Port on management server 
        /// </summary>
        private int _port = DefaultPort;

        #endregion

        #region Properties

        /// <summary>
        /// Gets or sets a value which represent the proxy setting. If set to null, the connection won't use proxy tunneling
        /// </summary>
        public string ProxySetting { get; set; }

        /// <summary>
        /// Returns true if the user asked for a specific port number, otherwise false
        /// </summary>
        public bool IsUserEnteredPort { get; private set; }

        /// <summary>
        ///Gets or sets a value. The field indicates whether to validate the SSL fingerprint when connecting to the server or not.
        /// </summary>
        public bool CheckFingerprint { get; set; }

        /// <summary>
        /// Gets or sets a value which represents the debug file name.
        /// </summary>
        public string DebugFile
        {
            get
            {
                return _debugFile;
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _debugFile = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets a value which represents the fingerprint file name.
        /// </summary>
        public string FingerprintFile
        {
            get
            {
                return _fingerprintFile;
            }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    _fingerprintFile = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets a value which represent the port number. If set to null the default port will be chosen.
        /// </summary>
        public int? Port
        {
            get
            {
                return _port;
            }
            set
            {
                if ( value == null )
                {
                    _port = DefaultPort;
                    IsUserEnteredPort = false;
                }
                else
                {
                    _port = value.Value;
                    IsUserEnteredPort = true;
                }
            }
        }

        #endregion
    }
}
