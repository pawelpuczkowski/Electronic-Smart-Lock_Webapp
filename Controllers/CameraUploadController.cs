using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;

namespace ZamekDoDrzwi.Controllers
{
    [ApiController]
    [Route("api/upload-photo")] // Endpoint: /api/upload-photo
    public class CameraUploadController : ControllerBase
    {
        private readonly IConfiguration _settings;
        private readonly IWebHostEnvironment _environment;

        public CameraUploadController(IConfiguration settings, IWebHostEnvironment environment)
        {
            _settings = settings;
            _environment = environment;
        }

        [HttpPost]
        public async Task<IActionResult> Post()
        {
            try
            {
                // Odczyt danych binarnych (zdjęcia) z żądania HTTP
                using var buffer = new MemoryStream();
                await Request.Body.CopyToAsync(buffer);
                var photoBytes = buffer.ToArray();

                // Walidacja – odrzuć zbyt małe pliki (np. puste żądania)
                if (photoBytes.Length < 1000)
                {
                    Response.StatusCode = 400;
                    return Content("Za mało danych – plik prawdopodobnie niepoprawny.");
                }

                // Przygotowanie katalogu docelowego na serwerze
                string uploadDir = Path.Combine(_environment.ContentRootPath, "uploads");
                if (!Directory.Exists(uploadDir))
                    Directory.CreateDirectory(uploadDir);

                // Tworzenie unikalnej nazwy pliku (timestamp w sekundach)
                string fileName = $"photo_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}.jpg";
                string fullPath = Path.Combine(uploadDir, fileName);

                // Zapis zdjęcia na dysku
                await System.IO.File.WriteAllBytesAsync(fullPath, photoBytes);
                Console.WriteLine($"📷 Zapisano zdjęcie: {fileName}");

                // Zapis informacji o pliku do bazy danych
                using var db = new MySqlConnection(_settings.GetConnectionString("MySql"));
                await db.OpenAsync();

                const string insertQuery = @"
                    INSERT INTO camera_photos (plik_nazwa, plik_sciezka)
                    VALUES (@name, @path)";

                using var insertCmd = new MySqlCommand(insertQuery, db);
                insertCmd.Parameters.AddWithValue("@name", fileName);
                insertCmd.Parameters.AddWithValue("@path", fullPath);
                await insertCmd.ExecuteNonQueryAsync();

                // Zwrócenie potwierdzenia
                return Content("Zdjęcie zapisane i zalogowane.");
            }
            catch (Exception error)
            {
                // W razie błędu – zwróć opis (dla logów serwera lub debugowania)
                Response.StatusCode = 500;
                return Content($"Błąd podczas zapisu zdjęcia: {error.Message}");
            }
        }
    }
}
