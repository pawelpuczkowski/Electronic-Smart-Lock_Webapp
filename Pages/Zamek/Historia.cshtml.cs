using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;
using System.Data;

namespace ZamekDoDrzwi.Pages.Zamek
{   
    public class HistoriaModel : AdminPageModel
    {
        private readonly IConfiguration _config;

        public HistoriaModel(IConfiguration config)
        {
            _config = config;
        }

        // Struktura pojedynczego wpisu historii
        public class OtwarcieEntry
        {
            public DateTime Data { get; set; }           // Data zdarzenia
            public string Szczegoly { get; set; } = "";  // Opis szczeglowy (np. kto otworzył zamek)
            public bool Sukces { get; set; }              // Czy otwarcie powiodło się
        }

        // Lista zdarzeń wyświetlana w tabeli
        public List<OtwarcieEntry> Historia { get; set; } = new();
       
        // Ładowanie historii z bazy MySQL     
        public void OnGet()
        {
            string connStr = _config.GetConnectionString("MySql");

            try
            {
                using var db = new MySqlConnection(connStr);
                db.Open();

                // Pobieramy ostatnie 100 zdarzeń z access_logs
                string sql = @"
                    SELECT 
                        al.czas,
                        al.szczegoly,
                        al.sukces
                    FROM access_logs al
                    WHERE 
                        al.typ_zdarzenia IN ('zamek', 'dostep')
                    ORDER BY al.czas DESC
                    LIMIT 100";

                using var cmd = new MySqlCommand(sql, db);
                using var reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    var entry = new OtwarcieEntry
                    {
                        Data = reader.IsDBNull("czas") ? DateTime.MinValue : reader.GetDateTime("czas"),
                        Szczegoly = reader["szczegoly"]?.ToString() ?? "[brak danych]",
                        Sukces = reader["sukces"] != DBNull.Value && Convert.ToBoolean(reader["sukces"])
                    };

                    Historia.Add(entry);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd podczas ładowania historii otwarć: {ex.Message}");
            }
        }
    }
}
