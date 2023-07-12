namespace Blueshift.MicrosoftGraph
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Net.Http;
    using System.Runtime.Serialization;
    using System.Security;

    using Blueshift.MicrosoftGraph.Model;

    /// <summary>
    /// Represents an exception thrown by the OneDrive adapter.
    /// </summary>
    [Serializable]
    public class GraphHttpException : Exception
    {
        public HttpStatusCode StatusCode { get; set; }

        public string HttpStatusMessage { get; set; }

        public GraphError ErrorResponse { get; set; }

        public Dictionary<string, IList<string>> ResponseHeaders { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphHttpException"/> class. 
        /// </summary>
        /// <param name="message">
        /// The message that describes the error
        /// </param>
        public GraphHttpException(string message)
            : base(message)
        {
            this.ResponseHeaders = new Dictionary<string, IList<string>>();
        }

        public GraphHttpException(string message, HttpStatusCode statusCode)
            : base(message)
        {
            this.ResponseHeaders = new Dictionary<string, IList<string>>();

            this.StatusCode = statusCode;
            this.HttpStatusMessage = Enum.GetName(typeof(HttpStatusCode), statusCode);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphHttpException"/> class. 
        /// </summary>
        /// <param name="message">
        /// The message that describes the error
        /// </param>
        /// <param name="innerException">
        /// The exception that is the cause of the current exception
        /// </param>
        public GraphHttpException(string message, Exception innerException)
            : base(message, innerException)
        {
            this.ResponseHeaders = new Dictionary<string, IList<string>>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GraphHttpException"/> class with serialization data
        /// </summary>
        /// <param name="info">
        /// The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.
        /// </param>
        /// <param name="context">
        /// The <see cref="StreamingContext"/> that contains contextual information about the source or destination.
        /// </param>
        protected GraphHttpException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            this.ResponseHeaders = new Dictionary<string, IList<string>>();
        }

        /// <summary>
        /// Gets the <see cref="SerializationInfo"/> with information about the exception.
        /// </summary>
        /// <param name="info">
        /// The <see cref="SerializationInfo"/> that holds the serialized object data 
        /// about the exception being thrown.
        /// </param>
        /// <param name="context">
        /// The <see cref="StreamingContext"/> that contains contextual information 
        /// about the source or destination.
        /// </param>
        [SecurityCritical]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            base.GetObjectData(info, context);
        }

        public static Exception FromResponse(HttpResponseMessage response, GraphErrorContainer errorContainer)
        {
            string exceptionMessage = string.Format("The server returned {0} ({1})", (int)response.StatusCode,
                response.ReasonPhrase);

            var exception = new GraphHttpException(exceptionMessage, response.StatusCode);
            if (errorContainer != null)
            {
                exception.ErrorResponse = errorContainer.Error;
            }

            foreach (KeyValuePair<string, IEnumerable<string>> responseHeader in response.Headers)
            {
                exception.ResponseHeaders.Add(responseHeader.Key, new List<string>(responseHeader.Value));
            }

            return exception;
        }
    }
}
