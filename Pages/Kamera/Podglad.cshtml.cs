using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.IO;

namespace ZamekDoDrzwi.Pages.Kamera
{
    public class PodgladModel : AdminPageModel
    {
        private readonly IConfiguration _settings;
        private readonly IWebHostEnvironment _env; // dodane

        public PodgladModel(IConfiguration settings, IWebHostEnvironment env) // poprawiony konstruktor
        {
            _settings = settings;
            _env = env;
        }

        // Lista zdjęć wyświetlanych w widoku
        public List<ZdjecieInfo> Zdjecia { get; set; } = new();

        // Klasa pomocnicza do przechowywania danych zdjęcia
        public class ZdjecieInfo
        {
            public string Url { get; set; } = string.Empty;
            public DateTime DataUtworzenia { get; set; }
        }

        // Główne ładowanie strony
        public void OnGet()
        {
            try
            {
                string connStr = _settings.GetConnectionString("MySql");
                using var db = new MySqlConnection(connStr);
                db.Open();

                // Dynamiczny URL do hosta, np. https://zamekdodrzwi.pl/uploads/
                string baseUrl = $"{Request.Scheme}://{Request.Host}/uploads/";

                const string selectQuery = @"
                    SELECT id, plik_nazwa, data_utworzenia 
                    FROM camera_photos 
                    ORDER BY data_utworzenia DESC";

                using var selectCmd = new MySqlCommand(selectQuery, db);
                using var reader = selectCmd.ExecuteReader();

                while (reader.Read())
                {
                    string nazwaPliku = reader.GetString("plik_nazwa");
                    DateTime createdAt = reader.GetDateTime("data_utworzenia");

                    Zdjecia.Add(new ZdjecieInfo
                    {
                        Url = baseUrl + nazwaPliku,
                        DataUtworzenia = createdAt
                    });
                }

                reader.Close();

                // Usuwanie zdjęć starszych niż 14 dni
                DateTime cutoffDate = DateTime.Now.AddDays(-14);
                var toDelete = new List<(int id, string nazwaPliku)>();

                const string oldQuery = @"
                    SELECT id, plik_nazwa 
                    FROM camera_photos 
                    WHERE data_utworzenia < @cutoff";

                using var oldCmd = new MySqlCommand(oldQuery, db);
                oldCmd.Parameters.AddWithValue("@cutoff", cutoffDate);

                using (var oldReader = oldCmd.ExecuteReader())
                {
                    while (oldReader.Read())
                    {
                        int id = oldReader.GetInt32("id");
                        string nazwaPliku = oldReader.GetString("plik_nazwa");
                        toDelete.Add((id, nazwaPliku));
                    }
                }

                // Kasowanie plików i rekordów
                foreach (var (id, nazwaPliku) in toDelete)
                {
                    try
                    {
                        string uploadsDir = Path.Combine(_env.ContentRootPath, "uploads");
                        string fullPath = Path.Combine(uploadsDir, nazwaPliku);

                        if (System.IO.File.Exists(fullPath))
                        {
                            System.IO.File.Delete(fullPath);
                            Console.WriteLine($"Usunięto stare zdjęcie: {nazwaPliku}");
                        }

                        using var deleteCmd = new MySqlCommand(
                            "DELETE FROM camera_photos WHERE id = @id", db);
                        deleteCmd.Parameters.AddWithValue("@id", id);
                        deleteCmd.ExecuteNonQuery();
                    }
                    catch (Exception err)
                    {
                        Console.WriteLine($"Błąd przy usuwaniu {nazwaPliku}: {err.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd w OnGet(): {ex.Message}");
            }
        }

        // Obsługa pobierania zdjęcia
        public IActionResult OnGetPobierz(string nazwa)
        {
            if (string.IsNullOrWhiteSpace(nazwa))
                return NotFound("Nie podano nazwy pliku.");

            try
            {
                string uploadsDir = Path.Combine(_env.ContentRootPath, "uploads");
                string fullPath = Path.Combine(uploadsDir, nazwa);

                if (!System.IO.File.Exists(fullPath))
                    return NotFound($"Plik {nazwa} nie istnieje.");

                var fileBytes = System.IO.File.ReadAllBytes(fullPath);
                return File(fileBytes, "image/jpeg", nazwa);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas pobierania pliku: {ex.Message}");
                return StatusCode(500, "Błąd serwera podczas pobierania pliku.");
            }
        }
    }
}
