using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Microsoft.Extensions.Options;
using WebApplication.Models;

namespace WebApplication.Controllers
{
    [Route("files")]
    [ApiController]
    public class FilesController : ControllerBase
    {
        private AzureSettings _azureSettings;
        public FilesController(IOptions<AzureSettings> azureSettings)
        {
            _azureSettings = azureSettings.Value;
        }

        [HttpPost("upload")]
        public IActionResult GetImage(IFormFile file)
        {
            var filePath = Path.GetTempFileName();
            if (file.Length > 0)
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                { 
                    file.CopyTo(stream);
                }
            }

            var client = Authenticate(_azureSettings.Endpoint, _azureSettings.SubscriptionKey);
            String readText = String.Empty;
            try
            {
                readText = BatchReadFileLocal(client, filePath);
            }
            catch (Exception e)
            {
                return BadRequest("При попытке прочитать сообщение возникла ошибка");
            }
            if (readText == String.Empty)
                return BadRequest("Не удалось прочиать текст на картинке");
            return Ok(readText);
        }

        private String BatchReadFileLocal(ComputerVisionClient client, String localImage)
        {
            StringBuilder stringBuilder = new StringBuilder();
            const Int32 numberOfCharsInOperationId = 36;
            using (Stream imageStream = System.IO.File.OpenRead(localImage))
            {
                BatchReadFileInStreamHeaders localFileTextHeaders = client.BatchReadFileInStreamAsync(imageStream).Result;
                string operationLocation = localFileTextHeaders.OperationLocation;
                
                string operationId = operationLocation.Substring(operationLocation.Length - numberOfCharsInOperationId);
                
                int i = 0;
                int maxRetries = 10;
                ReadOperationResult results;
                do
                {
                    results = client.GetReadOperationResultAsync(operationId).Result;
                    Console.WriteLine("Server status: {0}, waiting {1} seconds...", results.Status, i);
                    Task.Delay(1000);
                    if (i == 9)
                    {
                        return String.Empty;
                    }
                }
                while ((results.Status == TextOperationStatusCodes.Running ||
                        results.Status == TextOperationStatusCodes.NotStarted) && i++ < maxRetries);
                
                var textRecognitionLocalFileResults = results.RecognitionResults;
                foreach (TextRecognitionResult recResult in textRecognitionLocalFileResults)
                {
                    foreach (Line line in recResult.Lines)
                    {
                        stringBuilder.Append(line.Text);
                    }
                }
            }

            return stringBuilder.ToString();
        }
        private ComputerVisionClient Authenticate(String endpoint, String key)
        {
            ComputerVisionClient client =
                new ComputerVisionClient(new ApiKeyServiceClientCredentials(key))
                    { Endpoint = endpoint };
            return client;
        }

        [HttpGet]
        public IActionResult Test()
        {
            return Ok();
        }
    }
    
}