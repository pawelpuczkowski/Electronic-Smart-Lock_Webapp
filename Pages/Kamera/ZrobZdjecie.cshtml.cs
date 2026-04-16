using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
using ZamekDoDrzwi.Services;

namespace ZamekDoDrzwi.Pages.Kamera
{
    public class ZrobZdjecieModel : AdminPageModel
    {
        private readonly IConfiguration _config;
        private readonly ILogger<ZrobZdjecieModel> _logger;
        private readonly MqttWorker _mqtt;

        public string? OstatnieZdjecieUrl { get; set; }

        [TempData]
        public string? KomunikatKamera { get; set; }

        public ZrobZdjecieModel(IConfiguration config, ILogger<ZrobZdjecieModel> logger, MqttWorker mqtt)
        {
            _config = config;
            _logger = logger;
            _mqtt = mqtt;
        }

        // Pobiera ostatnie zdjęcie z bazy i buduje pełny URL
        private void WczytajOstatnieZdjecie()
        {
            try
            {
                string connStr = _config.GetConnectionString("MySql");
                using var db = new MySqlConnection(connStr);
                db.Open();

                // Pobieramy nazwę pliku zamiast pełnej ścieżki
                string sql = "SELECT plik_nazwa FROM camera_photos ORDER BY id DESC LIMIT 1";
                using var cmd = new MySqlCommand(sql, db);
                var nazwaPliku = cmd.ExecuteScalar()?.ToString();

                if (!string.IsNullOrEmpty(nazwaPliku))
                {
                    string bazaUrl = $"{Request.Scheme}://{Request.Host}/uploads/";
                    OstatnieZdjecieUrl = bazaUrl + nazwaPliku;
                    _logger.LogInformation($"Załadowano zdjęcie: {OstatnieZdjecieUrl}");
                }
                else
                {
                    _logger.LogWarning("Brak zdjęć w bazie danych.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wczytywania zdjęcia.");
            }
        }

        public void OnGet()
        {
            WczytajOstatnieZdjecie();
        }

        //Wysyła komendę do ESP przez MQTT i ładuje nowe zdjęcie
        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                await _mqtt.PublishAsync("zamek/cmd", "photo");
                _logger.LogInformation("Komenda MQTT 'photo' wysłana.");
                KomunikatKamera = "Wysłano polecenie wykonania zdjęcia.";

                AccessLogService.Log(
                    _config,
                    "kamera",
                    "remote",
                    "Wysłano komendę MQTT: photo",
                    "MQTT",
                    "Zdalne wykonanie zdjęcia",
                    true,
                    "zrob_zdjecie",
                    HttpContext.Session.GetInt32("UserId")
                );

                // Czekamy chwilę na przetworzenie zdjęcia (ESP -> serwer -> baza)
                await Task.Delay(4000);

                // Próba wczytania nowego zdjęcia
                WczytajOstatnieZdjecie();

                if (OstatnieZdjecieUrl != null)
                {
                    KomunikatKamera += "Zdjęcie zapisane.";
                }
                else
                {
                    KomunikatKamera += "Brak nowego zdjęcia (urządzenie mogło nie odpowiedzieć).";
                }
            }
            catch (Exception ex)
            {
                KomunikatKamera = $"Błąd podczas wykonywania zdjęcia: {ex.Message}";
                _logger.LogError(ex, "Błąd MQTT podczas wykonywania zdjęcia.");

                AccessLogService.Log(
                    _config,
                    "kamera",
                    "remote",
                    ex.Message,
                    "MQTT",
                    "Błąd podczas publikacji MQTT",
                    false,
                    "zrob_zdjecie",
                    HttpContext.Session.GetInt32("UserId")
                );
            }

            return RedirectToPage();
        }
    }
}
