using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;

namespace ZamekDoDrzwi.Pages.Logi
{
    public class KodyModel : AdminPageModel
    {
        private readonly IConfiguration _config;

        // Lista wszystkich kodów (dla widoku)
        public List<KodDostepu> Kody { get; set; } = new();

        public KodyModel(IConfiguration config)
        {
            _config = config;
        }

        // Pobierz listę kodów z bazy danych
        public void OnGet()
        {
            try
            {
                using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
                db.Open();

                // Aliasujemy is_blocked jako used_login_code, żeby nie zmieniać modelu widoku
                string dbQuery = @"
                    SELECT id, phone_number, login_code, valid_until,
                           is_blocked AS used_login_code
                    FROM access_codes
                    ORDER BY created_at DESC";

                using var dbCmd = new MySqlCommand(dbQuery, db);
                using var dbReader = dbCmd.ExecuteReader();

                while (dbReader.Read())
                {
                    Kody.Add(new KodDostepu
                    {
                        Id = dbReader.GetInt32("id"),
                        PhoneNumber = dbReader.GetString("phone_number"),
                        LoginCode = dbReader.GetString("login_code"),
                        ValidUntil = dbReader.GetDateTime("valid_until"),
                        UsedLoginCode = dbReader.GetBoolean("used_login_code") // alias dla is_blocked
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas pobierania kodów dostępu: {ex.Message}");
            }
        }

        // Zablokowanie kodu (is_blocked = 1)
        public IActionResult OnPostZablokuj(int id)
        {
            try
            {
                using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
                db.Open();

                const string dbQuery = "UPDATE access_codes SET is_blocked = 1 WHERE id = @id";
                using var dbCmd = new MySqlCommand(dbQuery, db);
                dbCmd.Parameters.AddWithValue("@id", id);
                dbCmd.ExecuteNonQuery();

                TempData["KomunikatKod"] = "Kod został zablokowany.";
            }
            catch (Exception ex)
            {
                TempData["KomunikatKod"] = $"Błąd blokowania: {ex.Message}";
            }

            return RedirectToPage();
        }

        // Odblokowanie kodu (is_blocked = 0)
        public IActionResult OnPostOdblokuj(int id)
        {
            try
            {
                using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
                db.Open();

                const string dbQuery = "UPDATE access_codes SET is_blocked = 0 WHERE id = @id";
                using var dbCmd = new MySqlCommand(dbQuery, db);
                dbCmd.Parameters.AddWithValue("@id", id);
                dbCmd.ExecuteNonQuery();

                TempData["KomunikatKod"] = "Kod został odblokowany.";
            }
            catch (Exception ex)
            {
                TempData["KomunikatKod"] = $"Błąd odblokowania: {ex.Message}";
            }

            return RedirectToPage();
        }

        // Usunięcie kodu z bazy
        public async Task<IActionResult> OnPostUsunAsync(int id)
        {
            try
            {
                using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
                await db.OpenAsync();

                const string dbQuery = "DELETE FROM access_codes WHERE id = @id";
                using var dbCmd = new MySqlCommand(dbQuery, db);
                dbCmd.Parameters.AddWithValue("@id", id);
                await dbCmd.ExecuteNonQueryAsync();

                TempData["KomunikatKod"] = "Kod został usunięty.";
            }
            catch (Exception ex)
            {
                TempData["KomunikatKod"] = $"Błąd usuwania: {ex.Message}";
            }

            return RedirectToPage();
        }

        //Model danych dla jednego kodu dostępu
        public class KodDostepu
        {
            public int Id { get; set; }
            public string PhoneNumber { get; set; } = "";
            public string LoginCode { get; set; } = "";
            public DateTime ValidUntil { get; set; }
            
            public bool UsedLoginCode { get; set; }
        }
    }
}
