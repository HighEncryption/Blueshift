namespace Blueshift
{
    using System;
    using System.Runtime.Serialization;
    using System.Security;

    using Blueshift.MicrosoftGraph.Model;

    /// <summary>
    /// Represents an exception thrown by the OneDrive adapter.
    /// </summary>
    [Serializable]
    public class TokenRefreshFailedException : Exception
    {
        public IdentityPlatformError ErrorData { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenRefreshFailedException"/> class. 
        /// </summary>
        /// <param name="message">
        /// The message that describes the error
        /// </param>
        public TokenRefreshFailedException(string message)
            : base(message)
        {
        }

        public TokenRefreshFailedException(string message, IdentityPlatformError errorData)
            : base(message)
        {
            this.ErrorData = errorData;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenRefreshFailedException"/> class. 
        /// </summary>
        /// <param name="message">
        /// The message that describes the error
        /// </param>
        /// <param name="innerException">
        /// The exception that is the cause of the current exception
        /// </param>
        public TokenRefreshFailedException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenRefreshFailedException"/> class with serialization data
        /// </summary>
        /// <param name="info">
        /// The <see cref="SerializationInfo"/> that holds the serialized object data about the exception being thrown.
        /// </param>
        /// <param name="context">
        /// The <see cref="SerializationInfo"/> that contains contextual information about the source or destination.
        /// </param>
        protected TokenRefreshFailedException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }
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
    }
}
