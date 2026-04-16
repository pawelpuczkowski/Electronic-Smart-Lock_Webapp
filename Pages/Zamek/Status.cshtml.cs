using Microsoft.AspNetCore.Mvc.RazorPages;
using MySql.Data.MySqlClient;

namespace ZamekDoDrzwi.Pages.Zamek;

public class StatusModel : AdminPageModel
{
    private readonly IConfiguration _config;

    public StatusModel(IConfiguration config) => _config = config;

    // Pola prezentowane w widoku 
    public string StatusDrzwi { get; set; } = "Brak danych";
    public string StatusPolaczenia { get; set; } = "Brak danych";
    public string StatusRFID { get; set; } = "Brak danych";
    public string StatusGSM { get; set; } = "Brak danych";
    public string StatusKamera { get; set; } = "Brak danych";

    public DateTime DataAktualizacji { get; set; } = DateTime.MinValue;
    public bool CzyStareDane { get; set; } = true; // true, gdy dane starsze niż 5 minut
        
    // OnGet() - wczytuje ostatni zapis statusu urządzenia z bazy MySQL  
    public void OnGet()
    {
        string connStr = _config.GetConnectionString("MySql");
        using var db = new MySqlConnection(connStr);
        db.Open();

        const string sql = @"
            SELECT status_drzwi, status_polaczenia, status_rfid,
                   status_gsm, status_kamera, data_aktualizacji
            FROM device_status
            ORDER BY data_aktualizacji DESC
            LIMIT 1";

        using var cmd = new MySqlCommand(sql, db);
        using var reader = cmd.ExecuteReader();

        // Brak danych w tabeli ? domyślny komunikat
        if (!reader.Read())
        {
            CzyStareDane = true;
            return;
        }

        // Pobranie daty aktualizacji
        DataAktualizacji = reader.GetDateTime("data_aktualizacji");
        CzyStareDane = (DateTime.Now - DataAktualizacji) > TimeSpan.FromMinutes(5);

        // Jeśli dane są starsze niż 5 minut ? uznaj jako nieaktualne
        if (CzyStareDane)
        {
            StatusDrzwi = "Brak danych";
            StatusPolaczenia = "Brak danych";
            StatusRFID = "Brak danych";
            StatusGSM = "Brak danych";
            StatusKamera = "Brak danych";
        }
        else
        {
            // Świeże dane - przypisujemy bieżące wartości
            StatusDrzwi = reader["status_drzwi"]?.ToString() ?? "Brak danych";
            StatusPolaczenia = reader["status_polaczenia"]?.ToString() ?? "Brak danych";
            StatusRFID = reader["status_rfid"]?.ToString() ?? "Brak danych";
            StatusGSM = reader["status_gsm"]?.ToString() ?? "Brak danych";
            StatusKamera = reader["status_kamera"]?.ToString() ?? "Brak danych";
        }
    }
}
