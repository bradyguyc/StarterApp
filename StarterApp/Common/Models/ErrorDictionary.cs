using CommunityToolkit.Mvvm.ComponentModel;

using Microsoft.Maui.Storage;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StarterApp.Common.Models
{
    public class ErrorDetails
    {
        public string ErrorCode;
        public string ErrorTitle;
        public string WhatThisMeans;
        public string WhatYouCanDo;
        public string HelpLink;
        public string Type;
    }

    static public class ErrorDictionary
    {
        static ErrorDictionary()
        {
            LoadErrorsFromFile();
        }

        static public List<ErrorDetails> Errors { get; set; }

        static public void LoadErrorsFromFile()
        {
            //string path = "Resources/Raw/ErrorDetails.json";
            var stream = FileSystem.OpenAppPackageFileAsync("ErrorDetailsList.json").Result;

            var reader = new StreamReader(stream);

            var contents = reader.ReadToEnd();
           
                Errors = JsonConvert.DeserializeObject<List<ErrorDetails>>(contents);
            
        }

        static public ErrorDetails GetErrorDetails(string errorCode)
        {
            var error = Errors.FirstOrDefault(e => e.ErrorCode == errorCode);

            if (error == null)
            {
                ErrorDetails errorDetail = new ErrorDetails
                {
                    ErrorCode = "mnb-999",
                    ErrorTitle = "Error code not defined: " + errorCode,
                    WhatThisMeans = "The error code is not in the list of know error codes. More than likely this is a developer problem and will be resolved with an upcoming release",
                    WhatYouCanDo = "If you have opted in to share analytics and errors with the developer data related to this situation will be provided to the developer so that they can provide a fix with a future release",
                    Type="Error",
                    HelpLink = "http://helpsite.com/error1"
                };
            }
            return error;
        }
    }

  
}
