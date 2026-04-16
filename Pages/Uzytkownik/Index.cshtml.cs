using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ZamekDoDrzwi.Services;

namespace ZamekDoDrzwi.Pages.Uzytkownik
{
    // Główna strona dashboardu użytkownika
    public class IndexModel : UserPageModel
    {
        private readonly IConfiguration _config;
          private readonly ILogger<DashboardModel> _logger;
    private readonly MqttWorker _mqtt;

        public IndexModel(IConfiguration config, ILogger<DashboardModel> logger, MqttWorker mqtt)
        {
            _config = config;
             _logger = logger;
        _mqtt = mqtt;
            
        }

        // Sekcja: Status systemu
        public string StatusDrzwi { get; set; } = "Brak danych";
        public string StatusPolaczenia { get; set; } = "Brak danych";
        public string StatusRFID { get; set; } = "Brak danych";
        public string StatusGSM { get; set; } = "Brak danych";
        public string StatusKamera { get; set; } = "Brak danych";
        public DateTime DataAktualizacji { get; set; } = DateTime.MinValue;

        // Sekcja: Podgląd kamery
        public string OstatnieZdjecie { get; set; } = "/images/sample.jpg"; // obraz domyślny

        // Sekcja: Ostatnie zdarzenia
        public List<Zdarzenie> OstatnieZdarzenia { get; set; } = new();

        // Sekcja: Kod tymczasowy
        public string? KodTymczasowy { get; set; } = null;
        public DateTime? KodWaznyDo { get; set; } = null;

        // Sekcja: Globalne powiadomienia użytkownika
        public bool EmailEnabled { get; set; } = true;
        public bool SmsEnabled { get; set; } = false;
        public bool LogEnabled { get; set; } = true;

        // Sekcja: Ostatnie powiadomienia
        public List<Powiadomienie> Powiadomienia { get; set; } = new();

        
        // Ładuje dane do widoku użytkownika       
        public IActionResult OnGet()
        {
            var userId = HttpContext.Session.GetInt32("UserId");
            var rola = HttpContext.Session.GetString("Rola");

            // Sprawdzenie sesji - jeśli brak, wróć do logowania
            if (userId == null || string.IsNullOrEmpty(rola))
            {
                return RedirectToPage("/Index");
            }

            // Wczytanie sekcji dashboardu
            WczytajStatusSystemu();
            WczytajOstatnieZdarzenia();
            WczytajOstatnieZdjecie();
            WczytajPowiadomienia(userId.Value);

            return Page();
        }

        // Odczyt aktualnego statusu urządzenia z bazy (device_status)
       
        private void WczytajStatusSystemu()
        {
            using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
            db.Open();

            string sql = "SELECT * FROM device_status ORDER BY data_aktualizacji DESC LIMIT 1";
            using var cmd = new MySqlCommand(sql, db);
            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                DataAktualizacji = reader.GetDateTime("data_aktualizacji");

                // Jeśli dane starsze niż 5 minut — traktujemy jako nieaktualne
                bool stareDane = DateTime.Now - DataAktualizacji > TimeSpan.FromMinutes(5);

                if (stareDane)
                {
                    StatusDrzwi = StatusPolaczenia = StatusRFID = StatusGSM = StatusKamera = "Brak danych";
                }
                else
                {
                    StatusDrzwi = reader["status_drzwi"]?.ToString() ?? "Brak danych";
                    StatusPolaczenia = reader["status_polaczenia"]?.ToString() ?? "Brak danych";
                    StatusRFID = reader["status_rfid"]?.ToString() ?? "Brak danych";
                    StatusGSM = reader["status_gsm"]?.ToString() ?? "Brak danych";
                    StatusKamera = reader["status_kamera"]?.ToString() ?? "Brak danych";
                }
            }
        }
               
        // Odczyt 5 ostatnich zdarzeń z tabeli access_logs        
        private void WczytajOstatnieZdarzenia()
        {
            using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
            db.Open();

            string sql = "SELECT typ_zdarzenia, komponent, szczegoly, czas FROM access_logs ORDER BY id DESC LIMIT 5";
            using var cmd = new MySqlCommand(sql, db);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                OstatnieZdarzenia.Add(new Zdarzenie
                {
                    typ = reader["typ_zdarzenia"]?.ToString() ?? "[brak]",
                    komponent = reader["komponent"]?.ToString() ?? "[brak]",
                    szczegoly = reader["szczegoly"]?.ToString() ?? "[brak]",
                    czas = reader.IsDBNull("czas") ? DateTime.MinValue : reader.GetDateTime("czas")
                });
            }
        }
              
        // Pobiera ścieżkę do ostatniego zdjęcia z kamery    
        private void WczytajOstatnieZdjecie()
{
    try
    {
        using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
        db.Open();

        // Pobieramy tylko nazwę pliku (nie pełną ścieżkę)
        string sql = "SELECT plik_nazwa FROM camera_photos ORDER BY id DESC LIMIT 1";
        using var cmd = new MySqlCommand(sql, db);
        using var reader = cmd.ExecuteReader();

        if (reader.Read())
        {
            string nazwaPliku = reader.GetString("plik_nazwa");

            // Dynamiczny adres URL — automatycznie dobiera https/http i host
            string bazaUrl = $"{Request.Scheme}://{Request.Host}/uploads/";

            // Pełny adres URL do najnowszego zdjęcia
            OstatnieZdjecie = bazaUrl + nazwaPliku;
        }
        else
        {
            // Fallback, gdy brak zdjęć w bazie
            OstatnieZdjecie = "/images/sample.jpg";
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Błąd podczas wczytywania ostatniego zdjęcia: {ex.Message}");
        OstatnieZdjecie = "/images/sample.jpg"; // awaryjnie
    }
}

       
        // Wczytuje 8 ostatnich powiadomień z tabeli esp_notifications
      
        private void WczytajPowiadomienia(int userId)
        {
            try
            {
                using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
                db.Open();

                const string sql = @"
                SELECT event_type, channel, message, success, created_at
                FROM esp_notifications
                WHERE user_id = @uid
                ORDER BY created_at DESC
                LIMIT 8";
                using var cmd = new MySqlCommand(sql, db);
                cmd.Parameters.AddWithValue("@uid", userId);

                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    Powiadomienia.Add(new Powiadomienie
                    {
                        Typ = r.GetString("event_type"),
                        Kanal = r.GetString("channel"),
                        Tresc = r.GetString("message"),
                        Sukces = r.GetBoolean("success"),
                        Data = r.GetDateTime("created_at")
                    });
                }
            }
            catch
            {                
                Console.WriteLine("Brak danych do przeslania.");
            }
        }

        // Wygenerowanie kodu tymczasowego (login_code) i wysłanie przez ESP32
       
       public async Task<IActionResult> OnPostWygenerujKod()
{
    var userId = HttpContext.Session.GetInt32("UserId");
    if (userId == null)
    {
        TempData["msg"] = "Nie jesteś zalogowany.";
        return RedirectToPage();
    }

    using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
    db.Open();

    // Pobranie numeru telefonu użytkownika
    string sqlTel = "SELECT telefon FROM users WHERE id = @uid LIMIT 1";
    string numerTelefonu = "";
    using (var cmdTel = new MySqlCommand(sqlTel, db))
    {
        cmdTel.Parameters.AddWithValue("@uid", userId.Value);
        var result = cmdTel.ExecuteScalar();

        if (result == null || string.IsNullOrWhiteSpace(result.ToString()))
        {
            TempData["msg"] = "Brak numeru telefonu w Twoim profilu.";
            return RedirectToPage();
        }

        numerTelefonu = result.ToString()!.Trim();
    }

    // Normalizacja numeru PL
    numerTelefonu = Regex.Replace(numerTelefonu, @"[\s\-]", "");
    if (numerTelefonu.Length == 9) numerTelefonu = "+48" + numerTelefonu;
    if (!Regex.IsMatch(numerTelefonu, @"^\+48\d{9}$"))
    {
        TempData["msg"] = "Nieprawidłowy numer telefonu w profilu.";
        return RedirectToPage();
    }

    // Wygenerowanie kodu ważnego 5 minut
    string kodLogowania = new Random().Next(100000, 999999).ToString();
    DateTime wygasaDo = DateTime.Now.AddMinutes(5);

    // Unieważnienie poprzednich kodów
    string updateSql = @"
        UPDATE access_codes 
        SET valid_until = NOW(), used_login_code = 1 
        WHERE phone_number = @telefon 
          AND valid_until > NOW() 
          AND used_login_code = 0";
    using (var updateCmd = new MySqlCommand(updateSql, db))
    {
        updateCmd.Parameters.AddWithValue("@telefon", numerTelefonu);
        updateCmd.ExecuteNonQuery();
    }

    // Wstawienie nowego kodu
    string insertSql = @"
        INSERT INTO access_codes (phone_number, login_code, valid_until, sms_sent, used_login_code)
        VALUES (@telefon, @kod, @wygasa, 0, 0)";
    using (var cmd = new MySqlCommand(insertSql, db))
    {
        cmd.Parameters.AddWithValue("@telefon", numerTelefonu);
        cmd.Parameters.AddWithValue("@kod", kodLogowania);
        cmd.Parameters.AddWithValue("@wygasa", wygasaDo);
        cmd.ExecuteNonQuery();
    }

    // Wysłanie SMS przez MQTT zamiast HTTP
    try
    {
        string tresc = $"Kod logowania: {kodLogowania}. Wazny do {wygasaDo:HH:mm}.";
        var payload = new
        {
            id = Guid.NewGuid().ToString(),   // unikalne ID
            phone = numerTelefonu,
            msg = tresc
        };

        string jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload);

        await _mqtt.PublishAsync("zamek/sms/send", jsonPayload);

        _logger.LogInformation($"📡 MQTT SMS -> {jsonPayload}");
        TempData["msg"] = $"Wyslano kod logowania.";

        // Zalogowanie do access_logs
        AccessLogService.Log(
            _config,
            "sms",
            "ESP32",
            $"Wysłano SMS z kodem {kodLogowania} na {numerTelefonu}",
            "MQTT",
            "wysylka_sms",
            true,
            "wygeneruj_kod",
            userId
        );
    }
    catch (Exception ex)
    {
        TempData["msg"] = $"Kod zapisany, ale MQTT nie wysłał wiadomości: {ex.Message}";
        _logger.LogError(ex, "Błąd MQTT podczas wysyłania kodu logowania");

        AccessLogService.Log(
            _config,
            "sms",
            "ESP32",
            ex.Message,
            "MQTT",
            "wysylka_sms_error",
            false,
            "wygeneruj_kod",
            userId
        );
    }

    return RedirectToPage();
}

  
        // Klasy pomocnicze dla danych dashboardu       
        public class Zdarzenie
        {
            public string typ { get; set; }
            public string komponent { get; set; }
            public string szczegoly { get; set; }
            public DateTime czas { get; set; }
        }

        public class Powiadomienie
        {
            public string Typ { get; set; } = "";
            public string Kanal { get; set; } = "";
            public string Tresc { get; set; } = "";
            public bool Sukces { get; set; }
            public DateTime Data { get; set; }
        }
    }
}
