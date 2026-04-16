using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

namespace ZamekDoDrzwi.Pages.Powiadomienia
{
    public class KonfiguracjaModel : AdminPageModel
    {
        private readonly IConfiguration _config;
        private readonly ILogger<KonfiguracjaModel> _logger;

        public KonfiguracjaModel(IConfiguration config, ILogger<KonfiguracjaModel> logger)
        {
            _config = config;
            _logger = logger;
        }

        // Formularz konfiguracji globalnych preferencji uzytkownika
        [BindProperty]
        public PreferencjeForm Preferencje { get; set; } = new();

        // Komunikat informacyjny (po zapisie)
        [TempData]
        public string? KomunikatKanaly { get; set; }

        // Model danych formularza
        public class PreferencjeForm
        {
            public bool EmailEnabled { get; set; }
            public bool LogEnabled { get; set; }
        }

        // Wczytanie ustawień użytkownika z bazy
        public IActionResult OnGet()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToPage("/Index"); // brak zalogowanego uzytkownika

            try
            {
                using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
                db.Open();

                string dbSql = @"
                    SELECT email_enabled, log_enabled
                    FROM notification_preferences
                    WHERE user_id = @uid
                ";

                using var dbCmd = new MySqlCommand(dbSql, db);
                dbCmd.Parameters.AddWithValue("@uid", userId);

                using var dbReader = dbCmd.ExecuteReader();
                if (dbReader.Read())
                {
                    Preferencje.EmailEnabled = dbReader.GetBoolean("email_enabled");
                    Preferencje.LogEnabled = dbReader.GetBoolean("log_enabled");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "B��d podczas wczytywania preferencji powiadomie�.");
                KomunikatKanaly = "Nie uda�o si� pobra� danych z bazy.";
            }

            return Page();
        }

        // Zapis zmian do bazy danych
        public IActionResult OnPost()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToPage("/Index");

            try
            {
                using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
                db.Open();

                string dbSql = @"
                    INSERT INTO notification_preferences (user_id, email_enabled, log_enabled)
                    VALUES (@uid, @em, @log)
                    ON DUPLICATE KEY UPDATE
                        email_enabled = @em,
                        log_enabled = @log
                ";

                using var dbCmd = new MySqlCommand(dbSql, db);
                dbCmd.Parameters.AddWithValue("@uid", userId);
                dbCmd.Parameters.AddWithValue("@em", Preferencje.EmailEnabled);
                dbCmd.Parameters.AddWithValue("@log", Preferencje.LogEnabled);

                dbCmd.ExecuteNonQuery();

                KomunikatKanaly = "Zapisano preferencje kanałów powiadomień.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas zapisu preferencji powiadomień.");
                KomunikatKanaly = "Wystąpił błąd podczas zapisu danych.";
            }

            return RedirectToPage();
        }
    }
}
