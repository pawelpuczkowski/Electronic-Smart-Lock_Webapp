using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;

namespace ZamekDoDrzwi.Pages.Uzytkownik.Powiadomienia
{
    // Model strony odpowiedzialnej za konfigurację zdarzeń, które mają generować powiadomienia (dla użytkownika)
    public class ZdarzeniaModel : UserPageModel
    {
        private readonly IConfiguration _config;
        private readonly ILogger<ZdarzeniaModel> _logger;

        public ZdarzeniaModel(IConfiguration config, ILogger<ZdarzeniaModel> logger)
        {
            _config = config;
            _logger = logger;
        }

        // Lista wszystkich zdarzeń konfigurowanych przez użytkownika
        [BindProperty]
        public List<ZdarzeniePowiadomienie> Zdarzenia { get; set; } = new();

        // Tymczasowy komunikat do wyświetlenia po zapisaniu zmian
        [TempData]
        public string? KomunikatZdarzenia { get; set; }

        // Model pojedynczego wiersza tabeli zdarzeń
        public class ZdarzeniePowiadomienie
        {
            public string EventType { get; set; } = "";     // Typ zdarzenia w bazie
            public bool EmailEnabled { get; set; }          // Czy wysyłać powiadomienie e-mail
            public bool LogEnabled { get; set; }            // Czy zapisywać zdarzenie do logów

            // Czytelna etykieta do wyświetlenia w interfejsie
            public string EventLabel => EventType switch
            {
                "door_remote_opened" => "Zdalne otwarcie drzwi",
                "doorbell_pressed" => "Naciśnięcie dzwonka",
                "door_opened" => "Otworzenie drzwi",
                "access_denied" => "Odmowa dostępu",
                "code_generated" => "Wygenerowanie kodu",
                "camera_photo" => "Zdjęcie z kamery",
                _ => EventType
            };
        }

        // Wczytanie istniejących zdarzeń i uzupełnienie brakujących
        public IActionResult OnGet()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToPage("/Index");

            try
            {
                using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
                db.Open();

                // Lista wszystkich typów zdarzeń dostępnych w systemie
                var wszystkieTypy = new List<string>
                {
                    "door_opened",
                    "access_denied",
                    "code_generated",
                    "camera_photo",
                    "door_remote_opened",
                    "doorbell_pressed"
                };

                // Zbiór zdarzeń już istniejących w bazie dla danego użytkownika
                var existing = new HashSet<string>();

                // Pobierz istniejące ustawienia użytkownika
                string selectSql = "SELECT event_type, email_enabled, log_enabled FROM notification_events WHERE user_id = @id";
                using var selectCmd = new MySqlCommand(selectSql, db);
                selectCmd.Parameters.AddWithValue("@id", userId.Value);

                using var reader = selectCmd.ExecuteReader();
                while (reader.Read())
                {
                    string type = reader.GetString("event_type");
                    existing.Add(type);

                    Zdarzenia.Add(new ZdarzeniePowiadomienie
                    {
                        EventType = type,
                        EmailEnabled = reader.GetBoolean("email_enabled"),
                        LogEnabled = reader.GetBoolean("log_enabled")
                    });
                }
                reader.Close();

                // Dodaj brakujące zdarzenia do tabeli i modelu
                foreach (var typ in wszystkieTypy)
                {
                    if (!existing.Contains(typ))
                    {
                        string insertSql = @"
                            INSERT INTO notification_events (user_id, event_type, email_enabled, log_enabled)
                            VALUES (@id, @typ, 0, 1)";
                        using var insertCmd = new MySqlCommand(insertSql, db);
                        insertCmd.Parameters.AddWithValue("@id", userId.Value);
                        insertCmd.Parameters.AddWithValue("@typ", typ);
                        insertCmd.ExecuteNonQuery();

                        Zdarzenia.Add(new ZdarzeniePowiadomienie
                        {
                            EventType = typ,
                            EmailEnabled = false,
                            LogEnabled = true
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas ładowania zdarzeń powiadomień.");
            }

            return Page();
        }

        // Zapis ustawień zdarzeń użytkownika
        public IActionResult OnPost()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null) return RedirectToPage("/Index");

            try
            {
                using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
                db.Open();

                // Pobierz globalne ustawienia kanałów dla użytkownika
                bool globalEmail = false, globalLog = false;
                using (var prefCmd = new MySqlCommand("SELECT email_enabled, log_enabled FROM notification_preferences WHERE user_id = @id", db))
                {
                    prefCmd.Parameters.AddWithValue("@id", userId.Value);
                    using var reader = prefCmd.ExecuteReader();
                    if (reader.Read())
                    {
                        globalEmail = reader.GetBoolean("email_enabled");
                        globalLog = reader.GetBoolean("log_enabled");
                    }
                }

                // Lista ostrzeżeń dla nieaktywnych kanałów globalnych
                List<string> ostrzezenia = new();
                foreach (var zd in Zdarzenia)
                {
                    if (zd.EmailEnabled && !globalEmail) ostrzezenia.Add("e-mail");
                    if (zd.LogEnabled && !globalLog) ostrzezenia.Add("logi");
                }

                // Zapisz lub zaktualizuj zdarzenia użytkownika
                foreach (var zd in Zdarzenia)
                {
                    string sql = @"
                        INSERT INTO notification_events (user_id, event_type, email_enabled, log_enabled)
                        VALUES (@id, @ev, @em, @log)
                        ON DUPLICATE KEY UPDATE
                            email_enabled = @em,
                            log_enabled = @log";

                    using var cmd = new MySqlCommand(sql, db);
                    cmd.Parameters.AddWithValue("@id", userId.Value);
                    cmd.Parameters.AddWithValue("@ev", zd.EventType);
                    cmd.Parameters.AddWithValue("@em", zd.EmailEnabled);
                    cmd.Parameters.AddWithValue("@log", zd.LogEnabled);
                    cmd.ExecuteNonQuery();
                }

                // Ustal komunikat w zależności od stanu globalnych ustawień
                KomunikatZdarzenia = ostrzezenia.Any()
                    ? $"Wybrałeś powiadomienia ({string.Join(", ", ostrzezenia.Distinct())}), ale globalnie te kanały są wyłączone – takie powiadomienia nie będą wysyłane."
                    : "Zapisano ustawienia powiadomień dla zdarzeń.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd przy zapisie zdarzeń powiadomień.");
                KomunikatZdarzenia = "Wystąpił błąd podczas zapisu.";
            }

            // Odśwież stronę po zapisie (z komunikatem)
            return RedirectToPage();
        }
    }
}
