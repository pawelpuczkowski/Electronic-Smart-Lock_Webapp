using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace ZamekDoDrzwi.Controllers
{
    [ApiController]
    [Route("api/sms-sent")] // Endpoint: /api/sms-sent
    public class SmsSentController : ControllerBase
    {
        private readonly IConfiguration _settings;

        public SmsSentController(IConfiguration settings)
        {
            _settings = settings;
        }

        [HttpPost]
        public IActionResult Post([FromForm] int? messageId)
        {
            // Walidacja danych wejściowych
            if (messageId == null || messageId <= 0)
            {
                Response.StatusCode = 400;
                return Content("no id", "text/plain");
            }

            try
            {
                // Połączenie z bazą danych
                using var db = new MySqlConnection(_settings.GetConnectionString("MySql"));
                db.Open();

                // Aktualizacja rekordu po pomyślnym wysłaniu SMS-a
                const string updateQuery = @"
                    UPDATE access_codes 
                    SET sms_sent = 1, sent_at = NOW() 
                    WHERE id = @id AND sms_sent = 0";

                using var updateCmd = new MySqlCommand(updateQuery, db);
                updateCmd.Parameters.AddWithValue("@id", messageId);

                int affectedRows = updateCmd.ExecuteNonQuery();

                // Zwróć prostą odpowiedź tekstową ("ok" = zaktualizowano, "skip" = już oznaczony)
                return Content(affectedRows > 0 ? "ok" : "skip", "text/plain");
            }
            catch (Exception error)
            {
                // Obsługa błędów (np. połączenie z DB)
                Response.StatusCode = 500;
                return Content($"error: {error.Message}", "text/plain");
            }
        }
    }
}
