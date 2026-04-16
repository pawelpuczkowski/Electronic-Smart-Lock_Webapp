using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace ZamekDoDrzwi.Controllers
{
    [ApiController]
    [Route("api/rfid-verify")] // Endpoint: /api/rfid-verify
    public class RfidTokenVerifyController : ControllerBase
    {
        private readonly IConfiguration _settings;

        public RfidTokenVerifyController(IConfiguration settings)
        {
            _settings = settings;
        }

        [HttpPost]
        public IActionResult Post(
            [FromForm] string cardUid,
            [FromForm] string verificationCode,
            [FromForm(Name = "token_id")] int tokenId)
        {
            try
            {
                // Podstawowa walidacja danych wejściowych
                if (string.IsNullOrWhiteSpace(cardUid) || string.IsNullOrWhiteSpace(verificationCode) || tokenId <= 0)
                    return Content("ERR", "text/plain");

                using var db = new MySqlConnection(_settings.GetConnectionString("MySql"));
                db.Open();

                // Pobranie tokenu i powiązanej karty RFID
                const string selectQuery = @"
                    SELECT t.id, t.kod_2fa, t.data_waznosci, t.uzyty
                    FROM rfid_tokens t
                    JOIN rfid_cards c ON c.id = t.rfid_card_id
                    WHERE t.id = @tokenId
                      AND c.uid = @uid
                      AND c.aktywna = 1
                      AND (c.data_waznosci IS NULL OR c.data_waznosci > NOW())
                    LIMIT 1";

                using var selectCmd = new MySqlCommand(selectQuery, db);
                selectCmd.Parameters.AddWithValue("@tokenId", tokenId);
                selectCmd.Parameters.AddWithValue("@uid", cardUid);

                using var reader = selectCmd.ExecuteReader();
                if (!reader.Read())
                    return Content("ERR", "text/plain");

                string codeFromDb = reader["kod_2fa"].ToString();
                DateTime expiryDate = reader.GetDateTime("data_waznosci");
                bool isUsed = Convert.ToBoolean(reader["uzyty"]);
                reader.Close();

                // Sprawdzenie stanu tokenu
                if (isUsed)
                    return Content("ERR", "text/plain");

                if (expiryDate <= DateTime.Now)
                    return Content("ERR", "text/plain");

                // Weryfikacja kodu 2FA
                if (codeFromDb != verificationCode)
                {
                    // Zwiększ licznik błędnych prób
                    const string incQuery = "UPDATE rfid_tokens SET proby_bledne = proby_bledne + 1 WHERE id = @id";
                    using var incCmd = new MySqlCommand(incQuery, db);
                    incCmd.Parameters.AddWithValue("@id", tokenId);
                    incCmd.ExecuteNonQuery();

                    return Content("ERR", "text/plain");
                }

                // Oznaczenie tokenu jako użyty (jednorazowy)
                const string updateQuery = "UPDATE rfid_tokens SET uzyty = 1 WHERE id = @id AND uzyty = 0";
                using var updateCmd = new MySqlCommand(updateQuery, db);
                updateCmd.Parameters.AddWithValue("@id", tokenId);
                int affectedRows = updateCmd.ExecuteNonQuery();

                // Zwracamy wynik (OK / ERR)
                return Content(affectedRows > 0 ? "OK" : "ERR", "text/plain");
            }
            catch (Exception error)
            {
                // Obsługa wyjątków i błędów serwera
                Response.StatusCode = 500;
                return Content($"ERR: {error.Message}", "text/plain");
            }
        }
    }
}
