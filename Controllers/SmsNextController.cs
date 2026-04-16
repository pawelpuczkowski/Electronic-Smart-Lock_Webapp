using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace ZamekDoDrzwi.Controllers
{
    [ApiController]
    [Route("api/sms-next")] // Endpoint: /api/sms-next
    public class SmsNextController : ControllerBase
    {
        private readonly IConfiguration _settings;

        public SmsNextController(IConfiguration settings)
        {
            _settings = settings;
        }

        [HttpGet]
        public IActionResult Get()
        {
            try
            {
                // Połączenie z bazą danych
                using var db = new MySqlConnection(_settings.GetConnectionString("MySql"));
                db.Open();

                // Pobranie pierwszego oczekującego kodu SMS
                const string query = @"
                    SELECT 
                        id, 
                        phone_number, 
                        login_code,
                        GREATEST(TIMESTAMPDIFF(SECOND, NOW(), valid_until), 0) AS ttl_sec
                    FROM access_codes
                    WHERE sms_sent = 0
                      AND valid_until > NOW()
                      AND is_blocked = 0
                    ORDER BY id ASC
                    LIMIT 1";

                using var selectCmd = new MySqlCommand(query, db);
                using var reader = selectCmd.ExecuteReader();

                // Jeśli jest kod do wysłania — zwróć dane w formacie tekstowym
                if (reader.Read())
                {
                    int codeId = reader.GetInt32("id");
                    string phoneNumber = reader["phone_number"]?.ToString() ?? "";
                    string loginCode = reader["login_code"]?.ToString() ?? "";
                    int ttlSeconds = Convert.ToInt32(reader["ttl_sec"]);

                    // Format odpowiedzi: id;phone;code;ttl
                    return Content($"{codeId};{phoneNumber};{loginCode};{ttlSeconds}\n", "text/plain; charset=utf-8");
                }
                else
                {
                    // Brak kodów do wysłania – zwróć pustą odpowiedź
                    return Content("", "text/plain; charset=utf-8");
                }
            }
            catch (Exception error)
            {
                // Obsługa błędów (np. połączenie DB)
                Response.StatusCode = 500;
                return Content($"error: {error.Message}", "text/plain; charset=utf-8");
            }
        }
    }
}
