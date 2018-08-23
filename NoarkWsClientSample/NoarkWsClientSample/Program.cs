using System;
using System.IO;
using System.Linq;
using CommandLine;
using Documaster.WebApi.Client.Noark5;
using Documaster.WebApi.Client.Noark5.Client;
using Documaster.WebApi.Client.Noark5.NoarkEntities;
using Documaster.WebApi.Client.IDP;

namespace NoarkWsClientSample
{
    class Program
    {
        private static NoarkClient client;
        private static IdpClient idpClient;
        static string testDoc;

        public static void Main(string[] args)
        {
            var options = ParserCommandLineArguments(args);
            InitializeSample(options);

            JournalingSample();
            ArchiveSample();
            BusinessSpecificMetadataSample();
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

        private static void InitializeSample(Options opts)
        {
            /*
             * Using the Noark 5 web services requires providing a valid access token.
             * The way this token is obtained depends on the system implementing the services.
             * This sample code obtains the token from the Documaster's identity provider service
             * with the help of a designated Documaster IDP client.
             * If the Noark client is used in the context of an application that has access to a web browser,
             * we strongly recommend choosing the Oauth2 Authorization Code Grant Flow supported for obtaining
             * access tokens.
             */

            //Initialize an IDP client and request an authorization token
            InitIdpClient(opts);
            var accessToken = idpClient.GetTokenWithPasswordGrantType(
                opts.ClientId, opts.ClientSecret, opts.Username, opts.Password).AccessToken;

            //Initialize a Noark client
            InitClient(opts);
            client.AuthToken = accessToken;

            testDoc = opts.TestDoc;
        }

        private static void InitIdpClient(Options options)
        {
            idpClient = new IdpClient(options.IdpServerAddress, true);
        }

        private static void InitClient(Options options)
        {
            client = new NoarkClient(options.ServerAddress, options.CertificatePath, options.CertificatePass, true);
        }

        private static void JournalingSample()
        {
            Console.WriteLine($"Journaling example {Environment.NewLine}");

            //Create a new Arkiv with an Arkivskaper
            //When new objects are initialized, a temporary Id is assigned to them.
            var newArkivskaper = new Arkivskaper("B7-23-W5", "John Smith");
            var newArkiv = new Arkiv("Arkiv");

            var transactionResponse = client.Transaction()
                .Save(newArkiv)
                .Save(newArkivskaper)
                .Link(newArkiv.LinkArkivskaper(newArkivskaper))
                .Commit();


            //When the transaction is committed, the transaction response contains a map with saved objects.
            //One can access the saved Arkiv by providing its temporary Id as a key to the map.
            //Notice that arkiv.Id is the permanent Id of the Arkiv.
            var arkiv = transactionResponse.Saved[newArkiv.Id] as Arkiv;
            Console.WriteLine(
                $"Created Arkiv: Id={arkiv.Id}, Tittel={arkiv.Tittel}, OpprettetDato={arkiv.OpprettetDato}");

            //Update the description of the Arkiv and create a new Arkivdel in it
            //Create a new Klassifikasjonssystem with one Klasse
            //Set the new Klassifikasjonssystem as the primary Klassifikasjonssystem for the Arkivdel
            arkiv.Beskrivelse = "Barnehage Arkiv";
            var newArkivdel = new Arkivdel("2007/8");
            var newKlassifikasjonssystem = new Klassifikasjonssystem("Barnehage");
            var newKlasse = new Klasse("01", "Tilbud");

            transactionResponse = client.Transaction()
                .Save(arkiv)
                .Save(newArkivdel)
                .Link(newArkivdel.LinkArkiv(arkiv))
                .Save(newKlassifikasjonssystem)
                .Link(newArkivdel.LinkPrimaerKlassifikasjonssystem(newKlassifikasjonssystem))
                .Save(newKlasse)
                .Link(newKlasse.LinkKlassifikasjonssystem(newKlassifikasjonssystem))
                .Commit();

            arkiv = transactionResponse.Saved[arkiv.Id] as Arkiv;
            Console.WriteLine($"Updated Arkiv: Id={arkiv.Id}, Beskrivelse={arkiv.Beskrivelse}");

            var arkivdel = transactionResponse.Saved[newArkivdel.Id] as Arkivdel;
            Console.WriteLine($"Created Arkivdel: Id={arkivdel.Id}, Tittel={arkivdel.Tittel}");

            var klassifikasjonssystemId = transactionResponse.Saved[newKlassifikasjonssystem.Id].Id;
            var klasseId = transactionResponse.Saved[newKlasse.Id].Id;

            //To screen an Arkivdel we should first search the system for available screening codes
            var screeningCodesList = client.CodeLists(field: "skjerming").First();
            Console.WriteLine($"Screening codes:");
            foreach (var code in screeningCodesList.Values)
            {
                Console.WriteLine($"    Code={code.Code}");
            }
            var screeningCode = screeningCodesList.Values.First();

            //Screen the Arkivdel
            arkivdel.Skjerming = new Skjerming(screeningCode.Code);
            transactionResponse = client.Transaction()
                .Save(arkivdel)
                .Commit();

            //Find the Arkivdel by id
            //By default the service will return null values for all screened fields of screened objects
            //To see the values of screened fields call SetPublicUse(false)
            var queryResults = client.Query<Arkivdel>("id=@arkivdelId", 10)
                .AddQueryParam("@arkivdelId", arkivdel.Id)
                .Execute();
            Console.WriteLine($"Found {queryResults.Results.Count()} Arkivdel object(s) with Id {arkivdel.Id}");

            //Print a screened field:
            arkivdel = queryResults.Results.First();
            Console.WriteLine($"Tittel of Arkivdel is masked: {arkivdel.Tittel}");

            //For convenience, objects in query and transaction responses contain the id's of many-to-one reference fields
            Console.WriteLine($"Arkivdel.RefArkiv: {arkivdel.RefArkiv}");
            Console.WriteLine($"Arkivdel.RefPrimaerKlassifikasjonssystem: {arkivdel.RefPrimaerKlassifikasjonssystem}");

            //Create two other Klassifikasjonssystem objects and link them to the Arkivdel as secondary Klassifikasjonssystem
            var sekundaerKlassifikasjonssystemSkole = new Klassifikasjonssystem("Skole");
            var klasseInSekundaerKlassifikasjonssystemSkole = new Klasse("07", "Report");
            var sekundaerKlassifikasjonssystem2 = new Klassifikasjonssystem("EOP");

            transactionResponse = client.Transaction()
                .Save(sekundaerKlassifikasjonssystemSkole)
                .Save(klasseInSekundaerKlassifikasjonssystemSkole)
                .Link(sekundaerKlassifikasjonssystemSkole.LinkKlasse(klasseInSekundaerKlassifikasjonssystemSkole))
                .Save(sekundaerKlassifikasjonssystem2)
                .Link(arkivdel.LinkSekundaerKlassifikasjonssystem(sekundaerKlassifikasjonssystemSkole,
                    sekundaerKlassifikasjonssystem2))
                .Commit();

            //We need the id of the saved Klasse for the next transactions
            var sekundaerKlasseId =
                transactionResponse.Saved[klasseInSekundaerKlassifikasjonssystemSkole.Id].Id;

            //Create a new Saksmappe in the Arkivdel
            //The code list value "admUnit" in the AdministrativEnhet code list must exists in the system!
            //The new Saksmappe needs to have a Klasse in the primary Klassifikasjonssystem of the Arkivdel
            //Also link the Saksmappe to a secondary Klasse
            var newSaksmappe = new Saksmappe("Tilbud (Smith, John)", new AdministrativEnhet("admUnit"));
            var newSakspart = new Sakspart("Alice", "internal");

            var savedObjects = client.Transaction()
                .Save(newSaksmappe)
                .Link(newSaksmappe.LinkArkivdel(arkivdel))
                .Link(newSaksmappe.LinkPrimaerKlasse(klasseId))
                .Link(newSaksmappe.LinkSekundaerKlasse(sekundaerKlasseId))
                .Save(newSakspart)
                .Link(newSaksmappe.LinkSakspart(newSakspart))
                .Commit()
                .Saved;

            var saksmappe = savedObjects[newSaksmappe.Id] as Saksmappe;
            Console.WriteLine($"Created Saksmappe: Id={saksmappe.Id}, Saksdato: {saksmappe.Saksdato}");

            //Create another Klasse
            //Unlink the Saksmappe from its Klasse and link it to the new Klasse
            var anotherKlasse = new Klasse("02", "Klage");

            client.Transaction()
                .Save(anotherKlasse)
                .Link(anotherKlasse.LinkKlassifikasjonssystem(klassifikasjonssystemId))
                .Unlink(saksmappe.UnlinkPrimaerKlasse(klasseId))
                .Link(saksmappe.LinkPrimaerKlasse(anotherKlasse))
                .Commit();
            Console.WriteLine(
                $"Unlinked Saksmappe wiht Id {saksmappe.Id} from Klasse '{newKlasse.Tittel}' and linked it to Klasse '{anotherKlasse.Tittel}'");

            //Find all available codes for journalstatus in Journalpost
            var journalstatusCodeList = client.CodeLists(type: "Journalpost", field: "journalstatus").First();
            Console.WriteLine($"CodeList list for {journalstatusCodeList.Type}.{journalstatusCodeList.Field}:");
            foreach (var code in journalstatusCodeList.Values)
            {
                Console.WriteLine($"    Code={code.Code}, Name={code.Name}");
            }

            //Create a new Journalpost in the Saksmappe
            //Create an EksternId object and link it to the Journalpost
            //Create a new Korrespondansepart and link it to the Journalpost
            //Create a Noekkelord (keyword) object and link it to the Journalpost
            var newJournalpost = new Journalpost("Tilbud (Smith, John, Godkjent)", Journalposttype.UTGAAENDE_DOKUMENT)
            {
                Journalaar = 2007,
                Journalsekvensnummer = 46
            };

            var newEksternId = new EksternId("External System", Guid.NewGuid().ToString());
            var newKorrespondansepart = new Korrespondansepart(Korrespondanseparttype.INTERN_MOTTAKER, "John Smith");
            var newNoekkelord = new Noekkelord("keyword");

            savedObjects = client.Transaction()
                .Save(newJournalpost)
                .Link(newJournalpost.LinkMappe(saksmappe))
                .Save(newEksternId)
                .Link(newJournalpost.LinkEksternId(newEksternId))
                .Save(newKorrespondansepart)
                .Link(newJournalpost.LinkKorrespondansepart(newKorrespondansepart))
                .Save(newNoekkelord)
                .Link(newNoekkelord.LinkRegistrering(newJournalpost))
                .Commit()
                .Saved;

            var journalPost = savedObjects[newJournalpost.Id] as Journalpost;
            Console.WriteLine(
                $"Created Journalpost: Id={journalPost.Id}, Tittel={journalPost.Tittel}, Journalstatus={journalPost.Journalstatus.Code}");

            //Find the Journalpost by the eksternID value
            var journalpstQueryResults = client.Query<Journalpost>("refEksternId.eksternID=@eksternId", 10)
                .AddQueryParam("@eksternId", newEksternId.EksternID)
                .Execute();
            Console.WriteLine(
                $"Found {journalpstQueryResults.Results.Count()} Journalpost objects with eksternID {newEksternId.EksternID}");

            //Upload a file
            Dokumentfil dokumentfil;
            using (var inputStream = File.OpenRead(testDoc))
            {
                dokumentfil = client.Upload(inputStream, "godkjenning.pdf");
            }
            Console.WriteLine($"Uploaded file {testDoc}");

            //Get available values for the Dokumenttype code list
            var dokumenttypeList = client.CodeLists("Dokument", "dokumenttype").First();
            if (dokumenttypeList.Values.Count == 0)
            {
                Console.WriteLine(
                    "Can not create an instance of Dokument because there are not available values in the Dokumenttype code list!");
                return;
            }
            var dokumentTypeCode = dokumenttypeList.Values.First().Code;

            //Create a new Dokument and Dokumentversjon using the uploaded file
            var newDokument = new Dokument(new Dokumenttype(dokumentTypeCode), "Tilbud (Smith, John, Godkjent)",
                TilknyttetRegistreringSom.HOVEDDOKUMENT);
            var newDokumentversjon = new Dokumentversjon(Variantformat.PRODUKSJONSFORMAT, ".pdf", dokumentfil);

            savedObjects = client.Transaction()
                .Save(newDokument)
                .Link(newDokument.LinkRegistrering(journalPost))
                .Save(newDokumentversjon)
                .Link(newDokumentversjon.LinkDokument(newDokument))
                .Commit()
                .Saved;

            var dokumentversjon = savedObjects[newDokumentversjon.Id] as Dokumentversjon;
            Console.WriteLine(
                $"Created Dokumentversjon: Id={dokumentversjon.Id}, Versjonsnummer: {dokumentversjon.Versjonsnummer}, Filstoerrelse: {dokumentversjon.Filstoerrelse}");

            //Download the Dokumentversjon file
            var downloadPath = Path.GetTempFileName();
            using (var outputStream = File.Create(downloadPath))
            {
                client.Download(dokumentversjon.Dokumentfil, outputStream);
            }
            Console.WriteLine($"Downloaded file {downloadPath}");

            //Find all dokument objects in a Saksmappe called "Tilbud (Smith, John)"
            //Results should be ordered by creation date in descending order
            var queryResponse = client.Query<Dokument>("refRegistrering.refMappe.tittel=@saksmappeTittel", 50)
                .AddQueryParam("@saksmappeTittel", "Tilbud (Smith, John)")
                .AddSortOrder("opprettetDato", Order.Descending)
                .Execute();
            Console.WriteLine(
                $"Query returned {queryResponse.Results.Count()} Dokument objects in Saksmappe objects called 'Tilbud (Smith, John)'");
            Console.WriteLine($"More results available: {queryResponse.HasMore}");

            //Delete the DokumentVersjon by id
            client.Transaction().Delete<Dokumentversjon>(dokumentversjon.Id).Commit();
            Console.WriteLine($"Deleted Dokumentversjon with Id {dokumentversjon.Id}");
            Console.WriteLine();
        }

        private static void ArchiveSample()
        {
            Console.WriteLine($"Archive example {Environment.NewLine}");

            //Create a new Arkiv with an Arkivskaper
            //Create a new Arkivdel in the Arkiv
            var newArkivskaper = new Arkivskaper("B7-23-W5", "John Smith");
            var newArkiv = new Arkiv("Arkiv");
            var newArkivdel = new Arkivdel("2007/8");

            var transactionResponse = client.Transaction()
                .Save(newArkiv)
                .Save(newArkivskaper)
                .Save(newArkivdel)
                .Link(newArkiv.LinkArkivskaper(newArkivskaper))
                .Link(newArkivdel.LinkArkiv(newArkiv))
                .Commit();

            var arkiv = transactionResponse.Saved[newArkiv.Id] as Arkiv;
            Console.WriteLine(
                $"Created Arkiv: Id={arkiv.Id}, Arkivstatus={arkiv.Arkivstatus.Code}, OpprettetDato={arkiv.OpprettetDato}");

            var arkivdel = transactionResponse.Saved[newArkivdel.Id] as Arkivdel;
            Console.WriteLine($"Created Arkivdel: Id={arkivdel.Id}, Arkivdelstatus={arkivdel.Arkivdelstatus.Code}");

            //Get all available values for the Mappetype code list
            var mappetypeList = client.CodeLists("Mappe", "mappetype").First();
            if (mappetypeList.Values.Count == 0)
            {
                Console.WriteLine(
                    "Can not create an instance of Mappe because there are not available values in the Mappetype code list!");
                return;
            }
            var mappetypeCode = mappetypeList.Values.First().Code;

            //Create a new Mappe
            var newMappe = new Mappe("Barnehage Tilbud")
            {
                Beskrivelse = "Mappe Beskrivelse",
                Mappetype = new Mappetype(mappetypeCode)
            };

            transactionResponse = client.Transaction()
                .Save(newMappe)
                .Link(newMappe.LinkArkivdel(arkivdel))
                .Commit();

            var mappe = transactionResponse.Saved[newMappe.Id] as Mappe;
            Console.WriteLine($"Created Mappe: Id={mappe.Id}, Tittel: {mappe.Tittel}");

            //Create a child Mappe in the Mappe
            var newBarnMappe = new Mappe("Tilbud (Smith, John)");

            var savedObjects = client.Transaction()
                .Save(newBarnMappe)
                .Link(newBarnMappe.LinkForelderMappe(mappe))
                .Commit()
                .Saved;

            var barnMappe = savedObjects[newBarnMappe.Id] as Mappe;
            Console.WriteLine(
                $"Created a new Mappe (Id={barnMappe.Id}, Tittel={barnMappe.Tittel}) in Mappe with Id {mappe.Id}");

            //Find all children of the Mappe
            var queryResults = client.Query<Mappe>("refForelderMappe.id=@forelderMappeId", 10)
                .AddQueryParam("@forelderMappeId", mappe.Id)
                .Execute();
            Console.WriteLine($"Found {queryResults.Results.Count()} Mappe objects in Mappe with Id {mappe.Id}");

            //Create a new Basisregistrering in the child Mappe
            //Link one Korrespondansepart to the Basisregistrering
            var newBasisregistrering = new Basisregistrering("Tilbud (Smith, John, Godkjent)");
            var newKorrespondansepart = new Korrespondansepart(Korrespondanseparttype.MOTTAKER, "John Smith");

            savedObjects = client.Transaction()
                .Save(newBasisregistrering)
                .Save(newKorrespondansepart)
                .Link(newBasisregistrering.LinkMappe(barnMappe))
                .Link(newBasisregistrering.LinkKorrespondansepart(newKorrespondansepart))
                .Commit()
                .Saved;

            var basisregistrering = savedObjects[newBasisregistrering.Id] as Basisregistrering;
            Console.WriteLine(
                $"Created Basisregistrering: Id={basisregistrering.Id}, Tittel={basisregistrering.Tittel}");

            //Upload a file
            Dokumentfil dokumentfil;
            using (var inputStream = File.OpenRead(testDoc))
            {
                dokumentfil = client.Upload(inputStream, "godkjenning.pdf");
            }
            Console.WriteLine($"Uploaded file {testDoc}");

            //Get available values for the Dokumenttype code list
            var dokumenttypeList = client.CodeLists("Dokument", "dokumenttype").First();
            if (dokumenttypeList.Values.Count == 0)
            {
                Console.WriteLine(
                    "Can not create an instance of Dokument because there are not available values in the Dokumenttype code list!");
                return;
            }
            var dokumenttypeCode = dokumenttypeList.Values.First().Code;

            //Create a new Dokument and Dokumentversjon using the uploaded file
            //Link the Dokument to the Basisregistrering
            var newDokument = new Dokument(new Dokumenttype(dokumenttypeCode), "Tilbud (Smith, John, Godkjent)",
                TilknyttetRegistreringSom.HOVEDDOKUMENT);
            var newDokumentversjon = new Dokumentversjon(Variantformat.PRODUKSJONSFORMAT, ".pdf", dokumentfil);

            savedObjects = client.Transaction()
                .Save(newDokument)
                .Link(newDokument.LinkRegistrering(basisregistrering))
                .Save(newDokumentversjon)
                .Link(newDokumentversjon.LinkDokument(newDokument))
                .Commit()
                .Saved;

            var dokumentversjon = savedObjects[newDokumentversjon.Id] as Dokumentversjon;
            Console.WriteLine(
                $"Created Dokumentversjon: Id={dokumentversjon.Id}, Versjonsnummer: {dokumentversjon.Versjonsnummer}, Filstoerrelse: {dokumentversjon.Filstoerrelse}");
        }

        private static void BusinessSpecificMetadataSample()
        {
            const string GROUP_ID = "APPR-03";

            //Get the business-specific metadata registry for a group with gruppeIdent "APPR-03"
            VirksomhetsspesifikkeMetadataInfo metadataInfo = client.BsmRegistry(GROUP_ID);

            //Print the registry for this group
            //Also find one string-type, long-type and double-type field
            string stringFieldId = null, doubleFieldId = null, longFieldId = null;

            foreach (VirksomhetsspesifikkeGruppeInfo groupInfo in metadataInfo.Gruppen)
            {
                Console.WriteLine(
                    $"GruppeInfo: GruppeIdent={groupInfo.GruppeIdent}, GruppeNavn={groupInfo.GruppeNavn}");
                foreach (VirksomhetsspesifikkeFeltInfo fieldInfo in groupInfo.Felter)
                {
                    Console.WriteLine(
                        $" ---- FeltInfo: FeltIdent={fieldInfo.FeltIdent}, FeltType={fieldInfo.FeltType}, FeltNavn={fieldInfo.FeltNavn}");

                    if (fieldInfo.FeltType == FieldType.String && stringFieldId == null)
                    {
                        stringFieldId = fieldInfo.FeltIdent;
                    }

                    if (fieldInfo.FeltType == FieldType.Double && doubleFieldId == null)
                    {
                        doubleFieldId = fieldInfo.FeltIdent;
                    }
                    if (fieldInfo.FeltType == FieldType.Long && longFieldId == null)
                    {
                        longFieldId = fieldInfo.FeltIdent;
                    }
                }
            }
            Console.WriteLine();

            //Create an Arkiv, Arkivdel and one Mappe
            //Set VirksomhetsspesifikkeMetadata for the Mappe
            var arkivskaper = new Arkivskaper("B67", "Jack Smith");
            var arkiv = new Arkiv("Arkiv - VirksomhetsspesifikkeMetadata Example");
            var arkivdel = new Arkivdel("Arkivdel - VirksomhetsspesifikkeMetadata Example");

            var mappe = new Mappe("Mappe with VirksomhetsspesifikkeMetadata");

            //Add three meta-data fields to the Mappe:
            if (stringFieldId != null)
            {
                mappe.VirksomhetsspesifikkeMetadata.AddBsmFieldValues(GROUP_ID, stringFieldId, "value 1",
                    "string value 2");
            }

            if (doubleFieldId != null)
            {
                mappe.VirksomhetsspesifikkeMetadata.AddBsmFieldValues(GROUP_ID, doubleFieldId, 5.63, 6.7);
            }

            if (longFieldId != null)
            {
                mappe.VirksomhetsspesifikkeMetadata.AddBsmFieldValues(GROUP_ID, longFieldId, 167907000L);
            }

            var transactionResponse = client.Transaction()
                .Save(arkiv)
                .Save(arkivskaper)
                .Link(arkiv.LinkArkivskaper(arkivskaper))
                .Save(arkivdel)
                .Link(arkivdel.LinkArkiv(arkiv))
                .Save(mappe)
                .Link(mappe.LinkArkivdel(arkivdel))
                .Commit();

            //Get the saved Mappe
            mappe = transactionResponse.Saved[mappe.Id] as Mappe;

            //Print the VirksomhetsspesifikkeMetadata of the Mappe
            BsmGroupsMap groupsMap = mappe.VirksomhetsspesifikkeMetadata;
            foreach (var groupId in groupsMap.Keys)
            {
                BsmFieldsMap fieldsMap = mappe.VirksomhetsspesifikkeMetadata[groupId];
                foreach (var fieldId in fieldsMap.Keys)
                {
                    BsmFieldValues values = fieldsMap[fieldId];
                    Console.WriteLine(
                        $"Gruppe={groupId}, Felt={fieldId}, ValueType={values.Type}, Values=[{string.Join(",", values.Values)}]");
                }
            }
            Console.WriteLine();

            //Update the VirksomhetsspesifikkeMetadata of the Mappe

            //Add one more string value to the string field
            if (stringFieldId != null)
            {
                //To add a new field value, simply add it to the set of values of the particular field
                //Use the "AddBsmFieldValues" method, if you want to override the existing set of values with a new one
                mappe.VirksomhetsspesifikkeMetadata[GROUP_ID][stringFieldId].Values.Add("string value 3");
            }

            //Remove one of the values of the double field
            if (doubleFieldId != null)
            {
                mappe.VirksomhetsspesifikkeMetadata.DeleteBsmFieldValue(GROUP_ID, doubleFieldId, 5.63);
            }

            //Completely remove the long field
            if (longFieldId != null)
            {
                mappe.VirksomhetsspesifikkeMetadata.DeleteBsmField(GROUP_ID, longFieldId);
            }

            //It is also possible to remove a whole group:
            //mappe.VirksomhetsspesifikkeMetadata.DeleteBsmGroup(groupIdentfier);

            transactionResponse = client.Transaction()
                .Save(mappe)
                .Commit();

            //Make query to fetch the Mappe

            QueryResponse<Mappe> queryResponse = client.Query<Mappe>("id=@id", 1)
                .AddQueryParam("@id", mappe.Id)
                .Execute();
            mappe = queryResponse.Results.First();

            //Print the new VirksomhetsspesifikkeMetadata
            groupsMap = mappe.VirksomhetsspesifikkeMetadata;
            foreach (var groupId in groupsMap.Keys)
            {
                BsmFieldsMap fieldsMap = mappe.VirksomhetsspesifikkeMetadata[groupId];
                foreach (var fieldId in fieldsMap.Keys)
                {
                    BsmFieldValues values = fieldsMap[fieldId];
                    Console.WriteLine(
                        $"Gruppe={groupId}, Felt={fieldId}, ValueType={values.Type}, Values=[{string.Join(",", values.Values)}]");
                }
            }

            Console.WriteLine();
        }
    }

    class Options
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