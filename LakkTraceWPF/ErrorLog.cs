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
            return dirName + "\\"+ functionName + "_" + DateTime.Today.ToString("yyyyMMdd") + "_" + DateTime.Now.ToString("HHmmss", System.Globalization.DateTimeFormatInfo.InvariantInfo)+".xml";
        }

        private static string generateFileInpuit(string errorMessage, string functionName,string prod, string carr, string mID, string hID)
        {
            string input = @"<ErrorMessage> 
                                <Data> 
                                    <Function>"+functionName+@"</Function> 
                                    <Date>" + DateTime.Today.ToString("yyyy.MM.dd") + @"</Date>
                                    <Time>"+ DateTime.Now.ToString("HH:mm:ss", System.Globalization.DateTimeFormatInfo.InvariantInfo) + @"</Time>
                                </Data>
                                <Input>
                                    <productTxbx.Text>"+prod+ @"</productTxbx.Text>
                                    <carrierTxbx.Text>" + carr+ @"</carrierTxbx.Text>
                                    <mainboardID>" + mID + @"</mainboardID>
                                    <heatsinkID>" + hID + @"</heatsinkID>
                                </Input>
                                <Message> 
                                    " + errorMessage+@"
                                </Message>
                             </ErrorMessage>";
            //productTxbx.Text,carrierTxbx.Text,mainboardID,heatsinkID
            return input;
        }

        public static void Create(string functionName, string errorMessage,string prod = "", string carr = "", string mID = "", string hID = "")
        {
            File.WriteAllText(generateFileName(functionName),generateFileInpuit(errorMessage, functionName, prod,carr,mID,hID));
        }
    }
}
