using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace NeoBlockAnalysis
{
    class toolHelper
    {
        public static decimal DecimalParse(string value)
        {
            return decimal.Parse(value, NumberStyles.Float);
        }
    }
}
