using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace MiddleweightReflection
{
    internal static class MrHelpers
    {
        /// <summary>
        /// Converts a StringHandle to a String
        /// </summary>
        internal static string AsString(this StringHandle stringHandle, MrAssembly assembly )
        {
            return assembly.Reader.GetString(stringHandle);
        }

        /// <summary>
        /// Converts a ConstantTypeCode to a PrimitiveTypeCode
        /// </summary>
        internal static PrimitiveTypeCode ToPrimitiveTypeCode(this ConstantTypeCode constantTypeCode)
        {
            switch (constantTypeCode)
            {
                case ConstantTypeCode.Boolean:
                    return PrimitiveTypeCode.Boolean;

                case ConstantTypeCode.Char:
                    return PrimitiveTypeCode.Char;

                case ConstantTypeCode.SByte:
                    return PrimitiveTypeCode.SByte;

                case ConstantTypeCode.Byte:
                    return PrimitiveTypeCode.Byte;

                case ConstantTypeCode.Int16:
                    return PrimitiveTypeCode.Int16;

                case ConstantTypeCode.UInt16:
                    return PrimitiveTypeCode.UInt16;

                case ConstantTypeCode.Int32:
                    return PrimitiveTypeCode.Int32;

                case ConstantTypeCode.UInt32:
                    return PrimitiveTypeCode.UInt32;

                case ConstantTypeCode.Int64:
                    return PrimitiveTypeCode.Int64;

                case ConstantTypeCode.UInt64:
                    return PrimitiveTypeCode.UInt64;

                case ConstantTypeCode.Single:
                    return PrimitiveTypeCode.Single;

                case ConstantTypeCode.Double:
                    return PrimitiveTypeCode.Double;

                case ConstantTypeCode.String:
                    return PrimitiveTypeCode.String;

                default:
                    throw new MrException("Invalid constant type code");

            }
        }
    }
}