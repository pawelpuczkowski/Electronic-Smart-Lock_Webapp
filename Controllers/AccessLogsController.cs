using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using System.Text.Json;

namespace ZamekDoDrzwi.Controllers
{
    [ApiController]
    [Route("api/accesslogs")] // Endpoint: /api/accesslogs
    public class AccessLogsController : ControllerBase
    {
        private readonly IConfiguration _settings;
        private readonly IWebHostEnvironment _environment;

        public AccessLogsController(IConfiguration settings, IWebHostEnvironment environment)
        {
            _settings = settings;
            _environment = environment;
        }

        [HttpPost]
        public IActionResult Post([FromBody] JsonElement payload)
        {
            try
            {
                // Nawiązanie połączenia z bazą danych MySQL
                using var db = new MySqlConnection(_settings.GetConnectionString("MySql"));
                db.Open();

                // Zapytanie SQL – wstawienie rekordu logu zdarzenia
                const string insertQuery = @"
                    INSERT INTO access_logs 
                        (typ_zdarzenia, komponent, szczegoly, metoda, powod, sukces, akcja)
                    VALUES (@typ, @komponent, @szczegoly, @metoda, @powod, @sukces, @akcja)
                ";

                using var insertCmd = new MySqlCommand(insertQuery, db);

                // Pobranie wartości z JSON-a przekazanego w żądaniu
                insertCmd.Parameters.AddWithValue("@typ", payload.GetProperty("typ_zdarzenia").GetString());
                insertCmd.Parameters.AddWithValue("@komponent", payload.GetProperty("komponent").GetString());
                insertCmd.Parameters.AddWithValue("@szczegoly", payload.GetProperty("szczegoly").GetString());
                insertCmd.Parameters.AddWithValue("@metoda", payload.GetProperty("metoda").GetString());
                insertCmd.Parameters.AddWithValue("@powod", payload.GetProperty("powod").GetString());
                insertCmd.Parameters.AddWithValue("@sukces", payload.GetProperty("sukces").GetInt32());
                insertCmd.Parameters.AddWithValue("@akcja", payload.GetProperty("akcja").GetString());

                // Wykonanie zapytania
                insertCmd.ExecuteNonQuery();
               
                return Ok(new { status = "ok" });
            }
            catch (Exception error)
            {
                // W razie błędu zwracamy kod 500 + opis błędu (JSON)
                return StatusCode(500, new { error = error.Message });
            }
        }
    }
}
