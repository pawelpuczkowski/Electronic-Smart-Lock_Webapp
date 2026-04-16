using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Text.Json;

namespace ZamekDoDrzwi.Controllers
{
    [ApiController]
    [Route("api/device-status")] // Endpoint: /api/device-status
    public class DeviceStatusController : ControllerBase
    {
        private readonly IConfiguration _settings;

        public DeviceStatusController(IConfiguration settings)
        {
            _settings = settings;
        }

        [HttpPost]
        public IActionResult Post([FromBody] JsonElement payload)
        {
            try
            {
                // Odczyt danych z JSON-a (status komponentów systemu)
                string doorStatus = payload.TryGetProperty("drzwi", out var d) ? d.GetString() ?? "brak" : "brak";
                string wifiStatus = payload.TryGetProperty("wifi", out var w) ? w.GetString() ?? "brak" : "brak";
                string rfidStatus = payload.TryGetProperty("rfid", out var r) ? r.GetString() ?? "brak" : "brak";
                string gsmStatus = payload.TryGetProperty("gsm", out var g) ? g.GetString() ?? "brak" : "brak";
                string cameraStatus = payload.TryGetProperty("kamera", out var k) ? k.GetString() ?? "brak" : "brak";

                // Połączenie z bazą danych
                using var db = new MySqlConnection(_settings.GetConnectionString("MySql"));
                db.Open();

                // Czyszczenie tabeli – zapisujemy tylko aktualny stan urządzenia
                using (var clearCmd = new MySqlCommand("DELETE FROM device_status", db))
                    clearCmd.ExecuteNonQuery();

                // Wstawienie nowego rekordu z bieżącym statusem
                const string insertQuery = @"
                    INSERT INTO device_status 
                        (status_drzwi, status_polaczenia, status_rfid, status_gsm, status_kamera, data_aktualizacji)
                    VALUES (@door, @wifi, @rfid, @gsm, @camera, NOW())";

                using var insertCmd = new MySqlCommand(insertQuery, db);
                insertCmd.Parameters.AddWithValue("@door", doorStatus);
                insertCmd.Parameters.AddWithValue("@wifi", wifiStatus);
                insertCmd.Parameters.AddWithValue("@rfid", rfidStatus);
                insertCmd.Parameters.AddWithValue("@gsm", gsmStatus);
                insertCmd.Parameters.AddWithValue("@camera", cameraStatus);
                insertCmd.ExecuteNonQuery();

                // Odpowiedź JSON zwracana do ESP lub aplikacji webowej
                return new JsonResult(new { success = true, message = "Status zapisany pomyślnie" });
            }
            catch (JsonException)
            {
                // Błąd w formacie JSON – np. brakujące lub błędne pola
                return BadRequest(new { success = false, message = "Nieprawidłowe dane JSON" });
            }
            catch (Exception error)
            {
                // Inny błąd (np. baza danych, połączenie itp.)
                return StatusCode(500, new { success = false, message = $"Błąd zapisu: {error.Message}" });
            }
        }
    }
}
