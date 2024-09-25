using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;

namespace InteractiveWebhook
{
    public class WebhookResponse
    {
        [JsonPropertyName("status")]
        public string Status { get; set; } = "success";
        [JsonPropertyName("option")]
        public string Option { get; set; } = "-1";
    }

    public class PhoneNumberTableLookup(ILogger<PhoneNumberTableLookup> logger)
    {
        private readonly ILogger<PhoneNumberTableLookup> _logger = logger;
        private static readonly string? connectionString = Environment.GetEnvironmentVariable("SqlConnectionString");
        private static readonly SqlConnection connection = new(connectionString);
        private static readonly string sqlQuery = "SELECT office_id FROM PhoneNumbers WHERE phone_number = @phone_number;"; // change to correct column names

        [Function("PhoneNumberTableLookup")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
        {
            _logger.LogInformation("Processing request: {}", req.GetDisplayUrl());

            if (req.Query.TryGetValue("dialed_number", out var phoneNumber))
            {
                try
                {
                    if (connection.State != System.Data.ConnectionState.Open)
                    {
                        await connection.OpenAsync();
                    }

                    using SqlCommand cmd = new(sqlQuery, connection);
                    cmd.Parameters.AddWithValue("@phone_number", phoneNumber.ToString());
                    using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                    int officeId = -1;
                    if (await reader.ReadAsync())
                    {
                        officeId = reader.GetInt32(0);
                    }

                    if (officeId == -1)
                    {
                        return new NotFoundResult();
                    }
                    else
                    {
                        return new OkObjectResult(new WebhookResponse { Option = officeId.ToString() });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("An error occurred: {}", ex.Message);
                    return new NotFoundResult();
                }
            }
            else
            {
                return new BadRequestResult();
            }
        }
    }
}
