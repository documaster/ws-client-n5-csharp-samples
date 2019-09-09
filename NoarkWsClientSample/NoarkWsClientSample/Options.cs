using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NoarkWsClientSample
{
    public class Options
    {
        [Option("idpaddr", Required = true, HelpText = "Idp server address")]
        public string IdpServerAddress { get; set; }

        [Option("clientid", Required = true, HelpText = "Idp Client Id")]
        public string ClientId { get; set; }

        [Option("clientsecret", Required = true, HelpText = "Idp Client Secret")]
        public string ClientSecret { get; set; }

        [Option("username", Required = true, HelpText = "Username")]
        public string Username { get; set; }

        [Option("password", Required = true, HelpText = "Password")]
        public string Password { get; set; }

        [Option("addr", Required = true, HelpText = "Server address")]
        public string ServerAddress { get; set; }

        [Option("cert", HelpText = "Path to certificate file")]
        public string CertificatePath { get; set; }

        [Option("certpass", HelpText = "Certificate password")]
        public string CertificatePass { get; set; }

        [Option("testdoc", Required = true, HelpText = "Path to test file")]
        public string TestDoc { get; set; }
    }
}
