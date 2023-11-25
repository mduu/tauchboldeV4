using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure;
using Azure.Communication.Email;

namespace Tauchbolde.ContactFormMailer
{
    public static class ContactForm
    {
        [FunctionName("ContactForm")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("SendEmails Function Triggered.");

            // Check secret
            string secret = req.Query["geheim"];
            if (!int.TryParse(secret, out var secretResult))
            {
                log.LogError("No secret result ('geheim' field)");
                return new BadRequestResult();
            }
            if (secretResult != 42)
            {
                log.LogError("Invalid secret result ('geheim' vield). Its not 42.");
                return new BadRequestResult();
            }

            string name = req.Query["name"];
            string email = req.Query["email"];
            string numberOfDives = req.Query["numberOfDives"];
            string education = req.Query["education"];
            string message = req.Query["message"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;
            email = email ?? data?.email;
            numberOfDives = numberOfDives ?? data?.numberOfDives;
            education = education ?? data?.education;
            message = message ?? data?.message;

            var myEmailAddress = Environment.GetEnvironmentVariable("recipient");
            var senderEmailAddress = Environment.GetEnvironmentVariable("senderEmailAddress");            
            var emailClient = new EmailClient(Environment.GetEnvironmentVariable("AzureCommunicationServicesConnectionString"));
            try
            {
                //Email to notify myself
                var selfEmailSendOperation = await emailClient.SendAsync(
                    wait: WaitUntil.Completed,
                    senderAddress: senderEmailAddress,
                    recipientAddress: myEmailAddress,
                    subject: $"Tauchbolde Kontaktformular von {name} ({email})",
                    htmlContent: $"<html><body>Name:&nbsp;{name}<br />Email:&nbsp;{email}<br />Anzahl TG's:&nbsp;{numberOfDives}<br />Ausbildung:&nbsp;{education}<br />Message:<br />{message}</body></html>");

                log.LogInformation($"Email sent with message ID: {selfEmailSendOperation.Id} and status: {selfEmailSendOperation.Value.Status}");
                
                return new OkObjectResult($"Emails sent.");
            }
            catch (RequestFailedException ex)
            {
                log.LogError($"Sending email operation failed with error code: {ex.ErrorCode}, message: {ex.Message}");
                return new ConflictObjectResult("Error sending email");
            }
        }
    }
}
