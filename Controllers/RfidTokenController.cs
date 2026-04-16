using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Text.RegularExpressions;

namespace ZamekDoDrzwi.Controllers
{
    [ApiController]
    [Route("api/rfid-token")] // Endpoint: /api/rfid-token
    public class RfidTokenController : ControllerBase
    {
        private readonly IConfiguration _settings;

        public RfidTokenController(IConfiguration settings)
        {
            _settings = settings;
        }

        [HttpPost]
        public IActionResult Post([FromForm] string cardUid, [FromForm] int? ttlSeconds)
        {
            try
            {
                // Walidacja i normalizacja UID karty
                if (string.IsNullOrWhiteSpace(cardUid))
                    return new JsonResult(new { ok = false, err = "no uid" });

                string normalizedUid = Regex.Replace(cardUid.ToUpper(), @"[^0-9A-F]", "");
                if (string.IsNullOrEmpty(normalizedUid))
                    return new JsonResult(new { ok = false, err = "bad uid" });

                // Okres ważności tokenu (min. 60s, max. 3600s)
                int ttl = ttlSeconds ?? 300;
                if (ttl < 60) ttl = 60;
                if (ttl > 3600) ttl = 3600;

                using var db = new MySqlConnection(_settings.GetConnectionString("MySql"));
                db.Open();

                // Wyszukanie karty RFID i powiązanego użytkownika
                const string findCardQuery = @"
                    SELECT c.id AS card_id, c.user_id, u.telefon AS phone
                    FROM rfid_cards c
                    LEFT JOIN users u ON u.id = c.user_id
                    WHERE REPLACE(REPLACE(UPPER(c.uid), ':', ''), '-', '') = @uid
                      AND c.aktywna = 1
                      AND (c.data_waznosci IS NULL OR c.data_waznosci > NOW())
                    LIMIT 1";

                using var findCmd = new MySqlCommand(findCardQuery, db);
                findCmd.Parameters.AddWithValue("@uid", normalizedUid);

                using var reader = findCmd.ExecuteReader();
                if (!reader.Read())
                    return new JsonResult(new { ok = false, err = "card" });

                int cardId = reader.GetInt32("card_id");
                int userId = reader.GetInt32("user_id");
                string phoneNumber = reader["phone"]?.ToString()?.Trim() ?? "";
                reader.Close();

                if (string.IsNullOrEmpty(phoneNumber))
                    return new JsonResult(new { ok = false, err = "phone" });

                // Dezaktywacja poprzednich niewykorzystanych tokenów
                using (var disableOld = new MySqlCommand(
                    "UPDATE rfid_tokens SET uzyty = 1 WHERE rfid_card_id = @id AND uzyty = 0", db))
                {
                    disableOld.Parameters.AddWithValue("@id", cardId);
                    disableOld.ExecuteNonQuery();
                }

                // Generowanie nowego kodu 2FA (6 cyfr)
                string verificationCode = new Random().Next(100000, 999999).ToString();

                // Wstawienie nowego tokenu do bazy
                const string insertQuery = @"
                    INSERT INTO rfid_tokens 
                        (rfid_card_id, user_id, kod_2fa, data_waznosci, uzyty, proby_bledne)
                    VALUES 
                        (@cardId, @userId, @code, DATE_ADD(NOW(), INTERVAL @ttl SECOND), 0, 0)";

                using var insertCmd = new MySqlCommand(insertQuery, db);
                insertCmd.Parameters.AddWithValue("@cardId", cardId);
                insertCmd.Parameters.AddWithValue("@userId", userId);
                insertCmd.Parameters.AddWithValue("@code", verificationCode);
                insertCmd.Parameters.AddWithValue("@ttl", ttl);
                insertCmd.ExecuteNonQuery();

                long newTokenId = insertCmd.LastInsertedId;

                // Odpowiedź JSON dla ESP32 lub aplikacji
                return new JsonResult(new
                {
                    ok = true,
                    token_id = newTokenId,
                    phone = phoneNumber,
                    ttl_sec = ttl,
                    code = verificationCode // kod jednorazowy do SMS
                });
            }
            catch (Exception error)
            {
                // Obsługa błędów (np. połączenie z DB, brak danych itp.)
                return StatusCode(500, new { ok = false, err = error.Message });
            }
        }
    }
}
