using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;

namespace ZamekDoDrzwi.Pages.Logi
{
    public class PrzegladModel : AdminPageModel
    {
        private readonly IConfiguration _config;
        public PrzegladModel(IConfiguration config) => _config = config;

        // Lista logów przekazywana do widoku
        public List<LogEntry> Logi { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? Szukaj { get; set; }

        // Model pojedynczego logu
        public class LogEntry
        {
            public DateTime Czas { get; set; }
            public string Szczegoly { get; set; } = "";
            public bool Sukces { get; set; }
        }

        // Pobierz listy logów z bazy
        public void OnGet()
        {
            try
            {
                using var db = new MySqlConnection(_config.GetConnectionString("MySql"));
                db.Open();

                // Bazowe zapytanie (maks. 100 rekord�w)
                string dbQuery = @"
                    SELECT czas, szczegoly, COALESCE(sukces, 1) AS sukces
                    FROM access_logs
                    WHERE 1=1";

                var dbParams = new List<MySqlParameter>();

                // Jeśli użytkownik wpisał tekst w wyszukiwarce
                if (!string.IsNullOrWhiteSpace(Szukaj))
                {
                    dbQuery += " AND szczegoly LIKE @szukaj";
                    dbParams.Add(new MySqlParameter("@szukaj", $"%{Szukaj}%"));
                }

                dbQuery += " ORDER BY czas DESC LIMIT 100";

                using var dbCmd = new MySqlCommand(dbQuery, db);
                dbCmd.Parameters.AddRange(dbParams.ToArray());

                using var dbReader = dbCmd.ExecuteReader();
                while (dbReader.Read())
                {
                    Logi.Add(new LogEntry
                    {
                        Czas = dbReader.GetDateTime("czas"),
                        Szczegoly = dbReader.GetString("szczegoly"),
                        Sukces = dbReader.GetBoolean("sukces")
                    });
                }
            }
            catch (Exception ex)
            {
                // W razie błędu logujemy go w konsoli serwera
                Console.WriteLine($"Błąd podczas pobierania logów: {ex.Message}");
            }
        }
    }
}
