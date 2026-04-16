using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;

namespace ZamekDoDrzwi.Pages.Powiadomienia
{
    public class ZdarzeniaModel : AdminPageModel
    {
        private readonly IConfiguration _config;
        private readonly ILogger<ZdarzeniaModel> _logger;

        public ZdarzeniaModel(IConfiguration config, ILogger<ZdarzeniaModel> logger)
        {
            _config = config;
            _logger = logger;
        }

        [BindProperty]
        public List<ZdarzeniePowiadomienie> Zdarzenia { get; set; } = new();

        [TempData]
        public string? KomunikatZdarz { get; set; }

        // Klasa modelu pojedynczego zdarzenia
        public class ZdarzeniePowiadomienie
        {
            public string EventType { get; set; } = "";
            public bool EmailEnabled { get; set; }
            public bool LogEnabled { get; set; }

            // Opis zdarzenia widoczny w interfejsie
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

        // Wczytanie zdarzeń z bazy danych
        public IActionResult OnGet()
        {
            int? userId = HttpContext.Session.GetInt32("UserId");
            if (userId == null)
                return RedirectToPage("/Index");

            try
            {
                using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
                db.Open();

                // Lista wszystkich dostępnych typów zdarzeń w systemie
                var allEventTypes = new List<string>
                {
                    "door_opened",
                    "access_denied",
                    "code_generated",
                    "camera_photo",
                    "door_remote_opened",
                    "doorbell_pressed"
                };

                var existingEventTypes = new HashSet<string>();

                // Pobierz zapisane zdarzenia użytkownika
                string dbSql = "SELECT event_type, email_enabled, log_enabled FROM notification_events WHERE user_id = @uid";
                using var dbCmd = new MySqlCommand(dbSql, db);
                dbCmd.Parameters.AddWithValue("@uid", userId.Value);

                using var dbReader = dbCmd.ExecuteReader();
                while (dbReader.Read())
                {
                    string type = dbReader.GetString("event_type");
                    existingEventTypes.Add(type);

                    Zdarzenia.Add(new ZdarzeniePowiadomienie
                    {
                        EventType = type,
                        EmailEnabled = dbReader.GetBoolean("email_enabled"),
                        LogEnabled = dbReader.GetBoolean("log_enabled")
                    });
                }
                dbReader.Close();

                // Dodaj brakujące zdarzenia (domyślnie logi = włączone)
                foreach (var typ in allEventTypes)
                {
                    if (!existingEventTypes.Contains(typ))
                    {
                        string dbInsertSql = @"
                            INSERT INTO notification_events (user_id, event_type, email_enabled, log_enabled)
                            VALUES (@uid, @type, 0, 1)";
                        using var dbInsertCmd = new MySqlCommand(dbInsertSql, db);
                        dbInsertCmd.Parameters.AddWithValue("@uid", userId.Value);
                        dbInsertCmd.Parameters.AddWithValue("@type", typ);
                        dbInsertCmd.ExecuteNonQuery();

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
                KomunikatZdarz = "Nie udało się załadować listy zdarzeń.";
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

                // Pobierz globalne preferencje (jeśli istnieją)
                bool globalEmail = false;
                bool globalLog = false;

                using (var dbPrefCmd = new MySqlCommand("SELECT email_enabled, log_enabled FROM notification_preferences WHERE user_id = @uid", db))
                {
                    dbPrefCmd.Parameters.AddWithValue("@uid", userId.Value);
                    using var dbReader = dbPrefCmd.ExecuteReader();
                    if (dbReader.Read())
                    {
                        globalEmail = dbReader.GetBoolean("email_enabled");
                        globalLog = dbReader.GetBoolean("log_enabled");
                    }
                }

                // Aktualizacja ustawień dla każdego zdarzenia
                foreach (var zd in Zdarzenia)
                {
                    string dbSql = @"
                        INSERT INTO notification_events (user_id, event_type, email_enabled, log_enabled)
                        VALUES (@uid, @type, @email, @log)
                        ON DUPLICATE KEY UPDATE
                            email_enabled = @email,
                            log_enabled = @log";

                    using var dbCmd = new MySqlCommand(dbSql, db);
                    dbCmd.Parameters.AddWithValue("@uid", userId.Value);
                    dbCmd.Parameters.AddWithValue("@type", zd.EventType);
                    dbCmd.Parameters.AddWithValue("@email", zd.EmailEnabled);
                    dbCmd.Parameters.AddWithValue("@log", zd.LogEnabled);
                    dbCmd.ExecuteNonQuery();

                    // Ostrzeżenie – lokalny kanał włączony, ale globalny wyłączony
                    if ((zd.EmailEnabled && !globalEmail) || (zd.LogEnabled && !globalLog))
                        KomunikatZdarz = "Niektóre zdarzenia mają aktywne kanały, które są globalnie wyłączone.";
                }

                KomunikatZdarz ??= "Zapisano ustawienia powiadomień dla zdarzeń.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd przy zapisie zdarzeń powiadomień.");
                KomunikatZdarz = "Wystąpił błąd podczas zapisu danych.";
            }

            return RedirectToPage();
        }
    }
}
