using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MiddleweightReflection
{
    /// <summary>
    /// Thrown by some APIs that are expected to be caught. (These can't be avoided because
    /// there are call frames in between from the System.Reflection.Metadata assembly.)
    /// </summary>
    public class MrException : Exception
    {
        public MrException(string message)
            : base(message)
        {
        }
    }
}
