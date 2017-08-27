using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Net;
using Newtonsoft.Json;
using System.Threading;
using Newtonsoft.Json.Linq;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace MgmtApi
{
    /// <summary>
    ///  Summary of the principal methods:
    /// (1) getServerFingerprint       - Returns the server fingerprint.
    /// (2) getFingerprintFromFile     - Returns the server fingerprint which is saved in the local fingerprint file.
    /// (3) checkFingerprintValidity   - Returns true if the fingerprint from the file equals to the server fingerprint, otherwise false.
    /// (4) saveFingerprintToFile      - saves the server fingerprint to the local fingerprint file.
    /// (5) deleteFingerprintFromFile  - delete a given server from the fingerprint file.
    /// </summary>
    public class FingerprintManager
    {
        #region Constants

        private const string FingerprintKey = "fingerprint-sha1";
        private const int DefaultPort = 443;
        private const int Timeout = 30;

        #endregion

        #region Private Members

        private readonly ReaderWriterLock _rwFile = new ReaderWriterLock();

        //The setting for tunneling through proxy
        private ApiProxySettingsProcessor _proxySettings;

        #endregion

        #region Properties

        public string FingerprintFile { get; private set; }

        #endregion

        #region Contractors

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="path">The fingerprint file name. </param>
        /// <param name="proxySettings">The proxy setting [user,password,server,port]</param>
        internal FingerprintManager(string path, ApiProxySettingsProcessor proxySettings) 
        {
            SetFingerprintFile(path);
            _proxySettings = proxySettings;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// This function replaces the fingerprints file with a new fingerprint file.
        /// If the file doesn't exists the function creates the file.       
        /// </summary>
        /// <param name="fingerprintFile">The name of the new fingerprint file</param>
        private void SetFingerprintFile(string fingerprintFile)
        {
            if (string.IsNullOrEmpty(fingerprintFile))
            {
                throw new ApiClientException("Error: file name is invalid");
            }

            string pathFile = Path.Combine(fingerprintFile);

            if (!File.Exists(pathFile))
            {
                using (FileStream fs = File.Create(pathFile))
                {
                    byte[] info = new UTF8Encoding(true).GetBytes("{}");
                    fs.Write(info, 0, info.Length);
                }
            }
            FingerprintFile = pathFile;
         }

        /// <summary>
        /// This function builds the string to be written to the fingerprint file
        /// </summary>
        /// <param name="server"></param>
        /// <param name="port"></param>
        /// <returns>string with following format: {server + ":" + port}</returns>
        private string buildStringToFingerprintFile(string server, int port)
        {
            return string.Format("{0}:{1}", server, port);
        }

        /// <summary>
        /// This function reads the fingerprint of the management server from the 'api fingerprint' command.
        /// This function should be used only when running on the management server.
        /// </summary>
        /// <returns> <see cref="JArray"/> that contains json object of the fingerprints</returns>
        private JArray GetFingerprintFromApiFingerprintUtil()
        {
            ProcessStartInfo psi = new ProcessStartInfo("api") { Arguments = "fingerprint -f json" };
            psi.RedirectStandardError = true;
            psi.RedirectStandardInput = true;
            Process exeProcess;

            try
            {
                using (exeProcess = Process.Start(psi))
                {
                    if (exeProcess == null)
                    {
                        throw new ApiClientException("Failed to get fingerprint from 'api fingerprint' command.\n" +
                                                        "Check that you are running on the Management Server. ");
                    }
                    exeProcess.WaitForExit();
                }
            }
            catch (IOException e)
            {
                throw new ApiClientException("Failed to get fingerprint from 'api fingerprint' command.\n" +
                                                         "Check that you are running on the Management Server. " +
                                                         "Error message:" + e.Message);
            }

            StreamReader reader = exeProcess.StandardOutput;
            if (reader == null)
            {
                throw new ApiClientException("Can't read API fingerprint from 'api fingerprint' response");
            }

            exeProcess.Close();
            string responseBodyString = reader.ReadToEnd();
            JArray result;

            try
            {
                result = JArray.Parse(responseBodyString);
            }
            catch (Exception)
            {
                //try to parse the response to text format (may occur on prior versions to R80.10)
                result = ParseFingerprintFromApiFingerprint(responseBodyString);
            }
            return result;
        }

        /// <summary>
        /// This function parses the 'api fingerprint -f json' response to json array that contain the server's fingerprint
        /// </summary>
        /// <param name="responseBodyString">'api fingerprint -f json' response</param>
        /// <returns><see cref="JArray"/> that contains json object of the fingerprints</returns>
        private JArray ParseFingerprintFromApiFingerprint(string responseBodyString)
        {
            try
            {
                string[] fingerprintApiSplit = responseBodyString.Split(new string[] { "English" }, StringSplitOptions.None);
                if (fingerprintApiSplit.Length < 1)
                {
                    throw new ApiClientException("Parsing 'api fingerprint' response failed.");
                }
                //get fingerprint value
                String[] fingerprintStringSplit = fingerprintApiSplit[0].Split(new string[] { "=" }, StringSplitOptions.None);
                if (fingerprintStringSplit.Length < 2)
                {
                    throw new ApiClientException("Parsing 'api fingerprint' response failed.");
                }
                string fingerprintValue = fingerprintStringSplit[1];
                string fingerprintValueInAsItInJsonFormat = fingerprintValue.Replace(":", "");

                //create the result in json format
                JObject fingerprintObject = new JObject();
                fingerprintObject.Add(FingerprintKey, fingerprintValueInAsItInJsonFormat);
                JArray result = new JArray();
                result.Add(fingerprintObject);
                return result;
            }
            catch (FormatException e)
            {
                throw new ApiClientException("Can't read API external port " + e.Message);
            }
       }

        #endregion

        #region Public Methods

        /// <summary>
        /// Returns the server's fingerprint from a local fingerprints file.
        /// </summary>
        /// <param name="server">>The IP address or name of the Check Point Management Server.</param>
        /// <returns>The server's fingerprint from the file if exists, else return null.</returns>
        public string GetFingerprintFromFile(string server)
        {
            return GetFingerprintFromFile(server, DefaultPort);
        }

        /// <summary>
        /// Returns the server's fingerprint from a local fingerprints file.
        /// </summary>
        /// <param name="server">The IP address or name of the Check Point Management Server.</param>
        /// /// <param name="port">The port number on management server</param>
        /// <returns>The server's fingerprint from the file if exists, else return null.</returns>
        public string GetFingerprintFromFile(string server, int port)
        {
            _rwFile.AcquireReaderLock(Timeout);
            string buffer;
            Dictionary<string, string> fingerprints;
            string serverAndPort = buildStringToFingerprintFile(server, port);
            try
            {
                // read the file to a buffer
                buffer = File.ReadAllText(FingerprintFile);
                fingerprints = JsonConvert.DeserializeObject<Dictionary<string, string>>(buffer);
            }
            catch (Exception)
            {
                throw new ApiClientException("Error: failed to get the fingerprint from the local fingerprint file");
            }
            finally {
                _rwFile.ReleaseReaderLock();
            }

            return fingerprints.ContainsKey(serverAndPort) ? fingerprints[serverAndPort] : null;
        }

        /// <summary>
        /// This function checks if the server's fingerprint, which is stored in the local fingerprints file,
        /// is equal to the server's fingerprint that is extracted from the server certificate.
        /// </summary>
        /// <param name="server">IP address or name of the Check Point Management Server.</param>
        /// <returns>True if the server's fingerprints from the file equals to the one extracted from the certificate, otherwise False. </returns>
        public bool CheckFingerprintValidity(string server)
        {
            return CheckFingerprintValidity(server, DefaultPort);
        }

        /// <summary>
        /// This function checks if the server's fingerprint, which is stored in the local fingerprints file,
        /// is equal to the server's fingerprint that is extracted from the server certificate.
        /// </summary>
        /// <param name="server">IP address or name of the Check Point Management Server.</param>
        /// / <param name="port">The port number on management server</param>
        /// <returns>True if the server's fingerprints from the file equals to the one extracted from the certificate, otherwise False. </returns>
        public bool CheckFingerprintValidity(string server, int port)
        {
            string fingerprint = GetFingerprintFromFile(server, port);

            if (string.IsNullOrEmpty(fingerprint))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(server))
            {
                string fingerprintServer = GetServerFingerprint(server, port);
                return fingerprint.Equals(fingerprintServer, StringComparison.Ordinal); 
            }

            return false;
        }

        /// <summary>
        /// This function removes the sever's fingerprint from the fingerprint file.
        /// If server's IP address wasn't stored in the local file, the function doesn't do anything.
        /// </summary>
        /// <param name="server">IP address or name of the Check Point Management Server.</param>
        public void DeleteFingerprintFromFile(string server)
        {
            DeleteFingerprintFromFile(server, DefaultPort);
        }

        /// <summary>
        /// This function removes the sever's fingerprint from the fingerprint file.
        /// If server's IP address wasn't stored in the local file, the function doesn't do anything.
        /// </summary>
        /// <param name="server">IP address or name of the Check Point Management Server.</param>
        /// <param name="port">The port number on management server</param>
        public void DeleteFingerprintFromFile(string server, int port)
        {
            _rwFile.AcquireWriterLock(Timeout);
            try
            {
                // read the file to a buffer
                string buffer = File.ReadAllText(FingerprintFile);

                Dictionary<string, string> fingerprints = JsonConvert.DeserializeObject<Dictionary<string, string>>(buffer);

                string serverAndPort = buildStringToFingerprintFile(server, port);
                if (fingerprints.ContainsKey(serverAndPort))
                {
                    fingerprints.Remove(serverAndPort);
                    string json = JsonConvert.SerializeObject(fingerprints, Formatting.Indented);
                    File.WriteAllText(FingerprintFile, json);
                }
            }
            finally
            {
                _rwFile.ReleaseWriterLock();
            }
        }

        /// <summary>
        /// This function initiates an HTTPS connection to the server and extracts the fingerprint from the server's certificate.
        /// The port set to default value: <see cref="DefaultPort"/>
        /// </summary>
        /// <param name="server">The IP address or name of the Check Point Management Server</param>
        /// <returns>The fingerprint. </returns>
        public string GetServerFingerprint(string server)
        {
            return GetServerFingerprint(server, DefaultPort);
        }

        /// <summary>
        /// This function initiates an HTTPS connection to the server and extracts the fingerprint from the server's certificate.
        /// </summary>
        /// <param name="serverIpAdress">The IP address or name of the Check Point Management Server.</param>
        /// <param name="port">The port number on management server</param>
        /// <returns>The fingerprint.</returns>
        public string GetServerFingerprint(string serverIpAdress, int port)
        {
            HttpWebRequest request;
            // send request to server
            try {
                // always accept certificate
                ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => { return true; };
                string httpsRequest = string.Format("https://{0}:{1}/web_api/", serverIpAdress, port);
                request = (HttpWebRequest)WebRequest.Create(httpsRequest);
                request.AllowAutoRedirect = false;

                if (_proxySettings.ProxySettingExist)
                {
                    WebProxy webProxy = new WebProxy(_proxySettings.Host, _proxySettings.Port) { BypassProxyOnLocal = false };
                    if (!string.IsNullOrEmpty(_proxySettings.UserName))
                    {
                        webProxy.Credentials = new NetworkCredential(_proxySettings.UserName, _proxySettings.Password);
                    }
                    request.Proxy = webProxy;
                }              
            }
            catch (Exception)
            {
                throw new ApiClientException(string.Format("ERROR: failed get the fingerprint from the server: {0} and port: {1}", serverIpAdress, port));
            }

            // get response from server
            try
            {
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            }
            catch (WebException e)
            {
                if (e.Response == null)
                {
                    throw new ApiClientException(string.Format("ERROR: failed get the fingerprint from the server: {0} and port: {1}", serverIpAdress, port));
                }
                // ignore any exception, it's likely an exception would be raised because of the http unauthorized status response
            }

            X509Certificate cert = request.ServicePoint.Certificate;
            return (cert != null) ? cert.GetCertHashString() : string.Empty; // cert would be null if the server didn't respond/unreachable, return an empty string then
        }

        /// <summary>
        /// This function stores the server's fingerprint into a local file.
        /// If the server's IP address was already stored in the local file its fingerprint is updated.
        /// </summary>
        /// <param name="server">IP address or name of the Check Point Management Server</param>
        /// <param name="fingerprint">A SHA1 fingerprint of the server's certificate.</param>
        public void SaveFingerprintToFile(string server, string fingerprint)
        {
            SaveFingerprintToFile(server, fingerprint, DefaultPort);
        }

        /// <summary>
        /// This function stores the server's fingerprint into a local file.
        /// If the server's IP address was already stored in the local file its fingerprint is updated.
        /// </summary>
        /// <param name="server">IP address or name of the Check Point Management Server</param>
        /// <param name="fingerprint">A SHA1 fingerprint of the server's certificate.</param>
        /// <param name="port">The port number on management server</param>
        public void SaveFingerprintToFile(string server, string fingerprint, int port)
        {
            // check if filename is legitimate, or the fingerprint is invalid. If so, return false
            if (string.IsNullOrEmpty(server) || string.IsNullOrEmpty(fingerprint))
            {
                throw new ApiClientException("Error: the server IP address or the fingerprint is invalid");
            }

            _rwFile.AcquireWriterLock(Timeout);
            try
            {
                // read the file to a buffer
                string buffer = File.ReadAllText(FingerprintFile);

                JObject fingerprints = JObject.Parse(buffer);

                string serverAndPort = buildStringToFingerprintFile(server, port);
                if (fingerprints[serverAndPort] == null || !string.Equals(fingerprints[serverAndPort].ToString(), fingerprint, StringComparison.OrdinalIgnoreCase))
                {
                    fingerprints[serverAndPort] = fingerprint;
                    string json = fingerprints.ToString();
                    File.WriteAllText(FingerprintFile, json);
                }
            }
            catch (Exception)
            {
                throw new ApiClientException("Error: failed to write fingerprint to the local fingerprint file");
            }
            finally
            {
                _rwFile.ReleaseWriterLock();
            }
        }

        #endregion
    }
}
