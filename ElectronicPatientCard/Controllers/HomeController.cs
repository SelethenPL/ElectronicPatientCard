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

            var listElement = new List<Details>();
            var selectedElement = new Details();

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
                                if (observation.Id == id)
                                {
                                    element.id = observation.Id;
                                    element.resourceName = "Observation";
                                    element.date = Convert.ToDateTime(observation.Effective.ToString());
                                    element.reason = observation.Code.Text;

                                    Quantity amount = observation.Value as Quantity;
                                    if (amount != null)
                                    {
                                        element.amount = amount.Value + " " + amount.Unit;
                                    }

                                    listElement.Add(element);
                                    selectedElement = element;
                                }                             
                                break;

                            case "MedicationRequest":
                                MedicationRequest medicationRequest = (MedicationRequest)i.Resource;
                                if (medicationRequest.Id == id)
                                {
                                    element.id = medicationRequest.Id;
                                    element.resourceName = "MedicationRequest";
                                    element.date = Convert.ToDateTime(medicationRequest.AuthoredOn.ToString());
                                    element.reason += ((CodeableConcept)medicationRequest.Medication).Text;

                                    listElement.Add(element);
                                    selectedElement = element;
                                }                               
                                break;
                        }
                    }
                    resultBundle = conn.Continue(resultBundle, PageDirection.Next);
                }
            }
           
            //  listElement = listElement.OrderByDescending(s => s.date).ToList();

            return View(selectedElement);
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
