namespace Blueshift
{
    using System.Runtime.Serialization;

    [Serializable]
    public class ProcessRunningException : Exception
    {
        public ProcessRunningException(string message)
            : base(message)
        {
        }

        public ProcessRunningException(string message, Exception inner)
            : base(message, inner)
        {
        }

        protected ProcessRunningException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
}