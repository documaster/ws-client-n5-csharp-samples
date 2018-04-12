using System;
using System.IO;
using System.Linq;
using CommandLine;
using NoarkWsClient;
using NoarkWsClient.Client;
using NoarkWsClient.NoarkEntities;

namespace NoarkWsClientSample
{
    class Program
    {
        private static NoarkClient client;
        static string testDoc;

        public static void Main(string[] args)
        {
            Parser.Default
                .ParseArguments<Options>(args)
                .WithParsed<Options>(
                    opts =>
                    {
                        InitClient(opts);
                        testDoc = opts.TestDoc;
                    });

            JournalingSample();
            ArchiveSample();
        }

        private static void InitClient(Options options)
        {
            client = new NoarkClient(options.ServerAddress);

            //System specific authentication using certificates
            //client.SetClientCert(options.CertificatePath, options.CertificatePass);

            client.TrustSelfSignedCerts = true;

            //System specific authorization using access token
            client.AuthToken = options.AccessToken;
        }

        private static void JournalingSample()
        {
            Console.WriteLine($"Journaling example {Environment.NewLine}");

            //Create a new Arkiv with an Arkivskaper
            var newArkivskaper = new Arkivskaper("B7-23-W5", "John Smith");
            var newArkiv = new Arkiv("Arkiv");

            var transactionResponse = client.Transaction()
                .Save(newArkiv)
                .Save(newArkivskaper)
                .Link(newArkiv.LinkArkivskaper(newArkivskaper))
                .Commit();

            var arkiv = transactionResponse.Saved[newArkiv.Id] as Arkiv;
            Console.WriteLine($"Created Arkiv: Id={arkiv.Id}, Tittel={arkiv.Tittel}, OpprettetDato={arkiv.OpprettetDato}");

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

            //Find the Arkivdel by id
            var queryResults = client.Query<Arkivdel>("id=@arkivdelId", 10)
                .AddQueryParam("@arkivdelId", arkivdel.Id)
                .Execute();
            Console.WriteLine($"Found {queryResults.Results.Count()} Arkivdel object(s) with Id {arkivdel.Id}");

            //Create a new Saksmappe in the Arkivdel
            //The new Saksmappe needs to have a Klasse in the Klassifikasjonssystem of the Arkivdel

            var newSaksmappe = new Saksmappe("Tilbud (Smith, John)", "kommune");
            var newSakspart = new Sakspart("Alice", "internal");

            var savedObjects = client.Transaction()
                .Save(newSaksmappe)
                .Link(newSaksmappe.LinkArkivdel(arkivdel))
                .Link(newSaksmappe.LinkKlasse(klasseId))
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
                .Unlink(saksmappe.UnlinkKlasse(klasseId))
                .Link(saksmappe.LinkKlasse(anotherKlasse))
                .Commit();
            Console.WriteLine($"Unlinked Saksmappe wiht Id {saksmappe.Id} from Klasse '{newKlasse.Tittel}' and linked it to Klasse '{anotherKlasse.Tittel}'");

            //Find all available codes for journalstatus
            var journalstatusCodeList = client.CodeLists("Journalpost", "journalstatus").First();
            Console.WriteLine($"CodeList list for {journalstatusCodeList.Type}.{journalstatusCodeList.Field}:");
            foreach (var code in journalstatusCodeList.Values)
            {
                Console.WriteLine($"    Code={code.Code}, Name={code.Name}");
            }

            //Create a new Journalpost in the Saksmappe
            //Create an EksternId object and link it to the Journalpost
            //Create a new Korrespondansepart and link it to the Journalpost

            var newJournalpost = new Journalpost("Tilbud (Smith, John, Godkjent)", Journalposttype.UTGAAENDE_DOKUMENT)
            {
                Journalaar = 2007,
                Journalsekvensnummer = 46
            };

            var newEksternId = new EksternId("External System", Guid.NewGuid().ToString());
            var newKorrespondansepart = new Korrespondansepart(Korrespondanseparttype.INTERN_MOTTAKER, "John Smith");

            savedObjects = client.Transaction()
                .Save(newJournalpost)
                .Link(newJournalpost.LinkMappe(saksmappe))
                .Save(newEksternId)
                .Link(newJournalpost.LinkEksternId(newEksternId))
                .Save(newKorrespondansepart)
                .Link(newJournalpost.LinkKorrespondansepart(newKorrespondansepart))
                .Commit()
                .Saved;

            var journalPost = savedObjects[newJournalpost.Id] as Journalpost;
            Console.WriteLine($"Created Journalpost: Id={journalPost.Id}, Tittel={journalPost.Tittel}, Journalstatus={journalPost.Journalstatus.Code}");

            //Find the Journalpost by the eksternID value
            var journalpstQueryResults = client.Query<Journalpost>("refEksternId.eksternID=@eksternId", 1)
                .AddQueryParam("@eksternId", newEksternId.EksternID)
                .Execute();
            Console.WriteLine($"Found {journalpstQueryResults.Total} Journalpost objects with eksternID {newEksternId.EksternID}");

            //Upload a file
            Dokumentfil dokumentfil;
            using (var inputStream = File.OpenRead(testDoc))
            {
                dokumentfil = client.Upload(inputStream, "godkjenning.pdf");
            }
            Console.WriteLine($"Uploaded file {testDoc}");

            //Create a new Dokument and Dokumentversjon using the uploaded file
            var newDokument = new Dokument("Godkjenning", "Tilbud (Smith, John, Godkjent)", TilknyttetRegistreringSom.HOVEDDOKUMENT);
            var newDokumentversjon = new Dokumentversjon(Variantformat.PRODUKSJONSFORMAT, ".pdf", dokumentfil);

            savedObjects = client.Transaction()
                .Save(newDokument)
                .Link(newDokument.LinkRegistrering(journalPost))
                .Save(newDokumentversjon)
                .Link(newDokumentversjon.LinkDokument(newDokument))
                .Commit()
                .Saved;

            var dokumentversjon = savedObjects[newDokumentversjon.Id] as Dokumentversjon;
            Console.WriteLine($"Created Dokumentversjon: Id={dokumentversjon.Id}, Versjonsnummer: {dokumentversjon.Versjonsnummer}, Filstoerrelse: {dokumentversjon.Filstoerrelse}");

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
            Console.WriteLine($"Query returned {queryResponse.Results.Count()} Dokument objects out of {queryResponse.Total} in Saksmappe objects called 'Tilbud (Smith, John)'");

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
            Console.WriteLine($"Created Arkiv: Id={arkiv.Id}, Arkivstatus={arkiv.Arkivstatus.Code}, OpprettetDato={arkiv.OpprettetDato}");

            var arkivdel = transactionResponse.Saved[newArkivdel.Id] as Arkivdel;
            Console.WriteLine($"Created Arkivdel: Id={arkivdel.Id}, Arkivdelstatus={arkivdel.Arkivdelstatus.Code}");

            //Create a new Mappe
            var newMappe = new Mappe("Barnehage Tilbud")
            {
                Beskrivelse = "Mappe Beskrivelse",
                Mappetype = "Barnehage"
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
            Console.WriteLine($"Created a new Mappe (Id={barnMappe.Id}, Tittel={barnMappe.Tittel}) in Mappe with Id {mappe.Id}");

            //Find all children of the Mappe
            var queryResults = client.Query<Mappe>("refForelderMappe.id=@forelderMappeId", 10)
                .AddQueryParam("@forelderMappeId", mappe.Id)
                .Execute();
            Console.WriteLine($"Found {queryResults.Total} Mappe objects in Mappe with Id {mappe.Id}");

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
            Console.WriteLine($"Created Basisregistrering: Id={basisregistrering.Id}, Tittel={basisregistrering.Tittel}");

            //Upload a file
            Dokumentfil dokumentfil;
            using (var inputStream = File.OpenRead(testDoc))
            {
                dokumentfil = client.Upload(inputStream, "godkjenning.pdf");
            }
            Console.WriteLine($"Uploaded file {testDoc}");

            //Create a new Dokument and Dokumentversjon using the uploaded file
            //Link the Dokument to the Basisregistrering
            var newDokument = new Dokument("Godkjenning", "Tilbud (Smith, John, Godkjent)", TilknyttetRegistreringSom.HOVEDDOKUMENT);
            var newDokumentversjon = new Dokumentversjon(Variantformat.PRODUKSJONSFORMAT, ".pdf", dokumentfil);

            savedObjects = client.Transaction()
                .Save(newDokument)
                .Link(newDokument.LinkRegistrering(basisregistrering))
                .Save(newDokumentversjon)
                .Link(newDokumentversjon.LinkDokument(newDokument))
                .Commit()
                .Saved;

            var dokumentversjon = savedObjects[newDokumentversjon.Id] as Dokumentversjon;
            Console.WriteLine($"Created Dokumentversjon: Id={dokumentversjon.Id}, Versjonsnummer: {dokumentversjon.Versjonsnummer}, Filstoerrelse: {dokumentversjon.Filstoerrelse}");
        }
    }

    class Options
    {
        [Option("addr", Required = true, HelpText = "Server address")]
        public string ServerAddress { get; set; }

        [Option("cert", HelpText = "Path to certificate file")]
        public string CertificatePath { get; set; }

        [Option("certpass", HelpText = "Certificate password")]
        public string CertificatePass { get; set; }

        [Option("token", Required = true, HelpText = "Authorization token")]
        public string AccessToken { get; set; }

        [Option("testdoc", Required = true, HelpText = "Path to test file")]
        public string TestDoc { get; set; }
    }
}
