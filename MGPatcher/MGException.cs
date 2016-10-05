using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MGPatcher
{
    public class MGException: System.Exception
    {

        public MGException()
        {
        }

        public MGException(string message)
            : base(message)
        {
        }
    }
}
