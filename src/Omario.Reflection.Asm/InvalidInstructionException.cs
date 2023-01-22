using System;
using System.Runtime.Serialization;

namespace Omario.Reflection.Asm
{
    public class InvalidInstructionException : Exception
    {
        public InvalidInstructionException()
        {
        }

        public InvalidInstructionException(string message) : base(message)
        {
        }

        public InvalidInstructionException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
