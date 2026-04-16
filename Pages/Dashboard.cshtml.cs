using ZamekDoDrzwi.Services;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
using System.Collections.Generic;
using System.Data;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ZamekDoDrzwi.Services;
namespace ZamekDoDrzwi.Pages
{
    public class DashboardModel : AdminPageModel
    {
        private readonly IConfiguration _config;
         private readonly ILogger<DashboardModel> _logger;
         private readonly MqttWorker _mqtt;
        public DashboardModel(IConfiguration config, ILogger<DashboardModel> logger, MqttWorker mqtt)
        {
            _config = config;
            _logger = logger;
            _mqtt = mqtt;
        }

        // Widok 
        public string StatusDrzwi { get; set; } = "Brak danych";       
        public string StatusPolaczenia { get; set; } = "Brak danych";
        public string StatusRFID { get; set; } = "Brak danych";
        public string StatusGSM { get; set; } = "Brak danych";
        public string StatusKamera { get; set; } = "Brak danych";
        public List<LogZdarzenia> Logi { get; set; } = new();
        public List<string> OstatnieZdjecia { get; set; } = new();
        public List<string> LabelList => LiczbaZdarzenDziennie.Keys.Reverse().ToList();
        public List<int> ValueList => LiczbaZdarzenDziennie.Values.Reverse().ToList();
        public string ValuesJson => JsonSerializer.Serialize(LiczbaZdarzenDziennie.Values.Reverse());
        public List<string> BledyLabels { get; set; } = new();
        public List<int> BledyValues { get; set; } = new();

        public List<string> KomponentyLabels { get; set; } = new();
        public List<int> KomponentyValues { get; set; } = new();
        public Dictionary<string, int> ZdjeciaDziennie { get; set; } = new();
        public List<string> ZdjeciaLabels => ZdjeciaDziennie.Keys.Reverse().ToList();
        public List<int> ZdjeciaValues => ZdjeciaDziennie.Values.Reverse().ToList();
        public Dictionary<string, int> TypyZdarzen { get; set; } = new();
        public List<string> TypyLabels => TypyZdarzen.Keys.ToList();
        public List<int> TypyValues => TypyZdarzen.Values.ToList();
        public Dictionary<string, int> ZdarzeniaGodziny { get; set; } = new();
        public List<string> GodzinyLabels => ZdarzeniaGodziny.Keys.ToList();
        public List<int> GodzinyValues => ZdarzeniaGodziny.Values.ToList();
        [TempData]
        public string? KomunikatKod { get; set; }

        public IActionResult OnGet()
        {
            if (HttpContext.Session.GetInt32("UserId") == null)
                return RedirectToPage("/Index");

            //Wczytaj dane z bazy
            WczytajStatusSystemu();
            WczytajLogi();
            WczytajZdjecia();
            WczytajLiczbeZdarzenDziennie();
            WczytajBledy();
            WczytajKomponenty();
            WczytajZdjeciaDziennie();
            WczytajTypyZdarzen();
            WczytajZdarzeniaWGodzinach();
            return Page();
        }

        //Wczytaj status systemu
        private void WczytajStatusSystemu()
        {
            string connStr = _config.GetConnectionString("MySql");
            using var db = new MySqlConnection(connStr);
            db.Open();

            string sql = "SELECT * FROM device_status ORDER BY data_aktualizacji DESC LIMIT 1";
            using var cmd = new MySqlCommand(sql, db);
            using var reader = cmd.ExecuteReader();

            if (reader.Read())
            {
                DateTime dataAktualizacji = reader.GetDateTime("data_aktualizacji");

                // Sprawdzenie czy dane są starsze niż 5 minut
                bool stareDane = DateTime.Now - dataAktualizacji > TimeSpan.FromMinutes(5);

                if (stareDane)
                {
                    StatusDrzwi = "Brak danych";
                    StatusPolaczenia = "Brak danych";
                    StatusRFID = "Brak danych";
                    StatusGSM = "Brak danych";
                    StatusKamera = "Brak danych";
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
            else
            {
                // Brak jakichkolwiek danych w tabeli
                StatusDrzwi = "Brak danych";
                StatusPolaczenia = "Brak danych";
                StatusRFID = "Brak danych";
                StatusGSM = "Brak danych";
                StatusKamera = "Brak danych";
            }
        }

        // Wczytaj logi
        private void WczytajLogi()
        {
            string connStr = _config.GetConnectionString("MySql");
            using var db = new MySqlConnection(connStr);
            db.Open();

            string sql = "SELECT czas, szczegoly FROM access_logs ORDER BY id DESC LIMIT 5";
            using var cmd = new MySqlCommand(sql, db);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                string czas = reader.IsDBNull("czas") ? "[brak daty]" : reader.GetDateTime("czas").ToString("yyyy-MM-dd HH:mm");
                string szczegoly = reader.IsDBNull("szczegoly") ? "[brak danych]" : reader.GetString("szczegoly");

                Logi.Add(new LogZdarzenia
                {
                    Godzina = czas,
                    Tresc = szczegoly
                });
            }
        }

        //Wczytaj ostatnie zdjęcia
        private void WczytajZdjecia()
{
    try
    {
        string connStr = _config.GetConnectionString("MySql");
        using var db = new MySqlConnection(connStr);
        db.Open();

        // Pobieramy tylko nazwy plików, nie pełne ścieżki
        string sql = "SELECT plik_nazwa FROM camera_photos ORDER BY id DESC LIMIT 3";
        using var cmd = new MySqlCommand(sql, db);
        using var reader = cmd.ExecuteReader();

        // Pobranie aktualnego hosta i protokołu (HTTP/HTTPS)
        string bazaUrl = $"{Request.Scheme}://{Request.Host}/uploads/";

        while (reader.Read())
        {
            string nazwaPliku = reader.GetString("plik_nazwa");

            // Sklejamy pełny URL: https://zamekdodrzwi.pl/uploads/photo_1739990123.jpg
            string pelnyUrl = bazaUrl + nazwaPliku;
            OstatnieZdjecia.Add(pelnyUrl);
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"łąd podczas wczytywania zdjęć: {ex.Message}");
    }
}


        public class LogZdarzenia
        {
            public string Godzina { get; set; }
            public string Tresc { get; set; }
        }
        public Dictionary<string, int> LiczbaZdarzenDziennie { get; set; } = new();      
       
        
        private void WczytajLiczbeZdarzenDziennie()
        {
            string connStr = _config.GetConnectionString("MySql");
            using var db = new MySqlConnection(connStr);
            db.Open();

            string sql = @"
        SELECT DATE(czas) AS dzien, COUNT(*) AS liczba
        FROM access_logs
        GROUP BY dzien
        ORDER BY dzien DESC
        LIMIT 7";

            using var cmd = new MySqlCommand(sql, db);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                string dzien = reader.GetDateTime("dzien").ToString("yyyy-MM-dd");
                int liczba = reader.GetInt32("liczba");
                LiczbaZdarzenDziennie[dzien] = liczba;
            }

        }
        private void WczytajBledy()
        {
            string connStr = _config.GetConnectionString("MySql");
            using var db = new MySqlConnection(connStr);
            db.Open();

            string sql = @"
        SELECT DATE(czas) AS dzien, COUNT(*) AS liczba
        FROM access_logs
        WHERE sukces = 0
        GROUP BY dzien
        ORDER BY dzien DESC
        LIMIT 7";

            using var cmd = new MySqlCommand(sql, db);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                string dzien = reader.GetDateTime("dzien").ToString("yyyy-MM-dd");
                int liczba = reader.GetInt32("liczba");
                BledyLabels.Add(dzien);
                BledyValues.Add(liczba);
            }
        }
        private void WczytajKomponenty()
        {
            string connStr = _config.GetConnectionString("MySql");
            using var db = new MySqlConnection(connStr);
            db.Open();

            string sql = @"
        SELECT komponent, COUNT(*) AS liczba
        FROM access_logs
        GROUP BY komponent
        ORDER BY liczba DESC
        LIMIT 7";

            using var cmd = new MySqlCommand(sql, db);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                string komponent = reader.GetString("komponent");
                int liczba = reader.GetInt32("liczba");
                KomponentyLabels.Add(komponent);
                KomponentyValues.Add(liczba);
            }
        }
        private void WczytajZdjeciaDziennie()
        {
            string connStr = _config.GetConnectionString("MySql");
            using var db = new MySqlConnection(connStr);
            db.Open();

            string sql = @"
        SELECT DATE(data_utworzenia) AS dzien, COUNT(*) AS liczba
        FROM camera_photos
        GROUP BY dzien
        ORDER BY dzien DESC
        LIMIT 7";

            using var cmd = new MySqlCommand(sql, db);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                string dzien = reader.GetDateTime("dzien").ToString("yyyy-MM-dd");
                int liczba = reader.GetInt32("liczba");
                ZdjeciaDziennie[dzien] = liczba;
            }
        }
        private void WczytajTypyZdarzen()
        {
            string connStr = _config.GetConnectionString("MySql");
            using var db = new MySqlConnection(connStr);
            db.Open();

            string sql = @"
        SELECT typ_zdarzenia, COUNT(*) AS liczba
        FROM access_logs
        GROUP BY typ_zdarzenia";

            using var cmd = new MySqlCommand(sql, db);
            using var reader = cmd.ExecuteReader();

            //  Mapowanie nazw technicznych z wyrkesów
            var mapa = new Dictionary<string, string>
    {
        { "dostep", "Dostęp" },
        { "drzwi", "Drzwi" },
        { "dzwonek", "Dzwonek" },
        { "kamera", "Kamera" },
        { "one_time_fail", "Kod jednorazowy – błędny" },
        { "one_time_ok", "Kod jednorazowy – poprawny" },
        { "pin_blad", "PIN – błędny" },
        { "rfid_zla_err", "RFID – błąd karty" },
        { "rfid_2fa_err", "RFID – nieprawidłowa" },
        { "rfid_2fa_ok", "RFID – poprawna" },
        { "rfid_2fa_sms_ok", "RFID + SMS – poprawne" },
        { "ruch", "Ruch wykryty" },
        { "sms", "Wiadomość SMS" },
        { "zamek", "Zamek drzwi" },
        { "esp_blad", "Błąd połączenia z ESP32" },
        { "esp_timeout", "Przekroczony czas ESP" },
        { "esp_exception", "Błąd komunikacji ESP" },
    };

            while (reader.Read())
            {
                string typ = reader.GetString("typ_zdarzenia");
                int liczba = reader.GetInt32("liczba");

                // Jeśli nie znaleziono mapowania, zostaw oryginał
                string etykieta = mapa.ContainsKey(typ) ? mapa[typ] : typ;

                TypyZdarzen[etykieta] = liczba;
            }
        }

        private void WczytajZdarzeniaWGodzinach()
        {
            string connStr = _config.GetConnectionString("MySql");
            using var db = new MySqlConnection(connStr);
            db.Open();

            string sql = @"
        SELECT HOUR(czas) AS godzina, COUNT(*) AS liczba
        FROM access_logs
        GROUP BY godzina
        ORDER BY godzina";

            using var cmd = new MySqlCommand(sql, db);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                string godzina = reader.GetInt32("godzina").ToString("00") + ":00";
                int liczba = reader.GetInt32("liczba");
                ZdarzeniaGodziny[godzina] = liczba;
            }
        }    

        public IActionResult OnGetOstatnieZdjecieJson()
{
    try
    {
        string connStr = _config.GetConnectionString("MySql");
        using var db = new MySqlConnection(connStr);
        db.Open();

        // Pobieramy tylko nazwę pliku (nie pełną ścieżkę)
        string sql = "SELECT plik_nazwa FROM camera_photos ORDER BY id DESC LIMIT 1";
        using var cmd = new MySqlCommand(sql, db);
        using var reader = cmd.ExecuteReader();

        if (reader.Read())
        {
            string nazwaPliku = reader.GetString("plik_nazwa");

            // Automatycznie używa HTTP/HTTPS oraz aktualnego hosta
            string bazaUrl = $"{Request.Scheme}://{Request.Host}/uploads/";
            string pelnyUrl = bazaUrl + nazwaPliku;

            return new JsonResult(new
            {
                url = pelnyUrl,
                fileName = nazwaPliku
            });
        }

        // Jeśli brak zdjęć w bazie
        return new JsonResult(new
        {
            url = (string?)null,
            fileName = (string?)null
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Błąd podczas pobierania zdjęcia: {ex.Message}");
        Response.StatusCode = 500;
        return new JsonResult(new { error = "Błąd serwera przy pobieraniu zdjęcia." });
    }
}

        public async Task<IActionResult> OnPostAsync()
{
    // 1) Dane z formularza
    string numerTelefonu = (Request.Form["telefon"].ToString() ?? "").Trim();
    string czasTekst = (Request.Form["czas"].ToString() ?? "").Trim();

    if (string.IsNullOrWhiteSpace(numerTelefonu) || string.IsNullOrWhiteSpace(czasTekst))
    {
        TempData["KomunikatKod"] = "Wprowadź numer telefonu i czas ważności.";
        return RedirectToPage();
    }

    // 2) Normalizacja telefonu do +48XXXXXXXXX
    numerTelefonu = Regex.Replace(numerTelefonu, @"[\s\-]", "");
    if (numerTelefonu.StartsWith("0") && numerTelefonu.Length == 10)
        numerTelefonu = "+48" + numerTelefonu[1..];
    else if (numerTelefonu.Length == 9)
        numerTelefonu = "+48" + numerTelefonu;

    const string PL = @"^\+48\d{9}$";
    if (!Regex.IsMatch(numerTelefonu, PL))
    {
        TempData["KomunikatKod"] = "Numer telefonu musi być w formacie +48XXXXXXXXX.";
        return RedirectToPage();
    }

    // 3) Czas ważności
    if (!DateTime.TryParse(czasTekst, out DateTime wygasaDo) || wygasaDo <= DateTime.Now)
    {
        TempData["KomunikatKod"] = "Podaj poprawny czas ważności (w przyszłości).";
        return RedirectToPage();
    }

    // 4) Kod 6-cyfrowy
    string kodLogowania = new Random().Next(100000, 999999).ToString();

    // 5) DB: zapisz nowy kod
    using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
    await db.OpenAsync();

    const string insertSql = @"
INSERT INTO access_codes
  (phone_number, login_code, valid_until, sms_sent, used_login_code, max_uses, used_count)
VALUES
  (@telefon, @kod, @wygasa, 0, 0, NULL, 0)";
    using (var cmd = new MySqlCommand(insertSql, db))
    {
        cmd.Parameters.AddWithValue("@telefon", numerTelefonu);
        cmd.Parameters.AddWithValue("@kod", kodLogowania);
        cmd.Parameters.AddWithValue("@wygasa", wygasaDo);
        await cmd.ExecuteNonQueryAsync();
    }

    // 6) Wysłanie SMS przez MQTT zamiast HTTP
    try
    {
        var payload = new
        {
            phone = numerTelefonu,
            code = kodLogowania,
            valid_until = wygasaDo.ToString("yyyy-MM-dd HH:mm:ss")
        };

        string jsonPayload = System.Text.Json.JsonSerializer.Serialize(payload);

        // Publikacja do ESP – nowy temat: zamek/sms/send
        await _mqtt.PublishAsync("zamek/sms/send", jsonPayload);

        _logger.LogInformation($"Wysłano MQTT -> zamek/sms/send: {jsonPayload}");
        TempData["KomunikatKod"] = $"Kod {kodLogowania} został wysłany na {numerTelefonu}.";
    }
    catch (Exception ex)
    {
        TempData["KomunikatKod"] = $"Kod zapisany (DB), ale MQTT nie wysłał wiadomości: {ex.Message}";
        _logger.LogError(ex, "Błąd podczas wysyłania komendy MQTT z kodem SMS.");
    }

    return RedirectToPage();
}


        public List<Powiadomienie> Powiadomienia { get; set; } = new();

        private void WczytajPowiadomienia()
        {
            string connStr = _config.GetConnectionString("MySql");
            using var db = new MySqlConnection(connStr);
            db.Open();

            string sql = @"SELECT event_type, channel, message, success, created_at 
                   FROM esp_notifications
                   ORDER BY created_at DESC
                   LIMIT 5";
            using var cmd = new MySqlCommand(sql, db);
            using var rdr = cmd.ExecuteReader();
            while (rdr.Read())
            {
                Powiadomienia.Add(new Powiadomienie
                {
                    Typ = rdr.GetString("event_type"),
                    Kanal = rdr.GetString("channel"),
                    Tresc = rdr.GetString("message"),
                    Sukces = rdr.GetBoolean("success"),
                    Data = rdr.GetDateTime("created_at")
                });
            }
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
