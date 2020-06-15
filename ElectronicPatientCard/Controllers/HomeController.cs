using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ElectronicPatientCard.Models;
using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Hl7.FhirPath.Sprache;
using System.Web;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc.Rendering;
using Hl7.Fhir.Support;

namespace ElectronicPatientCard.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        /**
         * Index Finished.
         */
        public ViewResult Index(string sortOrder, string searchString)
        {
            var patientList = new List<Patient>();

            var conn = new FhirClient("http://localhost:8080/baseR4");
            conn.PreferredFormat = ResourceFormat.Json;

            Bundle result = conn.Search<Patient>();
            while (result != null)
            {
                foreach (var i in result.Entry)
                {
                    if (i.Resource.TypeName == "Patient")
                    {
                        Patient patient = (Patient)i.Resource;
                        patientList.Add(patient);
                    }
                }
                result = conn.Continue(result, PageDirection.Next);
            }

            // Search case
            if (!String.IsNullOrEmpty(searchString))
            {
                patientList = patientList.Where(s => s.Name[0].Family.ToLower().Contains(searchString.ToLower()))
                    .ToList();
            }

            // Sort case
            ViewBag.SurnameSortParam = String.IsNullOrEmpty(sortOrder) ? "surname_desc" : "";
            ViewBag.FirstNameSortParam = sortOrder == "firstname" ? "firstname_desc" : "firstname";

            switch (sortOrder)
            {
                case "surname_desc":
                    patientList = patientList.OrderByDescending(s => s.Name[0].Family).ToList();
                    break;
                case "firstname":
                    patientList = patientList.OrderBy(s => s.Name[0].Given.ToList().ToString()).ToList();
                    break;
                case "firstname_desc":
                    patientList = patientList.OrderByDescending(s => s.Name[0].Given.ToList().ToString()).ToList();
                    break;
                default:
                    patientList = patientList.OrderBy(s => s.Name[0].Family).ToList();
                    break;
            }

            return View(patientList.ToList());
        }

        public ViewResult ShowDetails(string id)
        {
            var conn = new FhirClient("http://localhost:8080/baseR4");
            conn.PreferredFormat = ResourceFormat.Json;

            Patient patient = conn.Read<Patient>("Patient/" + id);

            UriBuilder uriBuilder = new UriBuilder("http://localhost:8080/baseR4");
            uriBuilder.Path = "Patient/" + patient.Id;
            Resource resultResource = conn.InstanceOperation(uriBuilder.Uri, "everything");

            ViewBag.Surname = patient.Name[0].Family;
            ViewBag.ID = patient.Id;
            ViewBag.Name = patient.Name[0].Given.FirstOrDefault();
            ViewBag.birthDate = new Date(patient.BirthDate.ToString());

            var listElement = new List<Details>();

            if (resultResource is Bundle)
            {
                Bundle resultBundle = resultResource as Bundle;
                while (resultBundle != null)
                {
                    foreach (var i in resultBundle.Entry)
                    {
                        Details element = new Details();
                        switch (i.Resource.TypeName)
                        {
                            case "Observation":
                                Observation observation = (Observation) i.Resource;

                                element.id = observation.Id;
                                element.resourceName = "Observation";
                                element.date = Convert.ToDateTime(observation.Effective.ToString());
                                element.reason = observation.Code.Text;
                                element.version = observation.Meta.VersionId;
                                


                                Quantity amount = observation.Value as Quantity;
                                if (amount != null)
                                {
                                    element.amount = amount.Value + " " + amount.Unit;
                                }

                                listElement.Add(element);
                                
                                break;

                            case "MedicationRequest":
                                MedicationRequest medicationRequest = (MedicationRequest) i.Resource;

                                element.id = medicationRequest.Id;
                                element.resourceName = "MedicationRequest";
                                element.date = Convert.ToDateTime(medicationRequest.AuthoredOn.ToString());
                                element.reason += ((CodeableConcept) medicationRequest.Medication).Text;
                                element.version = medicationRequest.Meta.VersionId;

                                listElement.Add(element);
                                break;
                        }
                    }
                    resultBundle = conn.Continue(resultBundle, PageDirection.Next);
                }
            }

            listElement = listElement.OrderByDescending(s => s.date).ToList();

            return View(listElement.ToList());

        }

        public ViewResult Chart(string id, string dateType = "all")
        {
            List<DataPoint> dataPoints = new List<DataPoint>();
            
            var conn = new FhirClient("http://localhost:8080/baseR4");
            conn.PreferredFormat = ResourceFormat.Json;

            Patient patient = conn.Read<Patient>("Patient/" + id);

            var x = conn.History("Patient/" + id);

            UriBuilder uriBuilder = new UriBuilder("http://localhost:8080/baseR4");
            uriBuilder.Path = "Patient/" + patient.Id;
            Resource resultResource = conn.InstanceOperation(uriBuilder.Uri, "everything");

            ViewBag.ID = patient.Id;
            ViewBag.Name = patient.Name[0].Given.FirstOrDefault() + " " + patient.Name[0].Family;

            if (resultResource is Bundle)
            {
                Bundle resultBundle = resultResource as Bundle;
                while (resultBundle != null)
                {
                    foreach (var i in resultBundle.Entry)
                    {
                        switch (i.Resource.TypeName)
                        {
                            case "Observation":
                                Observation observation = (Observation)i.Resource;

                                if (observation.Code.Text.Contains("Glucose"))
                                {
                                    var date = observation.Effective.ToString().Substring(0, 10);

                                    Quantity value = observation.Value as Quantity;
                                    var amount = double.Parse((value.Value).ToString());

                                    dataPoints.Add(new DataPoint(date, amount));
                                }
                                break;
                        }
                    }
                    resultBundle = conn.Continue(resultBundle, PageDirection.Next);
                }
            }
            ViewBag.MessagePost = dateType;

            switch (dateType)
            {
                case "5years":
                    dataPoints = dataPoints.Where(s => Convert.ToDateTime(s.label) >= DateTime.Now.Date.AddYears(-5)).ToList();
                    break;
                case "1year":
                    dataPoints = dataPoints.Where(s => Convert.ToDateTime(s.label) >= DateTime.Now.Date.AddYears(-1)).ToList();
                    break;
                case "6months":
                    dataPoints = dataPoints.Where(s => Convert.ToDateTime(s.label) >= DateTime.Now.Date.AddMonths(-6)).ToList();
                    break;
                case "3month":
                    dataPoints = dataPoints.Where(s => Convert.ToDateTime(s.label) >= DateTime.Now.Date.AddMonths(-3)).ToList();
                    break;
                case "1month":
                    dataPoints = dataPoints.Where(s => Convert.ToDateTime(s.label) >= DateTime.Now.Date.AddMonths(-1)).ToList();
                    break;
                default:
                    break;
            }

            ViewBag.DataPoints = JsonConvert.SerializeObject(dataPoints);
            
            return View();
        }


        public ViewResult EditPatient(string id)
        {
            var conn = new FhirClient("http://localhost:8080/baseR4");
            conn.PreferredFormat = ResourceFormat.Json;

            Patient patient = conn.Read<Patient>("Patient/" + id);

            PatientEdit newPatient = new PatientEdit();
            newPatient.surname = patient.Name[0].Family;
            newPatient.birthDate = patient.BirthDate;
            newPatient.mStatus = patient.MaritalStatus.Text;
            newPatient.id = patient.Id;

            return View(newPatient);
        }

        public ActionResult SavePatient(PatientEdit patient)
        {
           
            var conn = new FhirClient("http://localhost:8080/baseR4");
            conn.PreferredFormat = ResourceFormat.Json;

            Patient patientNew = conn.Read<Patient>("Patient/" + patient.id);
            patientNew.Name[0].Family = patient.surname;
            patientNew.BirthDate = patient.birthDate;
            patientNew.MaritalStatus.Text = patient.mStatus;
            conn.Update(patientNew);
            return View();
        }
        public ViewResult Edit(string id, string resourceName, string patientId)
        {
            
            var conn = new FhirClient("http://localhost:8080/baseR4");
            conn.PreferredFormat = ResourceFormat.Json;

            Patient patient = conn.Read<Patient>("Patient/" + patientId);

            UriBuilder uriBuilder = new UriBuilder("http://localhost:8080/baseR4");
            uriBuilder.Path = "Patient/" + patient.Id;
            Resource resultResource = conn.InstanceOperation(uriBuilder.Uri, "everything");

            ViewBag.Surname = patient.Name[0].Family;
            ViewBag.ID = patient.Id;
            ViewBag.Name = patient.Name[0].Given.FirstOrDefault();
            ViewBag.birthDate = new Date(patient.BirthDate.ToString());

            var selectedElement = new DetailsPatient();

            if (resultResource is Bundle)
            {
                Bundle resultBundle = resultResource as Bundle;
                while (resultBundle != null)
                {
                    foreach (var i in resultBundle.Entry)
                    {
                        DetailsPatient element = new DetailsPatient();
                        switch (i.Resource.TypeName)
                        {
                            case "Observation":
                                Observation observation = (Observation)i.Resource;
                                if (observation.Id == id)
                                {
                                    element.id = observation.Id;
                                    element.resourceName = "Observation";
                                    element.date = observation.Effective;
                                    element.reason = observation.Code.Text;
                                    element.amount = observation.Meta.VersionId;

                                    
                                    element.patientId = patient.Id;
                              
                                    selectedElement = element;
                                }                             
                                break;

                            case "MedicationRequest":
                                MedicationRequest medicationRequest = (MedicationRequest)i.Resource;
                                if (medicationRequest.Id == id)
                                {
                                    element.id = medicationRequest.Id;
                                    element.resourceName = "MedicationRequest";
                                   
                                    element.reason += ((CodeableConcept)medicationRequest.Medication).Text;
                                    element.patientId = patientId;
                                    element.amount = medicationRequest.Meta.VersionId;
                                    
                                    selectedElement = element;
                                }                               
                                break;
                        }
                    }
                    resultBundle = conn.Continue(resultBundle, PageDirection.Next);
                }
            }
           

            return View(selectedElement);
        }
        [HttpPost]
        [Obsolete]
        public ActionResult Save(DetailsPatient item)
        {
            DetailsPatient element = new DetailsPatient();
            element.date = item.date;
            element.reason = item.reason;
            element.amount = item.amount;
            element.resourceName = item.resourceName;
            element.id = item.id;
            element.patientId = item.patientId;


            var conn = new FhirClient("http://localhost:8080/baseR4");
            conn.PreferredFormat = ResourceFormat.Json;

            Patient patient = conn.Read<Patient>("Patient/" + element.patientId);

            UriBuilder uriBuilder = new UriBuilder("http://localhost:8080/baseR4");
            uriBuilder.Path = "Patient/" + patient.Id;
            Resource resultResource = conn.InstanceOperation(uriBuilder.Uri, "everything");

            ViewBag.Surname = patient.Name[0].Family;
            ViewBag.ID = patient.Id;
            ViewBag.Name = patient.Name[0].Given.FirstOrDefault();
            ViewBag.birthDate = new Date(patient.BirthDate.ToString());


            if (resultResource is Bundle)
            {
                Bundle resultBundle = resultResource as Bundle;
                while (resultBundle != null)
                {
                    foreach (var i in resultBundle.Entry)
                    {
                        DetailsPatient fetchedElement = new DetailsPatient();
                        switch (i.Resource.TypeName)
                        {
                            case "Observation":
                                Observation observation = (Observation)i.Resource;
                                
                                if (observation.Id == item.id)
                                {

                                    fetchedElement.date = observation.Effective;
                                    fetchedElement.reason = observation.Code.Text;
                                    fetchedElement.amount = observation.Meta.VersionId;


                                    UriBuilder uriBuilderBack = new UriBuilder("http://localhost:8080/baseR4");
                                    uriBuilderBack.Path = "Observation/" + item.id;
                                    Observation  resultResourceBack = conn.Read<Observation>(uriBuilderBack.Uri);

                            
                                    resultResourceBack.Code.Text = item.reason;
                                    conn.Update(resultResourceBack);
                
                                   


                                }
                                break;

                            case "MedicationRequest":
                                MedicationRequest medicationRequest = (MedicationRequest)i.Resource;
                                if (medicationRequest.Id == item.id)
                                {
                                    fetchedElement.amount = medicationRequest.Meta.VersionId;

                                    //fetchedElement.date = medicationRequest.AuthoredOn
                                    //fetchedElement.reason += ((CodeableConcept)medicationRequest.Medication).Text;

                                }
                                break;
                        }
                    }
                    resultBundle = conn.Continue(resultBundle, PageDirection.Next);
                }
            }



            return View(element);
        }
        public ActionResult Save()
        {
            return View(new Details());
        }
        
        public ActionResult ShowPatientVersion(string id)
        {
            var conn = new FhirClient("http://localhost:8080/baseR4");
            conn.PreferredFormat = ResourceFormat.Json;

            Patient patient = conn.Read<Patient>("Patient/" + id);

            var patientList = new List<PatientEdit>();

            PatientEdit newPatient = new PatientEdit();
            newPatient.surname = patient.Name[0].Family;
            newPatient.birthDate = patient.BirthDate;
            newPatient.mStatus = patient.MaritalStatus.Text;
            newPatient.id = patient.Id;
            newPatient.version = patient.Meta.VersionId;
            int versions = int.Parse(newPatient.version);

            for (int i = 1; i <= versions; i++)
            {
                PatientEdit vPatient = new PatientEdit();
                UriBuilder uriBuilder = new UriBuilder("http://localhost:8080/baseR4");
                uriBuilder.Path = "Patient/" + id + "/_history/"+i;
                Patient resultResource = conn.Read<Patient>(uriBuilder.Uri);
                vPatient.surname = resultResource.Name[0].Family;
                vPatient.birthDate = resultResource.BirthDate;
                vPatient.mStatus = resultResource.MaritalStatus.Text;
                vPatient.version = resultResource.Meta.VersionId;
                vPatient.modDate =  Convert.ToDateTime(resultResource.Meta.LastUpdated.ToString());
                patientList.Add(vPatient);
            }
            
            return View(patientList);
        }

        public ActionResult ShowResourceVersion(string rId, string id)
        {
            var conn = new FhirClient("http://localhost:8080/baseR4");
            conn.PreferredFormat = ResourceFormat.Json;

            Patient patient = conn.Read<Patient>("Patient/" + id);

            UriBuilder uriBuilder = new UriBuilder("http://localhost:8080/baseR4");
            uriBuilder.Path = "Patient/" + patient.Id;
            Resource resultResource = conn.InstanceOperation(uriBuilder.Uri, "everything");
            var resourceList = new List<Details>();

            if (resultResource is Bundle)
            {
                Bundle resultBundle = resultResource as Bundle;
                while (resultBundle != null)
                {
                    foreach (var i in resultBundle.Entry)
                    {
                        Details element = new Details();
                        switch (i.Resource.TypeName)
                        {
                            case "Observation":
                                Observation observation = (Observation)i.Resource;
                                if (observation.Id == rId)
                                {
                                    element.id = observation.Id;
                                    element.resourceName = "Observation";
                                    element.reason = observation.Code.Text;
                                    element.version = observation.Meta.VersionId;
                                    int versions = int.Parse(element.version);
                                    for (int j = 1; j <= versions; j++)
                                    {
                                        Details vPatient = new Details();
                                        UriBuilder uriBuilder1 = new UriBuilder("http://localhost:8080/baseR4");
                                        uriBuilder1.Path = "Observation/" + element.id + "/_history/" + j;
                                        Observation resultResource1 = conn.Read<Observation>(uriBuilder1.Uri);
                                        vPatient.id = resultResource1.Id;
                                        vPatient.reason = resultResource1.Code.Text;
                                        vPatient.date = Convert.ToDateTime(resultResource1.Effective.ToString());
                                        vPatient.version = resultResource1.Meta.VersionId;
                                        vPatient.modDate = Convert.ToDateTime(resultResource1.Meta.LastUpdated.ToString());
                                        resourceList.Add(vPatient);
                                    }

                                }
                                break;

                        }
                    }
                    resultBundle = conn.Continue(resultBundle, PageDirection.Next);
                }
            }


            return View(resourceList);
        
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
