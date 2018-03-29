using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace LakkTraceWPF
{
    class ErrorLog
    {
        private static string dirName = "Errors"; 
        private static string generateFileName(string functionName)
        {
            //create Directory if not exits
            Directory.CreateDirectory("Errors");

            //return the path + filename.xml
            return dirName + "\\"+ functionName + "_" + DateTime.Today.ToString("yyyyMMdd") + "_" + DateTime.Now.ToString("HHmmss", System.Globalization.DateTimeFormatInfo.InvariantInfo)+".html";
        }

        private static string generateFileInpuit(string errorMessage, string functionName,string prod, string carr, string mID, string hID)
        {
            string input = @"<HTML> 
                                <body>
                                    <label>Function: <b>" + functionName + @"</b></label><br>
                                    <label>Date: <b>" + DateTime.Today.ToString("yyyy.MM.dd") + @"</b></label><br>
                                    <label>Time: <b>" + DateTime.Now.ToString("HH:mm:ss", System.Globalization.DateTimeFormatInfo.InvariantInfo) + @"</b></label><br>
                                <br>
                                    <label>productTxbx.Text: <b>" + prod + @"</b></label> <br>
                                    <label>carrierTxbx.Text: <b>" + carr + @"</b></label> <br>
                                    <label>mainboardID: <b>" + mID + @"</b></label> <br>
                                    <label>heatsinkID: <b>" + hID + @"</b></label>
                                <br>
                                    <h3>Exception:</h3>
                                     " + errorMessage + @"
                                </body>
                             </HTML>";
            return input;
        }

        public static void Create(string functionName, string errorMessage,string prod = "", string carr = "", string mID = "", string hID = "")
        {
            File.WriteAllText(generateFileName(functionName),generateFileInpuit(errorMessage, functionName, prod,carr,mID,hID));
        }
    }
}
