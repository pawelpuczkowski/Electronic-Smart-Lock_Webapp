using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Text.Json;

namespace ZamekDoDrzwi.Controllers
{
    [ApiController]
    [Route("api/esp-notify")] // Endpoint: /api/esp-notify
    public class EspNotificationController : ControllerBase
    {
        private readonly IConfiguration _settings;
        private readonly IWebHostEnvironment _environment;

        public EspNotificationController(IConfiguration settings, IWebHostEnvironment environment)
        {
            _settings = settings;
            _environment = environment;
        }

        [HttpPost]
        public IActionResult Post([FromBody] JsonElement payload)
        {
            // Token autoryzacyjny (powinien być zgodny z tym na ESP)
            const string sharedSecret = "MojeSuperHaslo123";

            if (!Request.Headers.TryGetValue("Authorization", out var authHeader) ||
                authHeader.ToString() != $"Bearer {sharedSecret}")
            {
                Response.StatusCode = 403;
                return new JsonResult(new { error = "Brak autoryzacji" });
            }

            try
            {
                // Zapis surowego JSON-a do pliku (pomocne przy debugowaniu komunikacji z ESP)
                string jsonContent = payload.GetRawText();
                string debugLogPath = Path.Combine(_environment.ContentRootPath, "debug_notifications.txt");
                System.IO.File.AppendAllText(debugLogPath, jsonContent + Environment.NewLine);

                // Odczyt wymaganych danych z JSON-a
                string eventType = payload.TryGetProperty("eventType", out var et)
                    ? et.GetString() ?? "unknown"
                    : "unknown";

                string message = payload.TryGetProperty("message", out var msg)
                    ? msg.GetString() ?? ""
                    : "";

                // Połączenie z bazą danych
                using var db = new MySqlConnection(_settings.GetConnectionString("MySql"));
                db.Open();

                // Wstawienie nowego rekordu do tabeli powiadomień
                const string insertQuery = @"
                    INSERT INTO esp_notifications (event_type, message, processed)
                    VALUES (@type, @msg, 0)";

                using var insertCmd = new MySqlCommand(insertQuery, db);
                insertCmd.Parameters.AddWithValue("@type", eventType);
                insertCmd.Parameters.AddWithValue("@msg", message);
                insertCmd.ExecuteNonQuery();

                long recordId = insertCmd.LastInsertedId;

                // Sukces – odpowiedź dla ESP
                return new JsonResult(new
                {
                    status = "ok",
                    id = recordId,
                    eventType,
                    message = "Powiadomienie zapisane"
                });
            }
            catch (JsonException)
            {
                // Błąd w strukturze JSON-a
                Response.StatusCode = 400;
                return new JsonResult(new { error = "Nieprawidłowy JSON" });
            }
            catch (Exception error)
            {
                // Zapis błędów SQL do lokalnego logu (pomocne przy testach)
                string errorLogPath = Path.Combine(_environment.ContentRootPath, "debug_sql_errors.txt");
                System.IO.File.AppendAllText(errorLogPath, error.Message + Environment.NewLine);

                Response.StatusCode = 500;
                return new JsonResult(new { error = "Błąd serwera: " + error.Message });
            }
        }
    }
}
