using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace ZamekDoDrzwi.Controllers
{
    [ApiController]
    [Route("api/verify-code")]
    public class CodeVerificationController : ControllerBase
    {
        private readonly IConfiguration _settings;

        public CodeVerificationController(IConfiguration settings)
        {
            _settings = settings;
        }

        [HttpPost]
        public IActionResult Post([FromForm] string accessCode)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(accessCode))
                    return Content("ERR", "text/plain");

                using var db = new MySqlConnection(_settings.GetConnectionString("MySql"));
                db.Open();

                // Sprawdzenie poprawności i ważności kodu
                const string checkQuery = @"
                    SELECT id, max_uses, used_count
                    FROM access_codes
                    WHERE login_code = @code
                      AND valid_until > NOW()
                      AND is_blocked = 0
                    ORDER BY id DESC
                    LIMIT 1";

                using var checkCmd = new MySqlCommand(checkQuery, db);
                checkCmd.Parameters.AddWithValue("@code", accessCode);

                using var rdr = checkCmd.ExecuteReader();
                if (!rdr.Read())
                    return Content("ERR", "text/plain");

                int codeId = rdr.GetInt32("id");
                object maxUseObj = rdr["max_uses"];
                int currentUseCount = rdr.GetInt32("used_count");
                rdr.Close();

                int? maxUses = maxUseObj == DBNull.Value ? null : Convert.ToInt32(maxUseObj);
                bool stillValid = !maxUses.HasValue || currentUseCount < maxUses.Value;

                if (!stillValid)
                    return Content("ERR", "text/plain");

                // Aktualizacja użyć kodu
                const string updateQuery = @"
                    UPDATE access_codes
                    SET used_count = used_count + 1,
                        used_at = NOW(),
                        used_login_code = IF(max_uses IS NULL, 0, IF(used_count + 1 >= max_uses, 1, 0))
                    WHERE id = @id
                      AND valid_until > NOW()
                      AND is_blocked = 0
                      AND (max_uses IS NULL OR used_count < max_uses)";

                using var updateCmd = new MySqlCommand(updateQuery, db);
                updateCmd.Parameters.AddWithValue("@id", codeId);
                int rowsUpdated = updateCmd.ExecuteNonQuery();

                return Content(rowsUpdated > 0 ? "OK" : "ERR", "text/plain");
            }
            catch (Exception error)
            {
                Response.StatusCode = 500;
                return Content($"ERR: {error.Message}", "text/plain");
            }
        }
    }
}
