

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;



namespace MyNextBook.Helpers
{
    static public class ErrorHandler
    {


        static public string GetCallStack()
        {
            StackTrace stackTrace = new StackTrace();
            return stackTrace.ToString();
        }

        static public void AddLog(string message, [CallerMemberName] string memberName = "",
                [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {

            sourceFilePath = sourceFilePath.Substring(sourceFilePath.LastIndexOf("\\") + 1);

            /*
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("^Member/Function name: " + memberName + " : " + message);
            sb.AppendLine("^Source file path: " + sourceFilePath);
            sb.AppendLine("^Source line number: " + sourceLineNumber);
            sb.AppendLine("^Message: " + memberName + " : " + message);
            */
            string s = $"^^ {message} : {memberName} line# {sourceLineNumber}  : {sourceFilePath} ";
            System.Diagnostics.Debug.WriteLine(s);
        
        }
        static public void AddError(Exception ex, [CallerMemberName] string memberName = "",
    [CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
        {
            /*
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("^Member/Function name: " + memberName);
            sb.AppendLine("^Source file path: " + sourceFilePath);
            sb.AppendLine("^Source line number: " + sourceLineNumber);
            sb.AppendLine("^Message: " + memberName);
            sb.AppendLine("Ex:" + ex.Message);
            */

            //todo: add method to track errors/send errors...
            //Crashes.TrackError(ex);
            SentrySdk.CaptureException(ex);
            string s = $"^^ {ex.Message} : {memberName} : {sourceLineNumber}  : {sourceFilePath} : {ex.ToString()} ";

            System.Diagnostics.Debug.WriteLine(s);
            //Create log file

        }
    }
}
