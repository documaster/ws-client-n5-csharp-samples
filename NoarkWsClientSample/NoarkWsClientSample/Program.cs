using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandLine;
using Documaster.WebApi.Client.Noark5;
using Documaster.WebApi.Client.Noark5.Client;
using Documaster.WebApi.Client.Noark5.NoarkEntities;
using Documaster.WebApi.Client.IDP;
using Documaster.WebApi.Client.IDP.Oauth2;
using NoarkWsClientSample.eByggesak;

namespace NoarkWsClientSample
{
    static class Program
    {
        public static void Main(string[] args)
        {
            var options = ParserCommandLineArguments(args);

            Samples samples = new Samples(options);

            samples.JournalingSample();
            samples.ArchiveSample();
            samples.MeetingAndBoardHandlingDataSample();
            samples.BusinessSpecificMetadataSample();
            samples.CodeListsSample();

            EByggesakIntegrationSample eByggesakIntegrationSample = new EByggesakIntegrationSample(options);
            eByggesakIntegrationSample.ExecuteSample();
        }

        private static Options ParserCommandLineArguments(string[] args)
        {
            Options opts = null;

            var parseResult = Parser.Default
                .ParseArguments<Options>(args)
                .WithParsed(options => opts = options);

            if (parseResult.Tag == ParserResultType.NotParsed)
            {
                throw new Exception("Failed to parse command line arguments!");
            }

            return opts;
        }
    }
}
