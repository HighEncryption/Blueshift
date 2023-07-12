namespace Blueshift
{
    using System;
    using System.Diagnostics;

    using JetBrains.Annotations;

    public static class Pre
    {
        [DebuggerStepThrough]
        [ContractAnnotation("arg:null=>halt")]

        public static void ThrowIfArgumentNull(object arg, string name)
        {
            if (arg == null)
            {
                throw new ArgumentNullException(name);
            }
        }

        [DebuggerStepThrough]
        [ContractAnnotation("arg:null=>halt")]
        public static void ThrowIfStringNullOrEmpty(string arg, string name)
        {
            if (string.IsNullOrEmpty(arg))
            {
                throw new ArgumentNullException(name);
            }
        }

        [DebuggerStepThrough]
        [ContractAnnotation("arg:null=>halt")]
        public static void ThrowIfStringNullOrWhiteSpace(string arg, string name)
        {
            if (string.IsNullOrWhiteSpace(arg))
            {
                throw new ArgumentNullException(name);
            }
        }

        [DebuggerStepThrough]
        public static void ThrowIsValueOutOfRange<T>(
            T value,
            string name,
            T? minValue = null,
            T? maxValue = null)
        where T : struct, IComparable<T>
        {
            if (minValue.HasValue && value.CompareTo(minValue.Value) < 0)
            {
                throw new ArgumentOutOfRangeException(name);
            }

            if (maxValue.HasValue && value.CompareTo(maxValue.Value) > 0)
            {
                throw new ArgumentOutOfRangeException(name);
            }
        }

        [ContractAnnotation("value:false=>halt")]
        public static void ThrowIfFalse(bool value, string message)
        {
            if (!value)
            {
                throw new ArgumentException(message);
            }
        }

        [ContractAnnotation("value:true=>halt")]
        public static void ThrowIfTrue(bool value, string message)
        {
            if (value)
            {
                throw new ArgumentException(message);
            }
        }

        public static void BreakIf(bool condition)
        {
            if (!condition)
            {
                return;
            }

#if DEBUG
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
#endif
        }

        [ContractAnnotation("condition:false=>halt")]
        public static void Assert(bool condition)
        {
            Assert(condition, null);
        }

        [ContractAnnotation("condition:false=>halt")]
        public static void Assert(bool condition, string message)
        {
            if (condition)
            {
                return;
            }

#if DEBUG
            Debugger.Break();
#endif

            if (message != null)
            {
                Debug.Fail(message);
            }
        }
    }
}
