using Documaster.WebApi.Client.Noark5;
using Documaster.WebApi.Client.Noark5.Client;
using Documaster.WebApi.Client.Noark5.NoarkEntities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NoarkWsClientSample
{
    public class Samples
    {
        private readonly DocumasterClients documasterClients;
        private readonly string testDoc;

        public Samples(Options options)
        {
            this.documasterClients = new DocumasterClients(options);
            this.testDoc = options.TestDoc;
        }

        public void JournalingSample()
        {
            Console.WriteLine($"Journaling example {Environment.NewLine}");
            NoarkClient client = documasterClients.getNoarkClient();

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

            //Create a screening code
            Skjerming newSkjerming = new Skjerming(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "Description",
                "Authority");
            Skjerming skjerming = client.PutCodeListValue(newSkjerming);

            //Screen the Arkivdel
            arkivdel.Skjerming = skjerming;
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

            //Create a new administrativEnhet value
            AdministrativEnhet newAdministrativEnhet =
                new AdministrativEnhet(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
            AdministrativEnhet administrativEnhet = client.PutCodeListValue(newAdministrativEnhet);

            //Create a new Saksmappe in the Arkivdel
            //The new Saksmappe needs to have a Klasse in the primary Klassifikasjonssystem of the Arkivdel
            //Also link the Saksmappe to a secondary Klasse
            var newSaksmappe = new Saksmappe("Tilbud (Smith, John)", administrativEnhet);
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

            //Create a new value for Dokumenttype
            Dokumenttype newDokumenttype = new Dokumenttype(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
            Dokumenttype dokumenttype = client.PutCodeListValue(newDokumenttype);

            //Create a new Dokument and Dokumentversjon using the uploaded file
            var newDokument = new Dokument(dokumenttype, "Tilbud (Smith, John, Godkjent)",
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

        public void ArchiveSample()
        {
            Console.WriteLine($"Archive example {Environment.NewLine}");
            NoarkClient client = documasterClients.getNoarkClient();

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

        public void MeetingAndBoardHandlingDataSample()
        {
            Console.WriteLine($"Meeting and board handling data example {Environment.NewLine}");
            NoarkClient client = documasterClients.getNoarkClient();

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

            //Create a new Moetemappe and Moetedeltaker
            Moetemappe newMappe = new Moetemappe("Moetemappe Tittel", "Moetenummer", "Utvalg");
            Moetedeltaker moetedeltaker = new Moetedeltaker("Moetedeltaker Navn");

            transactionResponse = client.Transaction()
                .Save(newMappe)
                .Link(newMappe.LinkArkivdel(arkivdel))
                .Save(moetedeltaker)
                .Link(moetedeltaker.LinkMappe(newMappe))
                .Commit();

            var mappe = transactionResponse.Saved[newMappe.Id] as Moetemappe;
            Console.WriteLine($"Created Mappe: Id={mappe.Id}, Tittel={mappe.Tittel}");
            Console.WriteLine($"Created Moetedeltaker: Navn={moetedeltaker.Navn}");

            //Create a new AdministrativEnhett code list value
            AdministrativEnhet newAdministrativEnhet =
                new AdministrativEnhet(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
            AdministrativEnhet administrativEnhet = client.PutCodeListValue(newAdministrativEnhet);

            //Create a new Moeteregistrering
            Moeteregistrering newMoeteregistrering = new Moeteregistrering("Tittel", "Saksbehandler",
                administrativEnhet, Moeteregistreringstype.MOETEINNKALLING);
            transactionResponse = client.Transaction()
                .Save(newMoeteregistrering)
                .Link(newMoeteregistrering.LinkMappe(mappe))
                .Commit();

            var moeteregistrering = transactionResponse.Saved[newMoeteregistrering.Id] as Moeteregistrering;
            Console.WriteLine(
                $"Created Moeteregistrering: Id={moeteregistrering.Id}, Tittel={moeteregistrering.Tittel}");
            ;

            //Upload a file
            Dokumentfil dokumentfil;
            using (var inputStream = File.OpenRead(testDoc))
            {
                dokumentfil = client.Upload(inputStream, "godkjenning.pdf");
            }
            Console.WriteLine($"Uploaded file {testDoc}");

            //Create a new Dokumenttype code list value
            Dokumenttype newDokumenttype = new Dokumenttype(Guid.NewGuid().ToString(), Guid.NewGuid().ToString());
            Dokumenttype dokumenttype = client.PutCodeListValue(newDokumenttype);

            //Link the Dokument to the Moeteregistrering
            var newDokument = new Dokument(dokumenttype, "Tilbud (Smith, John, Godkjent)",
                TilknyttetRegistreringSom.HOVEDDOKUMENT);
            var newDokumentversjon = new Dokumentversjon(Variantformat.PRODUKSJONSFORMAT, ".pdf", dokumentfil);

            transactionResponse = client.Transaction()
                .Save(newDokument)
                .Link(newDokument.LinkRegistrering(moeteregistrering))
                .Save(newDokumentversjon)
                .Link(newDokumentversjon.LinkDokument(newDokument))
                .Commit();

            var dokumentversjon = transactionResponse.Saved[newDokumentversjon.Id] as Dokumentversjon;
            Console.WriteLine(
                $"Created Dokumentversjon: Id={dokumentversjon.Id}, Versjonsnummer: {dokumentversjon.Versjonsnummer}, Filstoerrelse: {dokumentversjon.Filstoerrelse}");
            Console.WriteLine();
        }

        public void BusinessSpecificMetadataSample()
        {
            NoarkClient client = documasterClients.getNoarkClient();

            string GROUP_ID = $"gr-{Guid.NewGuid().ToString()}";
            string STRING_FIELD_ID = $"f-{Guid.NewGuid().ToString()}";
            string DOUBLE_FIELD_ID = $"f-{Guid.NewGuid().ToString()}";
            string LONG_FIELD_ID = $"f-{Guid.NewGuid().ToString()}";

            //Create a business-specific metadata group
            MetadataGroupInfo newGroup = new MetadataGroupInfo(GROUP_ID, "BSM Group Name", "BSM Group Description");
            MetadataGroupInfo savedGroup = client.PutBsmGroup(newGroup);
            Console.WriteLine(
                $"Created new group: GroupId={savedGroup.GroupId}, GroupDescription={savedGroup.GroupDescription}, GroupName={savedGroup.GroupName}");
            Console.WriteLine();

            //Create a new string field with predefined values "value 1", "value 2" and "value 3"
            MetadataFieldInfo newFieldStr = new MetadataFieldInfo(STRING_FIELD_ID, "BSM Field String",
                "BSM Field Description", FieldType.String, new List<object>() { "value 1", "value 2", "value 3" });
            MetadataFieldInfo savedFieldStr = client.PutBsmField(GROUP_ID, newFieldStr);
            Console.WriteLine(
                $"Created new field: FieldId={savedFieldStr.FieldId}, FieldType={savedFieldStr.FieldType}, FieldName={savedFieldStr.FieldName}, FieldValues={string.Join(",", savedFieldStr.FieldValues)}");
            Console.WriteLine();

            //Create a new long field with predefined values 1 and 2
            MetadataFieldInfo newFieldLong = new MetadataFieldInfo(LONG_FIELD_ID, "BSM Field Long",
                "BSM Field Description", FieldType.Long, new List<object>() { 1L, 2L });
            MetadataFieldInfo savedFieldLong = client.PutBsmField(GROUP_ID, newFieldLong);
            Console.WriteLine(
                $"Created new field: FieldId={savedFieldLong.FieldId}, FieldType={savedFieldLong.FieldType}, FieldName={savedFieldLong.FieldName}, FieldValues={string.Join(",", savedFieldLong.FieldValues)}");

            //Create a new double field with no predefined values
            MetadataFieldInfo newFieldDouble = new MetadataFieldInfo(DOUBLE_FIELD_ID, "BSM Field Double",
                "BSM Field Description", FieldType.Double);
            MetadataFieldInfo savedFielDouble = client.PutBsmField(GROUP_ID, newFieldDouble);
            Console.WriteLine(
                $"Created new field: FieldId={newFieldDouble.FieldId}, FieldType={newFieldDouble.FieldType}, FieldName={newFieldDouble.FieldName}");
            Console.WriteLine();

            //Update string field - add new field value, remove an old one
            savedFieldStr.FieldValues.Add("value 4");
            savedFieldStr.FieldValues.Remove("value 3");
            MetadataFieldInfo updatedField = client.PutBsmField(GROUP_ID, savedFieldStr);
            Console.WriteLine(
                $"Updated field: FieldId={updatedField.FieldId}, FieldType={updatedField.FieldType}, FieldName={updatedField.FieldName}, FieldValues={string.Join(",", updatedField.FieldValues)}");
            Console.WriteLine();

            //Get the business-specific metadata registry for a specific group
            BusinessSpecificMetadataInfo metadataInfo = client.BsmRegistry(GROUP_ID);

            Console.WriteLine("BusinessSpecificMetadataInfo:");
            //Print the registry for this group
            foreach (MetadataGroupInfo groupInfo in metadataInfo.Groups)
            {
                Console.WriteLine(
                    $"GroupInfo: GroupId={groupInfo.GroupId}, GroupName={groupInfo.GroupName}");
                foreach (MetadataFieldInfo fieldInfo in groupInfo.Fields)
                {
                    Console.WriteLine(
                        $" ---- FieldInfo: FieldId={fieldInfo.FieldId}, FieldType={fieldInfo.FieldType}, FieldName={fieldInfo.FieldName}");
                }
            }
            Console.WriteLine("--------------------------------------------------------------------------");
            Console.WriteLine();

            //Create an Arkiv, Arkivdel and one Mappe
            //Set VirksomhetsspesifikkeMetadata for the Mappe
            var arkivskaper = new Arkivskaper("B67", "Jack Smith");
            var arkiv = new Arkiv("Arkiv - VirksomhetsspesifikkeMetadata Example");
            var arkivdel = new Arkivdel("Arkivdel - VirksomhetsspesifikkeMetadata Example");

            var mappe = new Mappe("Mappe with VirksomhetsspesifikkeMetadata");

            //Add three meta-data fields to the Mappe:
            mappe.VirksomhetsspesifikkeMetadata.AddBsmFieldValues(GROUP_ID, STRING_FIELD_ID, "value 1",
                "value 2");
            mappe.VirksomhetsspesifikkeMetadata.AddBsmFieldValues(GROUP_ID, DOUBLE_FIELD_ID, 1.2);
            mappe.VirksomhetsspesifikkeMetadata.AddBsmFieldValues(GROUP_ID, LONG_FIELD_ID, 2L);

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
            Console.WriteLine("Added VirksomhetsspesifikkeMetadata to folder:");
            BsmGroupsMap groupsMap = mappe.VirksomhetsspesifikkeMetadata;
            foreach (var groupId in groupsMap.Keys)
            {
                BsmFieldsMap fieldsMap = mappe.VirksomhetsspesifikkeMetadata[groupId];
                foreach (var fieldId in fieldsMap.Keys)
                {
                    BsmFieldValues values = fieldsMap[fieldId];
                    Console.WriteLine(
                        $"GroupId={groupId}, FieldId={fieldId}, ValueType={values.Type}, Values=[{string.Join(",", values.Values)}]");
                }
            }
            Console.WriteLine();

            //Update the VirksomhetsspesifikkeMetadata of the Mappe

            //Add one more string value to the string field

            //To add a new field value, simply add it to the set of values of the particular field
            //Use the "AddBsmFieldValues" method, if you want to override the existing set of values with a new one
            mappe.VirksomhetsspesifikkeMetadata[GROUP_ID][STRING_FIELD_ID].Values.Add("value 4");

            //Remove one of the values of the double field
            mappe.VirksomhetsspesifikkeMetadata.DeleteBsmFieldValue(GROUP_ID, DOUBLE_FIELD_ID, 2.6);

            //Completely remove the long field
            mappe.VirksomhetsspesifikkeMetadata.DeleteBsmField(GROUP_ID, LONG_FIELD_ID);

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
            Console.WriteLine("Updated VirksomhetsspesifikkeMetadata of folder:");
            groupsMap = mappe.VirksomhetsspesifikkeMetadata;
            foreach (var groupId in groupsMap.Keys)
            {
                BsmFieldsMap fieldsMap = mappe.VirksomhetsspesifikkeMetadata[groupId];
                foreach (var fieldId in fieldsMap.Keys)
                {
                    BsmFieldValues values = fieldsMap[fieldId];
                    Console.WriteLine(
                        $"GroupId={groupId}, FieldId={fieldId}, ValueType={values.Type}, Values=[{string.Join(",", values.Values)}]");
                }
            }

            Console.WriteLine();


            //Delete field
            client.DeleteBsmField(GROUP_ID, LONG_FIELD_ID);
            Console.WriteLine($"Deleted field with  FieldId={LONG_FIELD_ID}");
            Console.WriteLine();

            //Delete folder
            client.Transaction().Delete(mappe).Commit();
            Console.WriteLine($"Deleted folder");
            Console.WriteLine();

            //Delete group
            client.DeleteBsmGroup(GROUP_ID);
            Console.WriteLine($"Deleted group with GroupId={GROUP_ID}");
            Console.WriteLine();
        }

        public void CodeListsSample()
        {
            NoarkClient client = documasterClients.getNoarkClient();

            List<CodeList> allCodeLists = client.CodeLists();

            Console.WriteLine($"Code lists:{Environment.NewLine}");
            foreach (CodeList list in allCodeLists)
            {
                Console.WriteLine($"Code list: {list.Type}.{list.Field}");
                foreach (CodeValue codeValue in list.Values)
                {
                    Console.WriteLine(
                        $" --- Code value: Code={codeValue.Code}, Name={codeValue.Name}, Description={codeValue.Description}");
                }
                Console.WriteLine();
            }
            Console.WriteLine();

            //Create new list value for the code list Dokumenttype
            Dokumenttype dokumenttype = new Dokumenttype(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(),
                "Description");
            client.PutCodeListValue(dokumenttype);
            Console.WriteLine(
                $"Created new code value: Code={dokumenttype.Code}, Name={dokumenttype.Name}, Description={dokumenttype.Description}");
            Console.WriteLine();

            //Update list value
            Dokumenttype updatedValue = new Dokumenttype(dokumenttype.Code, dokumenttype.Name, "New Description");
            client.PutCodeListValue(updatedValue);
            Console.WriteLine(
                $"Updated code value: Code={updatedValue.Code}, Name={updatedValue.Name}, Description={updatedValue.Description}");
            Console.WriteLine();

            //Delete list value
            client.DeleteCodeListValue(updatedValue);
            Console.WriteLine($"Deleted code value");
            Console.WriteLine();
        }
    }
}
