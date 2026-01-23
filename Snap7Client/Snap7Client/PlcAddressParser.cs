using System.Text.RegularExpressions;

namespace Snap7ClientLib
{
    internal class PlcAddress
    {
        public int Db;
        public int Offset;
        public int Bit;
    }

    internal static class PlcAddressParser
    {
        public static PlcAddress Parse(string address)
        {
            // DB1.DBB30 | DB1.DBW22 | DB1.DBD26 | DB1.DBX2.0
            var match = Regex.Match(address, @"DB(\d+)\.DB([BWDX])(\d+)(?:\.(\d+))?");

            if (!match.Success)
                throw new ArgumentException("Invalid PLC address");

            return new PlcAddress
            {
                Db = int.Parse(match.Groups[1].Value),
                Offset = int.Parse(match.Groups[3].Value),
                Bit = match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 0
            };
        }
    }
}
