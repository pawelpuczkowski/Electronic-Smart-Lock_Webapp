using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using MySql.Data.MySqlClient;

namespace ZamekDoDrzwi.Pages.Uzytkownik.Powiadomienia
{
    // Klasa strony odpowiedzialna za konfigurację globalnych kanałów powiadomień (dla użytkownika)
    public class KonfiguracjaModel : UserPageModel
    {
        private readonly IConfiguration _config;
        private readonly ILogger<KonfiguracjaModel> _logger;

        // Konstruktor - pobiera konfigurację połączenia z bazą i loggera do diagnostyki
        public KonfiguracjaModel(IConfiguration config, ILogger<KonfiguracjaModel> logger)
        {
            _config = config;
            _logger = logger;
        }

        // Model formularza powiązanego z widokiem (checkboxy)
        [BindProperty]
        public PreferencjeForm Preferencje { get; set; } = new();

        // TempData pozwala wyświetlić komunikat po odwiedeniu strony
        [TempData]
        public string? Komunikat { get; set; }

        // Klasa pomocnicza dla danych formularza
        public class PreferencjeForm
        {
            public bool EmailEnabled { get; set; } // Czy użytkownik chce otrzymywać e-maile
            public bool LogEnabled { get; set; }   // Czy zapisywać zdarzenia w logach
        }

        // Wczytanie aktualnych preferencji użytkownika z bazy
        public IActionResult OnGet()
        {
            // Pobranie ID użytkownika z sesji
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
            {
                _logger.LogWarning("Brak sesji u�ytkownika przy OnGet");
                return RedirectToPage("/Index");
            }

            try
            {
                using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
                db.Open();

                // Pobranie preferencji powiadomień dla danego użytkownika
                var cmd = new MySqlCommand(
                    "SELECT email_enabled, log_enabled FROM notification_preferences WHERE user_id = @id",
                    db);
                cmd.Parameters.AddWithValue("@id", userId);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    Preferencje.EmailEnabled = reader.GetBoolean("email_enabled");
                    Preferencje.LogEnabled = reader.GetBoolean("log_enabled");
                }
            }
            catch (Exception ex)
            {
                // Logowanie błędu po stronie serwera
                _logger.LogError(ex, "Błąd podczas wczytywania preferencji powiadomień.");
            }

            return Page();
        }

        // Zapisanie zmian w preferencjach użytkownika
        public IActionResult OnPost()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToPage("/Index");

            try
            {
                using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
                db.Open();

                // Wstawienie lub aktualizacja rekordu w tabeli notification_preferences
                var sql = @"
                    INSERT INTO notification_preferences (user_id, email_enabled, log_enabled)
                    VALUES (@id, @em, @log)
                    ON DUPLICATE KEY UPDATE
                        email_enabled = @em,
                        log_enabled = @log";

                var cmd = new MySqlCommand(sql, db);
                cmd.Parameters.AddWithValue("@id", userId);
                cmd.Parameters.AddWithValue("@em", Preferencje.EmailEnabled);
                cmd.Parameters.AddWithValue("@log", Preferencje.LogEnabled);
                cmd.ExecuteNonQuery();

                // Informacja o sukcesie
                Komunikat = "Zapisano preferencje kanałów powiadomień.";
            }
            catch (Exception ex)
            {
                // Logowanie błędu i komunikat dla użytkownika
                _logger.LogError(ex, "Błąd podczas zapisu preferencji powiadomień.");
                Komunikat = "Wystąpił błąd podczas zapisu.";
            }

            // Odświeżenie strony z komunikatem (TempData)
            return RedirectToPage();
        }
    }
}
