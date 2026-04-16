using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
using System.Net.Http;
using System.Net.Http.Headers;

namespace ZamekDoDrzwi.Pages.Zamek
{
    public class OtworzModel : AdminPageModel
    {
        private readonly IConfiguration _config;
        private readonly ILogger<OtworzModel> _logger;
        private readonly MqttWorker _mqtt;

        public OtworzModel(IConfiguration config, ILogger<OtworzModel> logger, MqttWorker mqtt)
        {
            _config = config;
            _logger = logger;
            _mqtt = mqtt;
        }

        [TempData] public string? KomunikatZamek { get; set; }
        [TempData] public bool Sukces { get; set; }
        public string OstatnieOtwarcie { get; set; } = "brak danych";

        public void OnGet()
        {
            try
            {
                using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
                db.Open();
                var cmd = new MySqlCommand(@"
                    SELECT czas FROM access_logs
                    WHERE typ_zdarzenia = 'zamek' AND sukces = 1
                    ORDER BY czas DESC LIMIT 1", db);
                var result = cmd.ExecuteScalar();
                OstatnieOtwarcie = result != null
                    ? Convert.ToDateTime(result).ToString("yyyy-MM-dd HH:mm:ss")
                    : "brak danych";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd pobierania ostatniego otwarcia zamka.");
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            try
            {
                string topic = "zamek/cmd";
                string payload = "open";
                await _mqtt.PublishAsync(topic, payload);

                KomunikatZamek = "Polecenie otwarcia wysłane przez MQTT.";
                Sukces = true;

                Services.AccessLogService.Log(
                    _config,
                    "zamek",
                    "remote",
                    "Wysłano komendę MQTT: open",
                    "MQTT",
                    "Zdalne otwarcie zamka",
                    true,
                    "otwarcie_zdalne",
                    HttpContext.Session.GetInt32("UserId")
                );
            }
            catch (Exception ex)
            {
                KomunikatZamek = $"Błąd MQTT: {ex.Message}";
                Sukces = false;

                Services.AccessLogService.Log(
                    _config,
                    "zamek",
                    "remote",
                    ex.Message,
                    "MQTT",
                    "Błąd podczas publikacji MQTT",
                    false,
                    "otwarcie_zdalne",
                    HttpContext.Session.GetInt32("UserId")
                );
            }

            return RedirectToPage();
        }
    }
}
