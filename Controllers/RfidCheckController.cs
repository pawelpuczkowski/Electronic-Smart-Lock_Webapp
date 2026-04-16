using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace ZamekDoDrzwi.Controllers
{
    [ApiController]
    [Route("api/rfid-check")] // Endpoint: /api/rfid-check
    public class RfidCheckController : ControllerBase
    {
        private readonly IConfiguration _settings;

        public RfidCheckController(IConfiguration settings)
        {
            _settings = settings;
        }

        [HttpPost]
        public IActionResult Post([FromForm] string cardUid)
        {
            //  Weryfikacja, czy UID został przesłany
            if (string.IsNullOrEmpty(cardUid))
                return new JsonResult(new { success = false, reason = "Brak UID karty" });

            try
            {
                // Połączenie z bazą danych
                using var db = new MySqlConnection(_settings.GetConnectionString("MySql"));
                db.Open();

                // Sprawdzenie, czy karta istnieje i jest aktywna
                const string query = @"
                    SELECT COUNT(*) 
                    FROM rfid_cards 
                    WHERE uid = @uid AND aktywna = 1";

                using var checkCmd = new MySqlCommand(query, db);
                checkCmd.Parameters.AddWithValue("@uid", cardUid);

                int foundCount = Convert.ToInt32(checkCmd.ExecuteScalar());

                // Odpowiedź JSON dla systemu
                if (foundCount > 0)
                    return new JsonResult(new { success = true });
                else
                    return new JsonResult(new { success = false });
            }
            catch (Exception error)
            {
                // W przypadku błędu (np. brak połączenia z bazą)
                return StatusCode(500, new { success = false, error = error.Message });
            }
        }
    }
}
