using System;
using System.Text;

namespace MsiZapEx
{
    internal static class GuidEx
    {
        public static string MsiObfuscate(this Guid g)
        {
            string orig = g.ToString("N").ToUpper();
            StringBuilder obfus = new StringBuilder(orig);

            obfus[0] = orig[7];
            obfus[7] = orig[0];

            obfus[1] = orig[6];
            obfus[6] = orig[1];

            obfus[2] = orig[5];
            obfus[5] = orig[2];

            obfus[3] = orig[4];
            obfus[4] = orig[3];

            obfus[8] = orig[11];
            obfus[11] = orig[8];

            obfus[9] = orig[10];
            obfus[10] = orig[9];

            obfus[12] = orig[15];
            obfus[15] = orig[12];

            obfus[13] = orig[14];
            obfus[14] = orig[13];

            obfus[16] = orig[17];
            obfus[17] = orig[16];

            obfus[18] = orig[19];
            obfus[19] = orig[18];

            obfus[20] = orig[21];
            obfus[21] = orig[20];

            obfus[22] = orig[23];
            obfus[23] = orig[22];

            obfus[24] = orig[25];
            obfus[25] = orig[24];

            obfus[26] = orig[27];
            obfus[27] = orig[26];

            obfus[28] = orig[29];
            obfus[29] = orig[28];

            obfus[30] = orig[31];
            obfus[31] = orig[30];

            return obfus.ToString();
        }

        public static Guid MsiObfuscate(this string obfucatedGuid)
        {
            return new Guid(new Guid(obfucatedGuid).MsiObfuscate());
        }
    }
}