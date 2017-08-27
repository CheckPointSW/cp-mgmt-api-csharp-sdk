using System;

namespace MgmtApi
{
    /// <summary>
    /// This Class is responsible for parsing the proxy settings string which was entered by a user.
    /// </summary>
    class ApiProxySettingsProcessor
    {
        #region Properties

        /// <summary>
        /// Gets the user name in proxy server, optional 
        /// </summary>
        internal string UserName { get; private set; }

        /// <summary>
        ///Gets the password of proxy server, optional
        /// </summary>
        internal string Password { get; private set; }

        /// <summary>
        /// Gets the host name/number of the proxy server, mandatory
        /// </summary>
        internal string Host { get; private set; }

        /// <summary>
        /// Gets the port name/number of the proxy server, optional
        /// </summary>
        internal int Port { get; private set; }

        /// <summary>
        /// returns True if the proxy settings parse succeed, otherwise false.
        /// </summary>
        internal bool ProxySettingExist { get; private set; }

        #endregion

        #region Constructors

        /// <summary>
        ///Constructor
        /// </summary>
        /// <param name="proxySetting"></param>
        internal ApiProxySettingsProcessor(string proxySetting)
        {
            ParseProxySetting(proxySetting);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// This function parses the string of the proxy setting and defines the variables accordingly
        /// </summary>
        /// <param name="proxySetting">in format 'user:password@host:port'</param>
        private void ParseProxySetting(string proxySetting)
        {
            //The proxy setting string is empty, there is noting to do
            if (string.IsNullOrEmpty(proxySetting))
            {
                return;
            }

            string[] parse = proxySetting.Split('@');
            int parseLength = parse.Length;
            if (parseLength > 2 || parseLength < 1)
            {
                throw new ApiClientException("Error : proxy setting format is invalid, the format should be as following user:password@host:port (only the 'host' is mandatory)");
            }

            string[] hostAndPort;
            //user/password exist
            if (parse.Length == 2)
            {
                if (!string.IsNullOrEmpty(parse[0]))
                {
                    string[] userAndPassword = parse[0].Split(':');
                    int userAndPasswordLength = userAndPassword.Length;
                    if (userAndPasswordLength > 2)
                    {
                        //To many parameters
                        throw new ApiClientException("Error : proxy setting format is invalid, the format should be as following : user:password@host:port (only the 'host' is mandatory)");
                    }
                    if (userAndPasswordLength == 2)
                    {
                        //user and password exist
                        Password = userAndPassword[1];
                    }
                    UserName = userAndPassword[0];
                }
                hostAndPort = parse[1].Split(':');
            }
            //Only host(maybe port)
            else
            {
                hostAndPort = parse[0].Split(':');
            }

            int hostAndPortLength = hostAndPort.Length;
            if (hostAndPortLength > 2 || hostAndPortLength < 1)
            {
                throw new ApiClientException("Error : proxy setting format is invalid, the format should be as following : user:password@host:port (only the 'host' is mandatory)");
            }
            //must have host parameter
            Host = hostAndPort[0];
            if (hostAndPortLength == 2)
            {
                //port exist
                try
                {
                    Port = int.Parse(hostAndPort[1]);
                }
                catch (FormatException e)
                {
                    throw new ApiClientException(e.Message);
                }
            }
            ProxySettingExist = true;
        }

        #endregion
    }
}
