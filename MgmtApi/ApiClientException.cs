using System;

namespace MgmtApi
{
    /// <summary>
    /// This class is a form of Throwable that indicates the conditions that a reasonable application might want to catch.
    /// This class is a Checked exception, needing to be declared in a method or constructor's throws clause if they can be
    /// thrown by the execution of the method or constructor and propagated outside the method or constructor boundary.
    /// </summary>
    public class ApiClientException : Exception
    {
        #region ApiException Methods

        internal ApiClientException()
        {
        }

        internal ApiClientException(string message)
        : base(message)
        {
        }

        internal ApiClientException(string message, Exception inner)
        : base(message, inner)
        {
        }

        #endregion
    }
}
