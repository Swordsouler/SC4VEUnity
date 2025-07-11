using System;
using System.Linq;

namespace Sven.Demo
{
    public static class DemoManager
    {
        public static string graphName = "default";
        public static int semantisationFrequency = 10;
        private static Uri _endpointUri;
        public static Uri EndpointUri
        {
            get
            {
                if (_endpointUri == null)
                {
                    // Get the command line arguments
                    string[] args = Environment.GetCommandLineArgs();
                    string endpointUriString = args.FirstOrDefault(arg => arg.StartsWith("--endpointUri="))?.Split('=')[1];

                    if (!string.IsNullOrEmpty(endpointUriString) && Uri.TryCreate(endpointUriString, UriKind.Absolute, out Uri parsedUri))
                    {
                        _endpointUri = parsedUri;
                    }
                    else
                    {
                        // Default to localhost if no valid endpoint URI is provided
                        _endpointUri = new Uri("http://localhost:7200/repositories/Default");
                    }
                }

                return _endpointUri;
            }
        }
    }
}
