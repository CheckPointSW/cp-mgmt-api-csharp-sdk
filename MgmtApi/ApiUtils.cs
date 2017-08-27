using System;

namespace MgmtApi
{
    public class ApiUtils
    {
        #region Check Fingerprint Methods

        /// <summary>
        /// This function compares the server's fingerprint and the server's fingerprint written in the
        /// file for equality. If they are equal, the function does noting, but if they are not, the function asks the user if
        /// he wishes to save the server's fingerprint to the file.
        /// If the answer is yes, the function adds the server's fingerprint to the file.
        /// If the answer is no or writing to the file failed, the function throws an exception
        /// </summary>
        /// <param name="client">The <see cref="ApiClient"/> that makes the call</param>
        /// <param name="server">The IP address or name of the Check Point Management Server</param>
        public static void CheckFingerprint(ApiClient client, string server)
        {
            CheckFingerprint(client, server, 443);
        }

        /// <summary>
        /// This function compares the server's fingerprint and the server's fingerprint written in the
        /// file for equality. If they are equal, the function does noting, but if they are not, the function asks the user if
        /// he wishes to save the server's fingerprint to the file.
        /// If the answer is yes, the function adds the server's fingerprint to the file.
        /// If the answer is no or writing to the file failed, the function throws an exception
        /// </summary>
        /// <param name="client">The <see cref="ApiClient"/> that makes the call</param>
        /// <param name="server">The IP address or name of the Check Point Management Server</param>
        ///<param name="port">Port number</param>
        public static void CheckFingerprint(ApiClient client, string server, int port)
        {
            // try to get server fingerprint from file
            string fingerprint = null;
            bool addFingerprintToFile = false;

            //The API client looks for the server's certificate SHA1 fingerprint from server
            try
            {
                fingerprint = client.FingerprintManager.GetServerFingerprint(server, port);
            }
            catch (ApiClientException e)
            {
                Console.WriteLine("Error: Could not get the server's fingerprint - Check connectivity with the server. " + e.Message);
                throw new ApiClientException("Error: Could not get the server's fingerprint - Check connectivity with the server. " + e.Message);
            }

            if (string.IsNullOrEmpty(fingerprint))
            {
                Console.WriteLine("Couldn't find your server's fingerprint in file " + client.FingerprintManager.FingerprintFile);
                throw new ApiClientException("Couldn't find your server's fingerprint in file " + client.FingerprintManager.FingerprintFile);
            }

            // The API client looks for the server's certificate SHA1 fingerprint in a file.
            string fingerprintFromFile = null;

            try
            {
                fingerprintFromFile = client.FingerprintManager.GetFingerprintFromFile(server, port);
            }
            catch (ApiClientException e)
            {
                Console.WriteLine("Error: Could not get the server's fingerprint from fingerprint file. " + e.Message);
                throw new ApiClientException("Error: Could not get the server's fingerprint from fingerprint file. " + e.Message);
            }

            if (string.IsNullOrEmpty(fingerprintFromFile))
            {
                string messageToAsk = string.Format("First connection to the server {0}\n\nTo verify server identity, compare the" +
                            " following fingerprint with the one displayed by the api management tool" +
                            " <api fingerprint>.\n\nSHA1 Fingerprint = {1} \n\n", server, PrintFingerprint(fingerprint.ToUpper()));
                string messageIfNotApproved = "First connection to the server and the fingerprint wasn't approved ";
                addFingerprintToFile = AskUserToApproveTheFingerprint(messageToAsk, messageIfNotApproved);
            }
            // fingerprint file contains server fingerprint and fingerprint from file doesn't match the fingerprint of server
            else if (!string.Equals(fingerprintFromFile, fingerprint, StringComparison.CurrentCultureIgnoreCase))
            {
                    string messageToAsk = string.Format("Fingerprint of server {0} was changed.\n\n" +
                                    "To protect server against impersonation, compare the following fingerprint " +
                                    "with the one displayed by the api management tool" +
                                    " <api fingerprint>.\n\nSHA1 Fingerprint = {1}\n\n", server, PrintFingerprint(fingerprint.ToUpper()));

                    string messageIfNotApproved = "Fingerprint of server was changed and the new fingerprint wasn't approved ";
                    addFingerprintToFile = AskUserToApproveTheFingerprint(messageToAsk, messageIfNotApproved);
             }

            //Adding the fingerprint to file
            if (addFingerprintToFile)
            {
                try
                {
                    client.FingerprintManager.SaveFingerprintToFile(server, fingerprint);
                    Console.WriteLine("Fingerprint saved to file.");
                }
                catch (ApiClientException e)
                {
                    Console.WriteLine("Could not save your fingerprint. Continuing.");
                    throw new ApiClientException("Failed to write fingerprint to the file. " + e.Message);
                }
            }
        }

        /// <summary>
        /// In first connection to the server the function asks the user if he wishes to save the server's fingerprint to the file.
        /// </summary>
        /// <param name="message">The message that need to be shown to the user</param>
        /// <param name="messageIfNotApproved">The message that need to be thrown in case of negative answer</param>
        /// <returns>True in case the fingerprint need to be written in to the fingerprint file, otherwise exception will be thrown.</returns>
        private static bool AskUserToApproveTheFingerprint(string messageToAsk, string messageIfNotApproved)
        {
            Console.WriteLine(messageToAsk);
            Console.WriteLine("Do you accept this fingerprint? (y/n)");
            if (string.Equals(Console.ReadLine(), "y", StringComparison.CurrentCultureIgnoreCase))
            {
                return true;
            }
            else
            {
                Console.WriteLine(messageIfNotApproved);
                Console.WriteLine("OK. Ending program. Press any key to continue..");
                throw new ApiClientException(messageIfNotApproved);
            }
        }

        /// <summary>
        /// Returns the given fingerprint string with ":" after every two characters
        /// </summary>
        /// <param name="fingerprint">The fingerprint</param>
        /// <returns>The string that represent the fingerprint with ":" after every two characters</returns>
        private static string PrintFingerprint(string fingerprint)
        {
            string fingerprintStr = fingerprint;
            int index = fingerprintStr.Length - 2;
            while (index > 0)
            {
                fingerprintStr = fingerprintStr.Insert(index, ":");
                index = index - 2;
            }
            return fingerprintStr;
        }

        #endregion

        /// <summary>
        /// This function prints the given line in the given color
        /// </summary>
        /// <param name="line">The line to be printed</param>
        /// <param name="color">The color of the line</param>
        public static void WriteLineColored(string line, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(line);
            Console.ForegroundColor = ConsoleColor.Gray;
        }

        /// <summary>
        /// This function exit the program.
        /// </summary>
        public static void EndProgram()
        {
            Console.WriteLine("Press any key to exit..");
            Console.ReadKey();
            Environment.Exit(0);
        }

    }
}
